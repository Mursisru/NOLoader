using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NOLoader.API;
using NOLoader.API.Manifest;
using NOLoader.Core.Gates;
using NOLoader.Core.Logging;
using NOLoader.Core.Manifest;
using NOLoader.Core.Mods;
using NOLoader.Core.Interop;
using NOLoader.Core.Patching;
using NOLoader.Patcher;
using NOLoader.Registry;
#if NOLoader_DEV
using NOLoader.Telemetry;
#endif
#if NOLoader_DEV
using NOLoader.Core.Development;
#endif

namespace NOLoader.Core
{
    public static class Bootstrap
    {
        private static int _initialized;
        private static int _mainMenuReady;
        private static int _coreStarted;
        private static Thread? _unityWaitThread;
        private static IReadOnlyList<ModManifest>? _bootstrapManifests;

        public static string GameRoot { get; private set; } = string.Empty;
        public static string LoaderRoot { get; private set; } = string.Empty;

        /// <summary>Native proxy entry — Unity init thread, before game assemblies load.</summary>
        public static int Initialize(string gameRoot)
        {
            if (string.IsNullOrEmpty(gameRoot))
                gameRoot = Environment.GetEnvironmentVariable("NOLOADER_GAME_ROOT") ?? string.Empty;
            if (string.IsNullOrEmpty(gameRoot))
                gameRoot = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
                return 0;

            UnityMainThread.RegisterBootstrapThread();

            GameRoot = gameRoot ?? string.Empty;
            LoaderRoot = Path.Combine(GameRoot, "NOLoader");
            Runtime.RuntimeConfig.Load(GameRoot);
            Directory.CreateDirectory(Path.Combine(LoaderRoot, "logs"));
#if !NOLoader_DEV
            Runtime.Balance.CoreBalancerBootstrap.Initialize();
            GpuRender.GpuRenderBootstrap.Initialize(GameRoot);
#endif

            ModAssemblyCache.Build(LoaderRoot, GameRoot);
            RingBufferLog.StartBackgroundFlush(LoaderRoot);
            RingBufferLog.WriteAscii("[NOLoader] ModAssemblyCache entries=" + ModAssemblyCache.EntryCount);

            RingBufferLog.WriteAscii("[NOLoader] Bootstrap.Initialize");
            RingBufferLog.WriteAscii($"[NOLoader] {AppVersion.Display}");

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                ApplyGamePatches();
            }
            catch (Exception ex)
            {
                RingBufferLog.WriteAscii("[NOLoader] FATAL patch: " + ex);
                File.WriteAllText(Path.Combine(LoaderRoot, "logs", "bootstrap_fatal.txt"), ex.ToString());
            }

            _unityWaitThread = new Thread(WaitForUnityEngine)
            {
                IsBackground = true,
                Name = "NOLoader.UnityWait",
                Priority = ThreadPriority.BelowNormal
            };
            _unityWaitThread.Start();
            return 0;
        }

        /// <summary>Cecil postfix on MainMenu::Init — load mod DLLs before Encyclopedia preload.</summary>
        public static void OnMainMenuReady()
        {
            if (Interlocked.CompareExchange(ref _mainMenuReady, 1, 0) != 0)
                return;

            UnityMainThread.EnsureReady();
            UnityMainThread.RegisterBootstrapThread();

            ModAssemblyCache.Rebuild(LoaderRoot, GameRoot);
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            ModPatchAssemblyPreloader.Reset();
            string modsRoot = Path.Combine(LoaderRoot, "mods");
            var manifests = ModManifestPipeline.ReadValidated(modsRoot, out var gateErrors, GameRoot);
            _bootstrapManifests = manifests;
            foreach (string err in gateErrors)
                RingBufferLog.WriteAscii("[GateL1] " + err);

            ModPatchAssemblyPreloader.EnsureLoaded(manifests, LoaderRoot);
            EngineTweaker.NOEngineTweakerBootstrap.Initialize();
            RingBufferLog.WriteAscii("[NOLoader] MainMenu hook fired (cache=" + ModAssemblyCache.EntryCount + ")");

            try
            {
                StartLoaderMainThread();
            }
            catch (Exception ex)
            {
                RingBufferLog.WriteAscii("[NOLoader] FATAL MainMenu: " + ex);
                File.WriteAllText(Path.Combine(LoaderRoot, "logs", "bootstrap_fatal.txt"), ex.ToString());
            }
        }

        private static Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                return null;

            string managed = Path.Combine(GameRoot, "NuclearOption_Data", "Managed", name + ".dll");
            if (File.Exists(managed))
                return Assembly.LoadFrom(managed);

            string core = Path.Combine(LoaderRoot, "core", name + ".dll");
            if (File.Exists(core))
                return Assembly.LoadFrom(core);

            if (ModAssemblyCache.TryGetPath(name, out string modPath))
                return Assembly.LoadFrom(modPath);

            return null;
        }

        private static void WaitForUnityEngine()
        {
#if NOLoader_DEV
            const int sleepMs = 100;
            const int maxAttempts = 600;
#else
            const int sleepMs = 200;
            const int maxAttempts = 300;
#endif
            for (int i = 0; i < maxAttempts; i++)
            {
                if (AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => string.Equals(a.GetName().Name, "UnityEngine.CoreModule", StringComparison.Ordinal)))
                {
                    RingBufferLog.StartBackgroundFlush(LoaderRoot);
#if NOLoader_DEV
                    HotReloadService.Initialize(Path.Combine(LoaderRoot, "mods"));
#endif
                    return;
                }
                Thread.Sleep(sleepMs);
            }
        }

        private static void ApplyGamePatches()
        {
            string modsRoot = Path.Combine(LoaderRoot, "mods");
            var manifests = ModManifestPipeline.ReadValidated(modsRoot, out var gateErrors, GameRoot);
            _bootstrapManifests = manifests;
            GateReportStore.Clear();
            foreach (string err in gateErrors)
            {
                string tag = err.IndexOf("expectedSignatureHash", StringComparison.Ordinal) >= 0
                    || err.IndexOf("Signature mismatch", StringComparison.Ordinal) >= 0
                    ? "[GateL2]"
                    : "[GateL1]";
                RingBufferLog.WriteAscii(tag + " " + err);
                if (tag == "[GateL2]")
                    GateReportStore.RecordL2(err);
                else
                    GateReportStore.RecordL1(err);
            }

            bool csharpCorePrePatched = AssemblyPatcher.HasAssemblyReference(GameRoot, "Assembly-CSharp.dll", "NOLoader.Core");

            ModIlAssemblyLoadHook.Register(manifests, LoaderRoot);
            ModIlAssemblyLoadHook.PreloadPatchAssemblies(manifests, LoaderRoot);

            if (csharpCorePrePatched)
            {
                RingBufferLog.WriteAscii("[NOLoader] Assembly-CSharp core pre-patched — runtime Cecil skipped for core");
            }
            else
            {
                byte[]? gameAssembly = AssemblyPatcher.LoadGameAssemblyBytes(GameRoot);
                if (gameAssembly == null)
                {
                    RingBufferLog.WriteAscii("[NOLoader] Assembly-CSharp.dll not found");
                    return;
                }

                var corePlan = CoreBootstrapPatches.CreateGameAssemblyPlan(LoaderRoot);
                PatchSignatureResolver.PopulateMissingCoreHashes(gameAssembly, GameRoot, corePlan);
                var bootstrapResult = AssemblyPatcher.ApplyPatches(gameAssembly, corePlan, GameRoot);
                foreach (string err in bootstrapResult.Errors)
                {
                    RingBufferLog.WriteAscii("[GateL2] " + err);
                    GateReportStore.RecordL2(err);
                }

                if (bootstrapResult.RolledBack || bootstrapResult.PatchedBytes == null)
                {
                    RingBufferLog.WriteAscii("[GateL2] Assembly-CSharp patch failed — close game and re-run deploy (PatchTool)");
                    return;
                }

                byte[] workingBytes = bootstrapResult.PatchedBytes;
                var modPatchPlan = PatchPlanBuilder.Build(manifests, LoadStage.MainMenu);
                if (modPatchPlan.Count > 0)
                {
                    var modResult = AssemblyPatcher.ApplyPatches(workingBytes, modPatchPlan, GameRoot);
                    foreach (string err in modResult.Errors)
                    {
                        RingBufferLog.WriteAscii("[GateL2] " + err);
                        GateReportStore.RecordL2(err);
                    }
                    if (modResult.PatchedBytes != null && !modResult.RolledBack)
                        workingBytes = modResult.PatchedBytes;
                }

                AssemblyPatcher.WriteManagedModuleBytes(GameRoot, "Assembly-CSharp.dll", workingBytes);
                ModPatchScheduler.MarkMainMenuApplied();
                RingBufferLog.WriteAscii("[NOLoader] Patched Assembly-CSharp written to disk");
            }

            ApplyUnityEnginePatches();
            ApplyUnityPhysicsPatches();
        }

        private static void ApplyUnityPhysicsPatches()
        {
            var plan = CoreBootstrapPatches.CreateUnityPhysicsPlan(LoaderRoot);
            if (plan.Count == 0)
            {
                if (IsAssemblyAlreadyPatched(GameRoot, "UnityEngine.PhysicsModule.dll", "RigidbodyAddForcePrefixSkip"))
                {
                    if (AssemblyPatcher.RestoreManagedModuleFromBackup(GameRoot, "UnityEngine.PhysicsModule.dll"))
                        RingBufferLog.WriteAscii("[NOLoader] UnityEngine.PhysicsModule restored (physics_catch_unity=0)");
                }
                return;
            }

            if (IsAssemblyAlreadyPatched(GameRoot, "UnityEngine.PhysicsModule.dll", "RigidbodyAddForcePrefixSkip"))
            {
                RingBufferLog.WriteAscii("[NOLoader] UnityEngine.PhysicsModule already patched (pre-apply)");
                return;
            }

            try
            {
                byte[]? physicsBytes = AssemblyPatcher.LoadUnityPhysicsModuleBytes(GameRoot);
                if (physicsBytes == null)
                {
                    RingBufferLog.WriteAscii("[NOLoader] UnityEngine.PhysicsModule.dll not found");
                    return;
                }

                byte[]? snapshot = (byte[])physicsBytes.Clone();
                PatchSignatureResolver.PopulateMissingCoreHashes(physicsBytes, GameRoot, plan);
                var result = AssemblyPatcher.ApplyPatches(physicsBytes, plan, GameRoot, snapshot);
                foreach (string err in result.Errors)
                    RingBufferLog.WriteAscii("[GateL2] " + err);

                if (result.PatchedBytes != null && !result.RolledBack)
                {
                    AssemblyPatcher.WriteManagedModuleBytes(GameRoot, "UnityEngine.PhysicsModule.dll", result.PatchedBytes);
                    RingBufferLog.WriteAscii("[NOLoader] Patched UnityEngine.PhysicsModule written to disk");
                }
            }
            catch (Exception ex)
            {
                RingBufferLog.WriteAscii("[GateL2] Physics module patch deferred: " + ex.Message);
            }
        }

        private static void ApplyUnityEnginePatches()
        {
            var unityPlan = CoreBootstrapPatches.CreateUnityCorePlan(LoaderRoot);
            if (unityPlan.Count == 0)
                return;

            if (IsAssemblyAlreadyPatched(GameRoot, "UnityEngine.CoreModule.dll", "NOLoader.Core.Gates.MissionGateHooks"))
            {
                RingBufferLog.WriteAscii("[NOLoader] UnityEngine.CoreModule already patched (pre-apply)");
                return;
            }

            try
            {
                byte[]? unityBytes = AssemblyPatcher.LoadUnityCoreModuleBytes(GameRoot);
                if (unityBytes == null)
                {
                    RingBufferLog.WriteAscii("[NOLoader] UnityEngine.CoreModule.dll not found");
                    return;
                }

                byte[]? unitySnapshot = (byte[])unityBytes.Clone();
                PatchSignatureResolver.PopulateMissingCoreHashes(unityBytes, GameRoot, unityPlan);
                var result = AssemblyPatcher.ApplyPatches(unityBytes, unityPlan, GameRoot, unitySnapshot);
                foreach (string err in result.Errors)
                    RingBufferLog.WriteAscii("[GateL2] " + err);

                if (result.PatchedBytes != null && !result.RolledBack)
                {
                    AssemblyPatcher.WriteManagedModuleBytes(GameRoot, "UnityEngine.CoreModule.dll", result.PatchedBytes);
                    RingBufferLog.WriteAscii("[NOLoader] Patched UnityEngine.CoreModule written to disk");
                }
            }
            catch (Exception ex)
            {
                RingBufferLog.WriteAscii("[GateL2] Unity patch deferred: " + ex.Message);
            }
        }

        private static bool IsAssemblyAlreadyPatched(string gameRoot, string moduleFile, string marker)
        {
            if (PatchStateCache.TryIsPatched(gameRoot, moduleFile, marker))
                return true;

            string path = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", moduleFile);
            if (!File.Exists(path))
                return false;

            byte[] needle = System.Text.Encoding.ASCII.GetBytes(marker);
            byte[] haystack = File.ReadAllBytes(path);
            return IndexOf(haystack, needle) >= 0;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
                return -1;

            int limit = haystack.Length - needle.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }

        internal static void StartLoaderMainThread()
        {
            if (Interlocked.CompareExchange(ref _coreStarted, 1, 0) != 0)
            {
                RingBufferLog.WriteAscii("[NOLoader] StartLoaderMainThread skipped (already started)");
                return;
            }

            string modsRoot = Path.Combine(LoaderRoot, "mods");
            var manifests = ModManifestPipeline.ReadValidated(modsRoot, out _, GameRoot);
            _bootstrapManifests = manifests;
#if NOLoader_DEV
            DevOverlayHost.Initialize(LoaderRoot);
            GateL1PanelHost.InstallIfNeeded();
#endif

            LoaderLog.Bind(RingBufferLog.WriteAscii);
            NOModRegistry.Initialize();
            PhysicsSafetyCatch.Install();
            ModLifecycleManager.Initialize(manifests, GameRoot, LoaderRoot);
            ModLifecycleManager.NotifyMainMenuReady();

#if NOLoader_DEV
            TelemetryService.Initialize(GameRoot);
            TelemetryHost.Install();
#endif

            MissionGate.Install();

#if NOLoader_DEV
            UnityLogBridge.Log("[NOLoader] Core started " + AppVersion.Display);
#endif
            RingBufferLog.WriteAscii("[NOLoader] Core started " + AppVersion.Display);
        }
    }
}
