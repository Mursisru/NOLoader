using NOLoader.API;
using NOLoader.API.World;
using NOLoader.ModConfig;

namespace NOLoader.CoreBalancerVerify
{
    public sealed class CoreBalancerVerifyMod : INOMod, INOModTickSlow, INOModBackgroundWork
    {
        public void OnLoad(ref NOModContext ctx)
        {
            CoreBalancerVerifyState.Load(ModIniConfig.Load(ctx.ModRoot, "mod.ini"));
            CoreBalancerVerifyReporter.LogOnLoad(ref ctx);
        }

        public void OnUnload(ref NOModContext ctx)
        {
            if (CoreBalancerVerifyState.ReportOnUnload)
                CoreBalancerVerifyReporter.LogSummary(ref ctx);
        }

        public void OnSlowUpdate(ref NOModContext ctx, float dt)
        {
            CoreBalancerVerifyState.SlowCount++;

            if (!CoreBalancerVerifyState.RunComputeProbeSent && CoreBalancerVerifyState.SlowCount >= 2)
            {
                CoreBalancerVerifyState.RunComputeProbeSent = true;
                CoreBalancerVerifyState.RunComputeRequested++;
                float expected = CoreBalancerVerifyProbe.RunDeterministicMath(8000);
                NOModRuntime.Scheduler.RunCompute(
                    () => { CoreBalancerVerifyState.LastRunComputeResult = expected; },
                    () => { CoreBalancerVerifyState.RunComputeApplied++; });
            }

            if (CoreBalancerVerifyState.ShouldReport())
                CoreBalancerVerifyReporter.LogPeriodic(ref ctx);
        }

        public void OnCaptureInputs(ref NOModContext ctx, ref ModWorkInput input)
        {
            input.Param0 = CoreBalancerVerifyState.MathIterations;
            INOModWorldReader? stable = NOModRuntime.StableWorld;
            input.Param1 = stable != null ? stable.FrameId : -1;
            input.Param2 = stable != null ? stable.UnitCount : -1;
        }

        public void OnCompute(in ModWorkInput input, ref ModWorkOutput output)
        {
            CoreBalancerVerifyState.ComputeRuns++;
            output.FrameId = input.FrameId;
            output.Result0 = CoreBalancerVerifyProbe.RunDeterministicMath((int)input.Param0);
            output.Result1 = input.Param1;

            INOModWorldReader? stable = NOModRuntime.StableWorld;
            if (stable != null && stable.FrameId >= 0)
            {
                CoreBalancerVerifyState.StableWorldPassCount++;
                output.Result1 = stable.FrameId * 0.001f + stable.UnitCount;
            }
            else
            {
                CoreBalancerVerifyState.StableWorldFailCount++;
            }
        }

        public void OnApplyResults(ref NOModContext ctx, in ModWorkOutput output)
        {
            CoreBalancerVerifyState.ApplyRuns++;
            CoreBalancerVerifyState.LastComputeResult = output.Result0;

            if (output.Result0 > 0f)
            {
                CoreBalancerVerifyLogger.Pass("worker_math",
                    "result=" + output.Result0.ToString("F3") + " frame=" + output.FrameId);
            }
        }
    }
}
