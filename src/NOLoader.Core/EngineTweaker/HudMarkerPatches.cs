using System.Collections.Generic;
using NOLoader.API.Manifest;
using NOLoader.Core.Runtime;
using NOLoader.Patcher;

namespace NOLoader.Core.EngineTweaker
{
    internal static class HudMarkerPatches
    {
        public static List<PatchEntry> CreateGamePlan(string coreDir)
        {
            var plan = new List<PatchEntry>();
#if NOLoader_DEV
            return plan;
#else
            if (!RuntimeConfig.HudMarkerThrottleEnabled)
                return plan;

            plan.Add(Entry(coreDir, "noloader.hudmarker.begin", "CombatHUD::UpdateMarkers",
                "NOLoader.Core.EngineTweaker.HudMarkerThrottleHooks::UpdateMarkersPrefix", "Prefix"));
            plan.Add(Entry(coreDir, "noloader.hudmarker.pos", "HUDUnitMarker::UpdatePosition",
                "NOLoader.Core.EngineTweaker.HudMarkerThrottleHooks::UpdatePositionPrefixSkip", "PrefixSkip"));
            plan.Add(Entry(coreDir, "noloader.hudmarker.jam", "HUDUnitMarker::JammingDistortion",
                "NOLoader.Core.EngineTweaker.HudMarkerThrottleHooks::JammingDistortionPrefixSkip", "PrefixSkip"));
            return plan;
#endif
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
