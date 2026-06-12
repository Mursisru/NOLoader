using System;
using NOLoader.ModConfig;

namespace NOLoader.PerfTest
{
    internal static class PerfTestState
    {
        internal static int HeavyWork;
        internal static int LogIntervalSec = 5;
        internal static bool ReportOnUnload = true;

        internal static int ModIdHash;
        internal static int SessionStartMs;

        internal static int FastCount;
        internal static int NormalCount;
        internal static int SlowCount;
        internal static int PoolRentCount;
        internal static int PatchHits;
        internal static int LastLoggedPatchHits;
        internal static int LastDemoteLevel;
        internal static int MaxDemoteLevel;

        internal static int LastReportMs;
        internal static bool PatchFirstHitLogged;
        internal static bool OnLoadLogged;

        internal static void Load(ModIniConfig cfg)
        {
            HeavyWork = cfg.GetInt("PerfTest", "HeavyWork", 0);
            LogIntervalSec = cfg.GetInt("PerfTest", "LogIntervalSec", 5);
            if (LogIntervalSec < 1)
                LogIntervalSec = 1;
            ReportOnUnload = cfg.GetInt("PerfTest", "ReportOnUnload", 1) != 0;

            PatchHits = 0;
            LastLoggedPatchHits = 0;
            FastCount = 0;
            NormalCount = 0;
            SlowCount = 0;
            PoolRentCount = 0;
            LastDemoteLevel = 0;
            MaxDemoteLevel = 0;
            LastReportMs = 0;
            PatchFirstHitLogged = false;
            OnLoadLogged = false;
            SessionStartMs = EnvironmentTickMs();
        }

        internal static void Spin(int iterations)
        {
            int x = 0;
            for (int i = 0; i < iterations; i++)
                x ^= i * 31;
            if (x == int.MinValue)
                PatchHits++;
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
