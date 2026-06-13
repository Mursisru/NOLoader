#if !NOLoader_DEV
using NOLoader.API;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;

namespace NOLoader.Core.GpuRender
{
    internal static class GpuRenderBootstrap
    {
        private static bool _initialized;

        internal static void Initialize(string gameRoot)
        {
            if (_initialized)
                return;

            _initialized = true;

            if (!RuntimeConfig.GpuRenderEnabled)
            {
                RingBufferLog.WriteAscii("[GpuRender] disabled (gpu_render=0)");
                return;
            }

            if (RuntimeConfig.GfxNativeJobsEnabled)
                BootConfigHelper.Apply(gameRoot, true);

            NOModRuntime.Gpu = GpuComputeService.Instance;

            RingBufferLog.WriteAscii("[GpuRender] enabled metrics=" + RuntimeConfig.GpuMetricsEnabled
                + " hudPass=" + RuntimeConfig.GpuHudPassEnabled
                + " fxInstancing=" + RuntimeConfig.GpuFxInstancingEnabled
                + " canvasLimiter=" + RuntimeConfig.CanvasLimiterEnabled);
        }

        internal static void OnUnityReady()
        {
            if (!RuntimeConfig.GpuRenderEnabled)
                return;

            GpuRenderHost.EnsureInstalled();
            GpuHudPass.Initialize();
            if (RuntimeConfig.GpuFxInstancingEnabled)
                FxInstancingRegistry.Initialize();
        }
    }
}
#endif
