using System;
using NOLoader.ModConfig;

namespace NOLoader.GpuRenderVerify
{
    internal static class GpuRenderVerifyState
    {
        internal static bool OnLoadLogged;
        internal static int SlowCount;
        internal static int SessionStartMs;
        internal static int LastReportMs;
        internal static int ReportIntervalSec = 30;

        internal static int MaxMarkersSeen;
        internal static int GpuDispatchCount;
        internal static int MaxGpuDispatchSeen;

        internal static float ThrottleMin = 0.25f;
        internal static float ThrustMin = 500f;
        internal static int MechanicsPassCount;
        internal static int MechanicsFailCount;

        internal static void Load(ModIniConfig cfg)
        {
            ReportIntervalSec = cfg.GetInt("GpuRenderVerify", "report_interval_sec", 30);
            if (ReportIntervalSec < 10)
                ReportIntervalSec = 10;

            ThrottleMin = cfg.GetFloat("GpuRenderVerify", "throttle_min", 0.25f);
            ThrustMin = cfg.GetFloat("GpuRenderVerify", "thrust_min", 500f);

            SessionStartMs = EnvironmentTickMs();
            LastReportMs = 0;
            OnLoadLogged = false;
            SlowCount = 0;
            MaxMarkersSeen = 0;
            GpuDispatchCount = 0;
            MaxGpuDispatchSeen = 0;
            MechanicsPassCount = 0;
            MechanicsFailCount = 0;
        }

        internal static bool ShouldReport()
        {
            int now = EnvironmentTickMs();
            if (LastReportMs == 0)
            {
                LastReportMs = now;
                return false;
            }

            if (now - LastReportMs < ReportIntervalSec * 1000)
                return false;

            LastReportMs = now;
            return true;
        }

        internal static int EnvironmentTickMs() => Environment.TickCount & int.MaxValue;
    }
}
