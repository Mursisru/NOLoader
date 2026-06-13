#if !NOLoader_DEV
using System;
using System.Collections.Generic;
using System.Reflection;

namespace NOLoader.Core.ModOptimizer
{
    internal static class ModAssemblyTracker
    {
        private static readonly HashSet<string> CoreAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NOLoader.Core",
            "NOLoader.API",
            "NOLoader.Registry",
            "NOLoader.Patcher",
            "Assembly-CSharp",
            "UnityEngine.CoreModule",
            "UnityEngine.PhysicsModule",
            "UnityEngine.IMGUIModule",
            "UnityEngine.UI",
            "UnityEngine.InputLegacyModule",
            "mscorlib",
            "System",
            "System.Core"
        };

        private static readonly Dictionary<string, int> ModIdByAssembly = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal static void Register(Assembly assembly, int modIdHash)
        {
            string name = assembly.GetName().Name ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                return;

            ModIdByAssembly[name] = modIdHash;
        }

        internal static void Unregister(Assembly assembly)
        {
            string name = assembly.GetName().Name ?? string.Empty;
            if (!string.IsNullOrEmpty(name))
                ModIdByAssembly.Remove(name);
        }

        internal static int TryGetModIdHash(Assembly assembly)
        {
            string name = assembly.GetName().Name ?? string.Empty;
            return ModIdByAssembly.TryGetValue(name, out int hash) ? hash : 0;
        }

        internal static bool IsModAssembly(Assembly? assembly)
        {
            if (assembly == null)
                return false;

            string name = assembly.GetName().Name ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                return false;

            if (CoreAssemblies.Contains(name))
                return false;

            if (name.StartsWith("UnityEngine.", StringComparison.OrdinalIgnoreCase))
                return false;

            if (name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase))
                return false;

            return ModIdByAssembly.ContainsKey(name);
        }
    }
}
#endif
