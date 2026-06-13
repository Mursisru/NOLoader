#if !NOLoader_DEV
using System.Collections.Generic;
using NOLoader.API.Manifest;
using NOLoader.Core.Runtime;
using NOLoader.Patcher;

namespace NOLoader.Core.GpuRender
{
    internal static class GpuRenderPatches
    {
        public static List<PatchEntry> CreateGamePlan(string coreDir)
        {
            var plan = new List<PatchEntry>();
            if (!RuntimeConfig.GpuRenderEnabled)
                return plan;

            if (RuntimeConfig.GpuHudPassEnabled)
            {
                plan.Add(Entry(coreDir, "noloader.gpur.hud", "CombatHUD::LateUpdate",
                    "NOLoader.Core.GpuRender.GpuRenderHooks::CombatHudLateUpdatePostfix", "Postfix"));
                plan.Add(Entry(coreDir, "noloader.gpur.marker", "HUDUnitMarker::UpdatePosition",
                    "NOLoader.Core.GpuRender.GpuRenderHooks::HudUnitMarkerUpdatePositionPrefixSkip", "PrefixSkip"));
            }

            if (RuntimeConfig.GpuFxInstancingEnabled)
            {
                plan.Add(Entry(coreDir, "noloader.gpur.chaff", "RadarChaff::LaunchChaff",
                    "NOLoader.Core.GpuRender.GpuRenderHooks::RadarChaffLaunchChaffPostfix", "Postfix"));
            }

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
