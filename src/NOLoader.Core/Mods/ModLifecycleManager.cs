using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NOLoader.API;
using NOLoader.API.Manifest;
using NOLoader.Core.Gates;
using NOLoader.Core.Interop;
using NOLoader.Core.Logging;
using NOLoader.Core.Manifest;
using NOLoader.Core.Patching;
using UnityEngine;
#if !NOLoader_DEV
using NOLoader.Core.Runtime.Perf;
#endif

namespace NOLoader.Core.Mods
{
    public sealed class LoadedMod
    {
        public ModManifest Manifest = null!;
        public Assembly? Assembly;
        public INOMod? Instance;
        public bool Loaded;
        public bool Failed;
        public string? Error;
        public bool BlockedForMission;
        public Exception? LastException;
    }

    public static class ModLifecycleManager
    {
        private static readonly List<LoadedMod> Mods = new List<LoadedMod>();
        private static int _missionBlockedFlag;
        private static string _gameRoot = string.Empty;
        private static string _loaderRoot = string.Empty;
        private static LoadStage _currentStage = LoadStage.PreMenu;
        private static bool _mainMenuReady;
        private static bool _missionReady;
#if !NOLoader_DEV
        private static readonly Dictionary<LoadedMod, LoadedModEntry> PerfEntries = new Dictionary<LoadedMod, LoadedModEntry>();
#endif

        internal static string GameRootPath => _gameRoot;

        public static IReadOnlyList<LoadedMod> AllMods => Mods;

        public static int LoadedModCount
        {
            get
            {
                int count = 0;
                foreach (LoadedMod mod in Mods)
                {
                    if (mod.Loaded)
                        count++;
                }
                return count;
            }
        }

        public static bool MissionBlockedFlag => _missionBlockedFlag != 0;

        internal static int MissionBlockedFlagRaw => _missionBlockedFlag;

        public static bool IsMainMenuReady => _mainMenuReady;

        public static bool IsMissionReady => _missionReady;

        public static void Initialize(IReadOnlyList<ModManifest> manifests, string gameRoot, string loaderRoot)
        {
            _gameRoot = gameRoot;
            _loaderRoot = loaderRoot;
            _manifests = manifests;

            var ordered = TopologicalSort(manifests.Where(m => m.Valid).ToList());
            foreach (ModManifest manifest in ordered)
            {
                Mods.Add(new LoadedMod { Manifest = manifest });
            }

#if !NOLoader_DEV
            NOModPerfBootstrap.Initialize();
#endif
            ActivateStage(LoadStage.PreMenu);

            if (RequiresMissionStage(manifests))
                MissionStageObserver.Install();
        }

        private static IReadOnlyList<ModManifest>? _manifests;

        private static bool RequiresMissionStage(IReadOnlyList<ModManifest> manifests)
        {
            foreach (ModManifest manifest in manifests)
            {
                if (manifest.Valid && manifest.LoadStage == LoadStage.Mission)
                    return true;
            }

            return false;
        }

        public static void NotifyMainMenuReady()
        {
            if (_mainMenuReady)
                return;
            _mainMenuReady = true;
            ActivateStage(LoadStage.MainMenu);
        }

        public static void NotifyMissionReady()
        {
            if (_missionReady)
                return;
            _missionReady = true;

            IReadOnlyList<ModManifest> manifests = _manifests
                ?? ModManifestPipeline.ReadValidated(Path.Combine(_loaderRoot, "mods"), out _, _gameRoot);
            ModPatchScheduler.ApplyThroughStage(_gameRoot, manifests, LoadStage.Mission);
            TryLogModIlProbe();
            ActivateStage(LoadStage.Mission);
        }

        private static void TryLogModIlProbe()
        {
            try
            {
                Type? aircraftType = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
                    ?.GetType("Aircraft");
                ModIlMethodProbe.LogProbe(_gameRoot, aircraftType, "FixedUpdatePostfix");
            }
            catch
            {
                /* best effort */
            }
        }

        public static void ActivateStage(LoadStage stage)
        {
            if (!UnityMainThread.IsMainThread)
            {
                DispatchActivateStage(stage);
                return;
            }

            if (stage < _currentStage && stage != LoadStage.PreMenu)
                return;

            _currentStage = stage;
            foreach (LoadedMod mod in Mods)
            {
                if (mod.Manifest.LoadStage == stage && !mod.Loaded && !mod.Failed)
                    LoadMod(mod);
            }
        }

        private static void DispatchActivateStage(LoadStage stage)
        {
            UnityMainThread.Invoke(() => ActivateStage(stage));
        }

        public static void ReloadMod(string modId)
        {
#if NOLoader_DEV
            LoadedMod? mod = Mods.FirstOrDefault(m => m.Manifest.Id == modId || m.Manifest.IdHash.ToString("X8") == modId);
            if (mod == null) return;
            UnloadMod(mod);
            mod.Loaded = false;
            mod.Failed = false;
            LoadMod(mod);
#endif
        }

        public static void ReloadAllMods()
        {
#if NOLoader_DEV
            foreach (LoadedMod mod in Mods.ToList())
            {
                if (!mod.Loaded && !mod.Failed)
                    continue;
                UnloadMod(mod);
                mod.Loaded = false;
                mod.Failed = false;
                mod.BlockedForMission = false;
                LoadMod(mod);
            }
#endif
        }

        private static void LoadMod(LoadedMod mod)
        {
            try
            {
                string asmPath = Path.Combine(mod.Manifest.FolderPath, mod.Manifest.Assembly);
                if (!File.Exists(asmPath))
                    throw new FileNotFoundException("Mod assembly missing", asmPath);

                mod.Assembly = Assembly.LoadFrom(asmPath);
                Type? entryType = mod.Assembly.GetType(mod.Manifest.EntryType, throwOnError: true);
                mod.Instance = (INOMod?)Activator.CreateInstance(entryType!);
                if (mod.Instance == null)
                    throw new InvalidOperationException("Entry type does not implement INOMod");

                var ctx = new NOModContext
                {
                    GameRoot = _gameRoot,
                    ModRoot = mod.Manifest.FolderPath,
                    ModId = mod.Manifest.Id,
                    ModIdHash = mod.Manifest.IdHash,
                    ModVersion = mod.Manifest.Version,
                    Stage = mod.Manifest.LoadStage,
                    Services = CreateModServices()
                };

                mod.Instance.OnLoad(ref ctx);
                mod.Loaded = true;

#if !NOLoader_DEV
                LoadedModEntry perfEntry = ModTickScheduler.CreateEntry(mod);
                PerfEntries[mod] = perfEntry;
                NOModPerfBootstrap.OnModLoaded(perfEntry);
#endif
#if NOLoader_DEV
                ModAuthorContractEnforcer.Validate(mod);
#endif
                MissionGate.EnsureExceptionTracking();
#if NOLoader_DEV
                UnityLogBridge.Log("[NOLoader] Mod loaded: " + mod.Manifest.Id);
#endif
            }
            catch (Exception ex)
            {
                mod.Failed = true;
                mod.Error = ex.Message;
                mod.LastException = ex;
                if (mod.Manifest.LoadStage == LoadStage.Mission || mod.Manifest.LoadStage == LoadStage.MainMenu)
                {
                    string modLabel = !string.IsNullOrEmpty(mod.Manifest.Id)
                        ? mod.Manifest.Id
                        : mod.Manifest.IdHash.ToString("X8");
                    FlagModForMissionBlock(modLabel, ex);
                }
                UnityLogBridge.LogError("[NOLoader] Mod failed: " + mod.Manifest.Id + " — " + ex.Message);
            }
        }

        private static void UnloadMod(LoadedMod mod)
        {
            if (mod.Instance == null) return;
#if !NOLoader_DEV
            if (PerfEntries.TryGetValue(mod, out LoadedModEntry? perfEntry))
            {
                NOModPerfBootstrap.OnModUnloaded(perfEntry);
                PerfEntries.Remove(mod);
            }
#endif

            try
            {
                var ctx = new NOModContext
                {
                    GameRoot = _gameRoot,
                    ModRoot = mod.Manifest.FolderPath,
                    ModId = mod.Manifest.Id,
                    ModIdHash = mod.Manifest.IdHash,
                    ModVersion = mod.Manifest.Version,
                    Stage = mod.Manifest.LoadStage,
                    Services = CreateModServices()
                };
                mod.Instance.OnUnload(ref ctx);
            }
            catch { /* best effort */ }
            mod.Instance = null;
            mod.Assembly = null;
        }

        private static List<ModManifest> TopologicalSort(List<ModManifest> manifests)
        {
            var byId = new Dictionary<string, ModManifest>(StringComparer.OrdinalIgnoreCase);
            foreach (ModManifest m in manifests)
            {
                if (!string.IsNullOrEmpty(m.Id))
                    byId[m.Id] = m;
                if (m.IdHash != 0)
                    byId[m.IdHash.ToString("X8")] = m;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<ModManifest>();

            void Visit(string id)
            {
                if (string.IsNullOrEmpty(id) || visited.Contains(id))
                    return;
                visited.Add(id);
                if (!byId.TryGetValue(id, out ModManifest? m))
                    return;
                foreach (string dep in m.Dependencies)
                    Visit(dep);
                result.Add(m);
            }

            foreach (ModManifest m in manifests)
            {
                string key = !string.IsNullOrEmpty(m.Id) ? m.Id : m.IdHash.ToString("X8");
                Visit(key);
            }

            return result;
        }

        public static void FlagModForMissionBlock(string modId, Exception ex)
        {
            LoadedMod? mod = Mods.FirstOrDefault(m =>
                string.Equals(m.Manifest.Id, modId, StringComparison.OrdinalIgnoreCase)
                || (m.Manifest.IdHash != 0 && string.Equals(m.Manifest.IdHash.ToString("X8"), modId, StringComparison.OrdinalIgnoreCase)));
            if (mod == null) return;
            mod.BlockedForMission = true;
            mod.LastException = ex;
            _missionBlockedFlag = 1;
            RingBufferLog.WriteAscii("[GateL4] Mission block flagged: " + modId);
        }

        public static bool AnyMissionBlocked => MissionBlockedFlag;

        private static NOModServices CreateModServices()
        {
#if !NOLoader_DEV
            return NOModPerfBootstrap.CreateServices(requestWorld: false);
#else
            return new NOModServices { Pool = NOModRuntime.Pool };
#endif
        }
    }
}
