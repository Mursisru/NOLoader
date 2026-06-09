using System;
using System.Reflection;
using NOLoader.API;
using UnityEngine;

namespace NOLoader.DiagCommon
{
    public static class CoreProbe
    {
        public static bool LoaderLogBound()
        {
            FieldInfo? sink = typeof(LoaderLog).GetField("_sink", BindingFlags.NonPublic | BindingFlags.Static);
            return sink?.GetValue(null) != null;
        }

        public static int ReadTelemetryPort()
        {
            PropertyInfo? port = FindCoreOrTelemetryType("NOLoader.Telemetry.TelemetryService")
                ?.GetProperty("Port", BindingFlags.Public | BindingFlags.Static);
            return port?.GetValue(null) is int p ? p : -1;
        }

        public static bool ReadGateL2Enforced()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(asm.GetName().Name, "NOLoader.Patcher", StringComparison.Ordinal))
                    continue;

                FieldInfo? field = asm.GetType("NOLoader.Patcher.AssemblyPatcher", throwOnError: false)
                    ?.GetField("RequireSignatureHashes", BindingFlags.Public | BindingFlags.Static);
                return field?.GetValue(null) is bool b && b;
            }

            return false;
        }

        public static bool TryReadMissionGateInstalled(out bool value)
        {
            value = false;
            Type? t = FindCoreType("NOLoader.Core.Gates.MissionGate");
            if (t == null)
                return false;

            PropertyInfo? prop = t.GetProperty("IsInstalled", BindingFlags.Public | BindingFlags.Static);
            if (prop?.GetValue(null) is bool b)
            {
                value = b;
                return true;
            }

            return false;
        }

        public static bool TryReadDevFlag(string typeName, out bool value)
        {
            value = false;
            Type? t = FindCoreType("NOLoader.Core.Development." + typeName);
            if (t == null)
                return false;

            FieldInfo? installed = t.GetField("_installed", BindingFlags.NonPublic | BindingFlags.Static);
            if (installed?.GetValue(null) is bool b)
            {
                value = b;
                return true;
            }

            FieldInfo? host = t.GetField("_host", BindingFlags.NonPublic | BindingFlags.Static);
            if (host != null)
            {
                value = host.GetValue(null) != null;
                return true;
            }

            return false;
        }

        public static bool ReadMissionBlocked()
        {
            PropertyInfo? prop = FindCoreType("NOLoader.Core.Mods.ModLifecycleManager")
                ?.GetProperty("AnyMissionBlocked", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) is bool b && b;
        }

        public static int ReadPhysicsCatchCount()
        {
            Type? hooks = typeof(NOLoader.Registry.NOModRegistry).Assembly
                .GetType("NOLoader.Registry.PhysicsCatchHooks");
            PropertyInfo? prop = hooks?.GetProperty("InterceptCount", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) is int n ? n : -1;
        }

        public static int ReadModAssemblyCacheCount()
        {
            Type? t = FindCoreType("NOLoader.Core.Interop.ModAssemblyCache");
            PropertyInfo? prop = t?.GetProperty("EntryCount", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) is int n ? n : -1;
        }

        public static int ReadGameTypeCacheCount()
        {
            Type? t = FindCoreType("NOLoader.Core.Interop.GameTypeCache");
            PropertyInfo? prop = t?.GetProperty("CachedTypeCount", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) is int n ? n : -1;
        }

        public static int ReadReflectionResolveCount()
        {
            Type? t = FindCoreType("NOLoader.Core.Development.ReflectionTracker");
            PropertyInfo? prop = t?.GetProperty("TotalResolves", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) is int n ? n : -1;
        }

        public static string ReadCoreDisplayVersion()
        {
            PropertyInfo? prop = FindCoreType("NOLoader.Core.AppVersion")
                ?.GetProperty("Display", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) as string ?? string.Empty;
        }

        private static readonly string[] BakedCoreModIds =
        {
            "noloader.bootstrap",
            "noloader.registry",
            "noloader.physics",
            "noloader.gatel4",
            "noloader.gatel4.scene",
            "noloader.physics.unity",
            "noloader.physics.unity.single"
        };

        public static int ReadBakedCoreHashCount()
        {
            Type? t = FindCoreType("NOLoader.Core.Patching.CoreBootstrapPatchHashes");
            MethodInfo? tryGet = t?.GetMethod("TryGet", BindingFlags.Public | BindingFlags.Static);
            if (tryGet == null)
                return 0;

            int count = 0;
            foreach (string modId in BakedCoreModIds)
            {
                if (tryGet.Invoke(null, new object[] { modId }) is string hash && !string.IsNullOrEmpty(hash))
                    count++;
            }

            return count;
        }

        public static bool ReadRingLogContains(string gameRoot, string fragment)
        {
            string path = System.IO.Path.Combine(gameRoot, "NOLoader", "logs", "noloader_ring.log");
            if (!System.IO.File.Exists(path))
                return false;

            try
            {
                return System.IO.File.ReadAllText(path).IndexOf(fragment, StringComparison.Ordinal) >= 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsCorePrePatched(string gameRoot)
        {
            return GameProbe.IsDllMarker(gameRoot, "Assembly-CSharp.dll", "OnMainMenuReady")
                && GameProbe.IsDllMarker(gameRoot, "Assembly-CSharp.dll", "CanLoadPrefixSkip");
        }

        public static bool IsGameFullyPrePatched(string gameRoot)
        {
            return IsCorePrePatched(gameRoot)
                && GameProbe.IsDllMarker(gameRoot, "UnityEngine.CoreModule.dll", "SceneLoadPrefixSkip")
                && GameProbe.IsDllMarker(gameRoot, "UnityEngine.PhysicsModule.dll", "RigidbodyAddForcePrefixSkip");
        }

        public static bool IsTelemetryHostPresent()
            => GameObject.Find("NOLoader.TelemetryHost") != null;

        public static bool LoadedModSupportsMissionFaultTracking()
        {
            Type? t = FindCoreType("NOLoader.Core.Mods.LoadedMod");
            if (t == null)
                return false;

            return t.GetField("BlockedForMission", BindingFlags.Public | BindingFlags.Instance) != null
                && t.GetField("LastException", BindingFlags.Public | BindingFlags.Instance) != null;
        }

        public static bool GateL4HooksPresent(string gameRoot)
        {
            return GameProbe.IsDllMarker(gameRoot, "Assembly-CSharp.dll", "CanLoadPrefixSkip")
                && GameProbe.IsDllMarker(gameRoot, "UnityEngine.CoreModule.dll", "SceneLoadPrefixSkip");
        }

        public static bool ReadTelemetryBindingsReady()
        {
            PropertyInfo? prop = FindCoreOrTelemetryType("NOLoader.Telemetry.TelemetryService")
                ?.GetProperty("BindingsReady", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) is bool b && b;
        }

        public static bool ReadTelemetrySnapshotValid()
        {
            PropertyInfo? prop = FindCoreOrTelemetryType("NOLoader.Telemetry.TelemetryService")
                ?.GetProperty("HasValidSnapshot", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) is bool b && b;
        }

        public static bool IsGateReportStorePresent()
            => FindCoreType("NOLoader.Core.Gates.GateReportStore") != null;

        public static bool IsModPatchSchedulerPresent()
            => FindCoreType("NOLoader.Core.Patching.ModPatchScheduler") != null;

        public static bool IsRegistryPipelinePresent()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(asm.GetName().Name, "NOLoader.Registry", StringComparison.Ordinal))
                    return asm.GetType("NOLoader.Registry.NOModRegistryPipeline", throwOnError: false) != null;
            }

            return false;
        }

        public static bool ReadManifestHasGuid(string modRoot)
        {
            string path = System.IO.Path.Combine(modRoot, "mod.json");
            if (!System.IO.File.Exists(path))
                return false;
            string text = System.IO.File.ReadAllText(path);
            return text.IndexOf("\"guid\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool ReadHashOnlyMode()
        {
            PropertyInfo? prop = typeof(StringHashTable).GetProperty("DevAutoRegisterEnabled", BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null) is bool b && !b;
        }

        private static Type? FindCoreType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(asm.GetName().Name, "NOLoader.Core", StringComparison.Ordinal))
                    continue;
                Type? t = asm.GetType(fullName, throwOnError: false);
                if (t != null)
                    return t;
            }
            return null;
        }

        private static Type? FindCoreOrTelemetryType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string? name = asm.GetName().Name;
                if (name != "NOLoader.Core" && name != "NOLoader.Telemetry")
                    continue;
                Type? t = asm.GetType(fullName, throwOnError: false);
                if (t != null)
                    return t;
            }
            return null;
        }
    }
}
