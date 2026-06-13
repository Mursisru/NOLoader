using System;
using NOLoader.API;

namespace NOLoader.GpuRenderVerify
{
    internal static class GpuRenderVerifyReporter
    {
        internal static void LogOnLoad(ref NOModContext ctx)
        {
            if (GpuRenderVerifyState.OnLoadLogged)
                return;

            GpuRenderVerifyState.OnLoadLogged = true;
            GpuRenderVerifyLogger.Phase("OnLoad");
            GpuRenderVerifyLogger.Info("mod=" + ctx.ModId + " DEV2O13 field test (GpuRender + sim sanity)");
            GpuRenderVerifyLogger.Pass("manifest", "no Cecil patches");
            GpuRenderVerifyLogger.Info(GpuRenderVerifyProbe.FormatGpuRuntime());
            GpuRenderVerifyLogger.Info("ini: gpu_render=1 gpu_metrics=1; optional gpu_hud_pass=1 gpu_fx_instancing=1 canvas_limiter=1");
            GpuRenderVerifyLogger.Info("fly mission 30s+; parse: scripts\\RDYTU\\parse-gpurender-verify-ringlog.ps1");
        }

        internal static void LogPeriodic(ref NOModContext ctx)
        {
            int elapsedSec = (Environment.TickCount - GpuRenderVerifyState.SessionStartMs) / 1000;

            int markers = GpuRenderVerifyProbe.CountCombatHudMarkers();
            if (markers > GpuRenderVerifyState.MaxMarkersSeen)
                GpuRenderVerifyState.MaxMarkersSeen = markers;

            GpuRenderVerifyLogger.Info("elapsed=" + elapsedSec + "s markers=" + markers
                + " gpuDispatch=" + GpuRenderVerifyState.GpuDispatchCount);

            if (markers >= 0)
                GpuRenderVerifyLogger.Pass("hud_markers", "visible=" + markers);
            else
                GpuRenderVerifyLogger.Warn("hud_markers", "CombatHUD not ready");

            if (GpuRenderVerifyState.GpuDispatchCount > 0)
            {
                if (GpuRenderVerifyState.GpuDispatchCount > GpuRenderVerifyState.MaxGpuDispatchSeen)
                    GpuRenderVerifyState.MaxGpuDispatchSeen = GpuRenderVerifyState.GpuDispatchCount;
                GpuRenderVerifyLogger.Pass("gpu_compute", "dispatches=" + GpuRenderVerifyState.GpuDispatchCount);
            }
            else if (elapsedSec >= 20)
            {
                GpuRenderVerifyLogger.Warn("gpu_compute",
                    "dispatches=0 — set gpu_render=1 (GpuHudPass camera hook dispatches INOModGpuCompute)");
            }

            if (GpuRenderVerifyProbe.TrySampleLocalAircraft(out MechanicsSample sample))
            {
                GpuRenderVerifyLogger.Info("mechanics " + GpuRenderVerifyProbe.FormatMechanics(in sample));

                if (sample.DisplayDetail >= 1f)
                    GpuRenderVerifyLogger.Pass("display_detail", "detail=" + sample.DisplayDetail.ToString("F2"));
                else
                {
                    GpuRenderVerifyState.MechanicsFailCount++;
                    GpuRenderVerifyLogger.Fail("display_detail", "detail=" + sample.DisplayDetail.ToString("F2"));
                }

                if (sample.Throttle >= GpuRenderVerifyState.ThrottleMin && sample.Ignition && sample.LocalSim)
                {
                    if (sample.CurrentThrust >= GpuRenderVerifyState.ThrustMin)
                    {
                        GpuRenderVerifyState.MechanicsPassCount++;
                        GpuRenderVerifyLogger.Pass("thrust_sim",
                            "thrust=" + sample.CurrentThrust.ToString("F0") + " thr=" + sample.Throttle.ToString("F2"));
                    }
                    else if (sample.Speed > 5f)
                    {
                        GpuRenderVerifyState.MechanicsFailCount++;
                        GpuRenderVerifyLogger.Fail("thrust_sim",
                            "thrust=" + sample.CurrentThrust.ToString("F0") + " at thr=" + sample.Throttle.ToString("F2"));
                    }
                }
            }
            else
            {
                GpuRenderVerifyLogger.Warn("mechanics", "no local aircraft");
            }

            GpuRenderVerifyLogger.Pass("ring_log", "grep [GpuRender] threadingMode= gpu device=");
        }

        internal static void LogSummary(ref NOModContext ctx)
        {
            GpuRenderVerifyLogger.Phase("Unload");
            GpuRenderVerifyLogger.Info("maxMarkers=" + GpuRenderVerifyState.MaxMarkersSeen
                + " maxGpuDispatch=" + GpuRenderVerifyState.MaxGpuDispatchSeen
                + " mechanicsPass=" + GpuRenderVerifyState.MechanicsPassCount
                + " mechanicsFail=" + GpuRenderVerifyState.MechanicsFailCount);
        }
    }
}
