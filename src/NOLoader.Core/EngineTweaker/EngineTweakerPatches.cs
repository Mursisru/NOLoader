using System.Collections.Generic;
using NOLoader.API.Manifest;
using NOLoader.Core.Runtime;
using NOLoader.Patcher;

namespace NOLoader.Core.EngineTweaker
{
    internal static class EngineTweakerPatches
    {
        public static List<PatchEntry> CreateGamePlan(string coreDir)
        {
            var plan = new List<PatchEntry>();
#if NOLoader_DEV
            return plan;
#else
            if (!RuntimeConfig.EngineTweakerEnabled)
                return plan;

            if (RuntimeConfig.StringCacheEnabled)
            {
                plan.Add(Entry(coreDir, "noloader.tweaker.speed", "UnitConverter::SpeedReading",
                    "NOLoader.Core.EngineTweaker.EngineStringCache::SpeedReading", "Redirect", "081F0860652B76C6"));
                plan.Add(Entry(coreDir, "noloader.tweaker.speedground", "UnitConverter::SpeedReadingGround",
                    "NOLoader.Core.EngineTweaker.EngineStringCache::SpeedReadingGround", "Redirect", "BFE30D916E810B0C"));
                plan.Add(Entry(coreDir, "noloader.tweaker.alt", "UnitConverter::AltitudeReading",
                    "NOLoader.Core.EngineTweaker.EngineStringCache::AltitudeReading", "Redirect", "A64A437E626A7C60"));
                plan.Add(Entry(coreDir, "noloader.tweaker.climb", "UnitConverter::ClimbRateReading",
                    "NOLoader.Core.EngineTweaker.EngineStringCache::ClimbRateReading", "Redirect", "6B6EAEBD6C9BDC0B"));
                plan.Add(Entry(coreDir, "noloader.tweaker.dist", "UnitConverter::DistanceReading",
                    "NOLoader.Core.EngineTweaker.EngineStringCache::DistanceReading", "Redirect", "6AB26B2115C4FE7D"));
            }

            // CameraStateManager — intentionally not patched (vanilla TrackIR / cockpit camera).

            if (RuntimeConfig.CullingGroundWheelsEnabled)
            {
                plan.Add(Entry(coreDir, "noloader.tweaker.gvupdate", "GroundVehicle::Update",
                    "NOLoader.Core.EngineTweaker.NOEngineTweakerHooks::GroundVehicleUpdatePrefixSkip", "PrefixSkip"));
                plan.Add(Entry(coreDir, "noloader.tweaker.wheels", "GroundVehicle::AnimateWheels",
                    "NOLoader.Core.EngineTweaker.NOEngineTweakerHooks::AnimateWheelsPrefixSkip", "PrefixSkip"));
            }

            if (RuntimeConfig.CullingPilotAnimEnabled)
            {
                plan.Add(Entry(coreDir, "noloader.tweaker.pilot", "Pilot::Update",
                    "NOLoader.Core.EngineTweaker.NOEngineTweakerHooks::PilotUpdatePrefix", "Prefix"));
                plan.Add(Entry(coreDir, "noloader.tweaker.pilotdismount", "PilotDismounted::FixedUpdate",
                    "NOLoader.Core.EngineTweaker.NOEngineTweakerHooks::PilotDismountedFixedUpdatePrefix", "Prefix"));
            }

            return plan;
#endif
        }

        public static List<PatchEntry> CreateUnityUiPlan(string coreDir)
        {
            var plan = new List<PatchEntry>();
#if NOLoader_DEV
            return plan;
#else
            if (!RuntimeConfig.EngineTweakerEnabled && !RuntimeConfig.GpuRenderEnabled)
                return plan;

            if (!RuntimeConfig.CanvasLimiterEnabled)
                return plan;

            plan.Add(Entry(coreDir, "noloader.tweaker.canvas", "UnityEngine.UI.CanvasUpdateRegistry::RegisterCanvasElementForGraphicRebuild",
                "NOLoader.Core.EngineTweaker.NOEngineTweakerHooks::RegisterCanvasElementForGraphicRebuildPrefixSkip", "PrefixSkip", "5A5078F468505E08"));
            return plan;
#endif
        }

        private static PatchEntry Entry(
            string coreDir,
            string modId,
            string target,
            string inject,
            string method,
            string? hash = null)
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
                    Method = method,
                    ExpectedSignatureHash = hash
                }
            };
        }
    }
}
