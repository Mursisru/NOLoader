using NOLoader.Core.Mods;
using NOLoader.Core.Runtime;

namespace NOLoader.Core.EngineTweaker
{
    /// <summary>Per-frame engine tweaker on WaitForEndOfFrame — after vanilla CameraStateManager/TrackIR.</summary>
    internal static class EngineTweakerFrameLoop
    {
        internal static void RunEndOfFrame()
        {
            if (!RuntimeConfig.EngineTweakerEnabled)
                return;

            if (TrackIrStabilityGuard.ShouldProtectVanillaCameraFrame())
            {
                FpsAdaptiveDetailGovernor.ForceRestoreIfThrottled();
                return;
            }

            EngineTweakerGameAccess.InvalidateCameraCache();
            FpsAdaptiveDetailGovernor.Tick();
            GroundVehicleRendererCull.ApplyFrame();

            if (!RuntimeConfig.FrameCacheEnabled)
                return;

            if (ModLifecycleManager.LoadedModCount <= 0 && EngineFrameCacheImpl.Instance.Reads <= 0)
                return;

            object? csm = EngineTweakerGameAccess.TryGetCameraStateManager();
            if (csm != null)
                EngineFrameCacheImpl.Instance.PopulateFromCameraState(csm);
        }
    }
}
