using System;
using NOLoader.ModConfig;

namespace NOLoader.MechanicsVerify
{
    internal static class MechanicsVerifyState
    {
        internal static int LogIntervalSec = 5;
        internal static float ThrottleMin = 0.25f;
        internal static float ThrustMin = 50f;
        internal static bool ReportOnUnload = true;

        internal static int SessionStartMs;
        internal static int LastReportMs;
        internal static bool OnLoadLogged;

        internal static int SlowCount;
        internal static int DisplayDetailPassCount;
        internal static int DisplayDetailFailCount;
        internal static int ThrustPassCount;
        internal static int ThrustFailCount;
        internal static int ThrottleSeenCount;
        internal static float MaxThrottleSeen;
        internal static float MaxThrustSeen;
        internal static float LastTickThrottle = -1f;

        internal static void Load(ModIniConfig cfg)
        {
            LogIntervalSec = cfg.GetInt("MechanicsVerify", "LogIntervalSec", 5);
            if (LogIntervalSec < 1)
                LogIntervalSec = 1;

            ThrottleMin = cfg.GetFloat("MechanicsVerify", "ThrottleMin", 0.25f);
            ThrustMin = cfg.GetFloat("MechanicsVerify", "ThrustMin", 50f);
            ReportOnUnload = cfg.GetInt("MechanicsVerify", "ReportOnUnload", 1) != 0;

            SessionStartMs = EnvironmentTickMs();
            LastReportMs = 0;
            OnLoadLogged = false;
            SlowCount = 0;
            DisplayDetailPassCount = 0;
            DisplayDetailFailCount = 0;
            ThrustPassCount = 0;
            ThrustFailCount = 0;
            ThrottleSeenCount = 0;
            MaxThrottleSeen = 0f;
            MaxThrustSeen = 0f;
            LastTickThrottle = -1f;
        }

        internal static bool ShouldReport()
        {
            int now = EnvironmentTickMs();
            if (LastReportMs == 0)
            {
                LastReportMs = now;
                return false;
            }

            if (now - LastReportMs < LogIntervalSec * 1000)
                return false;

            LastReportMs = now;
            return true;
        }

        internal static int EnvironmentTickMs() => Environment.TickCount & int.MaxValue;
    }
}
