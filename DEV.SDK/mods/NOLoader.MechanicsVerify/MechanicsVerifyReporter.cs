using System;
using NOLoader.API;

namespace NOLoader.MechanicsVerify
{
    internal static class MechanicsVerifyReporter
    {
        internal static void LogOnLoad(ref NOModContext ctx)
        {
            if (MechanicsVerifyState.OnLoadLogged)
                return;

            MechanicsVerifyState.OnLoadLogged = true;
            MechanicsVerifyLogger.Phase("OnLoad");
            MechanicsVerifyLogger.Info("mod=" + ctx.ModId + " hash=" + ctx.ModIdHash.ToString("X8")
                + " patches=0 (read-only probe)");
            MechanicsVerifyLogger.Pass("manifest", "no Cecil patches — cannot stale-patch Assembly-CSharp");
            MechanicsVerifyLogger.Info("Apply throttle in flight; expect [PASS] display_detail + thrust_sim");
        }

        internal static void LogPeriodic(ref NOModContext ctx)
        {
            int elapsedSec = (MechanicsVerifyState.EnvironmentTickMs() - MechanicsVerifyState.SessionStartMs) / 1000;

            if (!MechanicsVerifyProbe.TrySampleLocalAircraft(out MechanicsSample sample))
            {
                MechanicsVerifyLogger.Warn("aircraft", "no local aircraft (elapsed=" + elapsedSec + "s)");
                return;
            }

            if (sample.Throttle > MechanicsVerifyState.MaxThrottleSeen)
                MechanicsVerifyState.MaxThrottleSeen = sample.Throttle;
            if (sample.CurrentThrust > MechanicsVerifyState.MaxThrustSeen)
                MechanicsVerifyState.MaxThrustSeen = sample.CurrentThrust;

            MechanicsVerifyLogger.Info("sample " + MechanicsVerifyProbe.FormatSample(sample));

            if (sample.DisplayDetail >= 1f)
            {
                MechanicsVerifyState.DisplayDetailPassCount++;
                MechanicsVerifyLogger.Pass("display_detail", "detail=" + sample.DisplayDetail.ToString("F2"));
            }
            else
            {
                MechanicsVerifyState.DisplayDetailFailCount++;
                MechanicsVerifyLogger.Fail("display_detail",
                    "detail=" + sample.DisplayDetail.ToString("F2") + " (<1 breaks engine audio/FX paths)");
            }

            if (sample.Throttle >= MechanicsVerifyState.ThrottleMin)
            {
                MechanicsVerifyState.ThrottleSeenCount++;

                if (!sample.Ignition)
                {
                    MechanicsVerifyLogger.Warn("ignition", "throttle=" + sample.Throttle.ToString("F2") + " but Ignition=false");
                }
                else if (!sample.LocalSim)
                {
                    MechanicsVerifyLogger.Warn("local_sim", "not LocalSim — thrust may be server-side only");
                }
                else if (sample.CurrentThrust >= MechanicsVerifyState.ThrustMin)
                {
                    MechanicsVerifyState.ThrustPassCount++;
                    MechanicsVerifyLogger.Pass("thrust_sim",
                        "thrust=" + sample.CurrentThrust.ToString("F0") + " at thr=" + sample.Throttle.ToString("F2"));
                }
                else if (sample.Speed > 5f && sample.Throttle >= 0.5f)
                {
                    MechanicsVerifyState.ThrustFailCount++;
                    MechanicsVerifyLogger.Fail("thrust_sim",
                        "throttle=" + sample.Throttle.ToString("F2") + " thrust=" + sample.CurrentThrust.ToString("F0")
                        + " speed=" + sample.Speed.ToString("F1") + " — possible broken sim/thrust path");
                }
                else
                {
                    MechanicsVerifyLogger.Warn("thrust_sim",
                        "thrust=" + sample.CurrentThrust.ToString("F0") + " (spool up or low throttle?)");
                }
            }
            else if (sample.Brake >= 0.5f)
            {
                MechanicsVerifyLogger.Info("thrust_sim skipped (brake=" + sample.Brake.ToString("F2") + ", thr<"
                    + MechanicsVerifyState.ThrottleMin.ToString("F2") + ")");
            }
            else
            {
                MechanicsVerifyLogger.Info("thrust_sim skipped (thr<" + MechanicsVerifyState.ThrottleMin.ToString("F2") + ")");
            }
        }

        internal static void LogSummary(ref NOModContext ctx)
        {
            MechanicsVerifyLogger.Phase("Summary");
            int elapsedSec = (MechanicsVerifyState.EnvironmentTickMs() - MechanicsVerifyState.SessionStartMs) / 1000;
            MechanicsVerifyLogger.Info("elapsedSec=" + elapsedSec
                + " slow=" + MechanicsVerifyState.SlowCount
                + " detailPass=" + MechanicsVerifyState.DisplayDetailPassCount
                + " detailFail=" + MechanicsVerifyState.DisplayDetailFailCount
                + " thrustPass=" + MechanicsVerifyState.ThrustPassCount
                + " thrustFail=" + MechanicsVerifyState.ThrustFailCount
                + " maxThr=" + MechanicsVerifyState.MaxThrottleSeen.ToString("F2")
                + " maxThrust=" + MechanicsVerifyState.MaxThrustSeen.ToString("F0"));

            bool ok = MechanicsVerifyState.SlowCount > 0
                && MechanicsVerifyState.DisplayDetailFailCount == 0
                && (MechanicsVerifyState.ThrustPassCount > 0 || MechanicsVerifyState.ThrottleSeenCount == 0);

            if (MechanicsVerifyState.ThrustFailCount > 0)
                ok = false;

            if (ok && MechanicsVerifyState.ThrustPassCount > 0)
                MechanicsVerifyLogger.Pass("summary", "displayDetail OK + thrust observed under throttle");
            else if (MechanicsVerifyState.ThrottleSeenCount == 0)
                MechanicsVerifyLogger.Warn("summary", "no throttle samples — fly with power applied to verify thrust");
            else
                MechanicsVerifyLogger.Fail("summary", "mechanics check incomplete or failed");
        }
    }
}
