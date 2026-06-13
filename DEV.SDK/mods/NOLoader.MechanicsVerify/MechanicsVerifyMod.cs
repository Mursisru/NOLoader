using NOLoader.API;
using NOLoader.ModConfig;

namespace NOLoader.MechanicsVerify
{
    public sealed class MechanicsVerifyMod : INOMod, INOModTickNormal, INOModTickSlow
    {
        public void OnLoad(ref NOModContext ctx)
        {
            MechanicsVerifyState.Load(ModIniConfig.Load(ctx.ModRoot, "mod.ini"));
            MechanicsVerifyReporter.LogOnLoad(ref ctx);
        }

        public void OnUnload(ref NOModContext ctx)
        {
            if (MechanicsVerifyState.ReportOnUnload)
                MechanicsVerifyReporter.LogSummary(ref ctx);
        }

        public void OnNormalUpdate(ref NOModContext ctx, float dt)
        {
            if (!MechanicsVerifyProbe.TrySampleLocalAircraft(out MechanicsSample sample))
                return;

            if (sample.Throttle > MechanicsVerifyState.MaxThrottleSeen)
                MechanicsVerifyState.MaxThrottleSeen = sample.Throttle;

            float delta = sample.Throttle - MechanicsVerifyState.LastTickThrottle;
            if (System.Math.Abs(delta) >= 0.05f)
            {
                MechanicsVerifyState.LastTickThrottle = sample.Throttle;
                MechanicsVerifyLogger.Info("thr_delta " + MechanicsVerifyProbe.FormatSample(sample));
            }
        }

        public void OnSlowUpdate(ref NOModContext ctx, float dt)
        {
            MechanicsVerifyState.SlowCount++;
            if (MechanicsVerifyState.ShouldReport())
                MechanicsVerifyReporter.LogPeriodic(ref ctx);
        }
    }
}
