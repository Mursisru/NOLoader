using System;
using NOLoader.ModConfig;

namespace NOLoader.CoreBalancerVerify
{
    internal static class CoreBalancerVerifyState
    {
        internal static int LogIntervalSec = 5;
        internal static int MathIterations = 12000;
        internal static bool ReportOnUnload = true;

        internal static int SessionStartMs;
        internal static int LastReportMs;
        internal static bool OnLoadLogged;

        internal static int SlowCount;
        internal static int ComputeRuns;
        internal static int ApplyRuns;
        internal static int RunComputeRequested;
        internal static int RunComputeApplied;
        internal static float LastComputeResult;
        internal static float LastRunComputeResult;

        internal static int StableWorldPassCount;
        internal static int StableWorldFailCount;
        internal static int SchedulerPassCount;
        internal static int SchedulerWarnCount;
        internal static int PipelinePassCount;
        internal static int PipelineFailCount;

        internal static int LastStableFrameId;
        internal static int LastStableUnitCount;
        internal static int MaxSchedulerQueue;

        internal static bool RunComputeProbeSent;

        internal static void Load(ModIniConfig cfg)
        {
            LogIntervalSec = cfg.GetInt("CoreBalancerVerify", "LogIntervalSec", 5);
            if (LogIntervalSec < 1)
                LogIntervalSec = 1;

            MathIterations = cfg.GetInt("CoreBalancerVerify", "MathIterations", 12000);
            if (MathIterations < 1000)
                MathIterations = 1000;

            ReportOnUnload = cfg.GetInt("CoreBalancerVerify", "ReportOnUnload", 1) != 0;

            SessionStartMs = EnvironmentTickMs();
            LastReportMs = 0;
            OnLoadLogged = false;
            ResetCounters();
        }

        internal static void ResetCounters()
        {
            SlowCount = 0;
            ComputeRuns = 0;
            ApplyRuns = 0;
            RunComputeRequested = 0;
            RunComputeApplied = 0;
            LastComputeResult = 0f;
            LastRunComputeResult = 0f;
            StableWorldPassCount = 0;
            StableWorldFailCount = 0;
            SchedulerPassCount = 0;
            SchedulerWarnCount = 0;
            PipelinePassCount = 0;
            PipelineFailCount = 0;
            LastStableFrameId = 0;
            LastStableUnitCount = 0;
            MaxSchedulerQueue = 0;
            RunComputeProbeSent = false;
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
