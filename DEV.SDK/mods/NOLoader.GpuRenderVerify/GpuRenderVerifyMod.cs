using NOLoader.API;
using NOLoader.ModConfig;

namespace NOLoader.GpuRenderVerify
{
    /// <summary>Single RDYTU field-test mod for DEV2O13 (GpuRender + sim sanity).</summary>
    public sealed class GpuRenderVerifyMod : INOMod, INOModTickSlow, INOModGpuCompute
    {
        public void OnLoad(ref NOModContext ctx)
        {
            GpuRenderVerifyState.Load(ModIniConfig.Load(ctx.ModRoot, "mod.ini"));
            GpuRenderVerifyReporter.LogOnLoad(ref ctx);
        }

        public void OnUnload(ref NOModContext ctx)
        {
            GpuRenderVerifyReporter.LogSummary(ref ctx);
        }

        public void OnSlowUpdate(ref NOModContext ctx, float dt)
        {
            GpuRenderVerifyState.SlowCount++;
            if (GpuRenderVerifyState.ShouldReport())
                GpuRenderVerifyReporter.LogPeriodic(ref ctx);
        }

        public void OnDispatchGpu(ref NOModContext ctx, object commandBuffer)
        {
            GpuRenderVerifyState.GpuDispatchCount++;
        }
    }
}
