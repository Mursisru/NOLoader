#if !NOLoader_DEV
using System.Collections.Generic;
using NOLoader.API.Manifest;
using NOLoader.Core.Runtime;
using NOLoader.Patcher;

namespace NOLoader.Core.ModOptimizer
{
    internal static class ModOptimizerPatches
    {
        public static List<PatchEntry> CreateUnityCorePlan(string coreDir)
        {
            var plan = new List<PatchEntry>();
            if (!RuntimeConfig.ModOptimizerEnabled || !RuntimeConfig.ModSceneLocatorEnabled)
                return plan;

            plan.Add(Entry(coreDir, "noloader.modopt.find", "UnityEngine.GameObject::Find",
                "NOLoader.Core.ModOptimizer.ModOptimizerHooks::FindRedirect", "Redirect"));

            return plan;
        }

        private static PatchEntry Entry(string coreDir, string modId, string target, string inject, string method)
        {
            return new PatchEntry
            {
                ModId = modId,
                ModFolder = coreDir,
                InjectAssembly = "NOLoader.Core.dll",
                Descriptor = new PatchDescriptor
                {
                    Target = target,
                    Inject = inject,
                    Method = method
                }
            };
        }
    }
}
#endif
