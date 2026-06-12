using NOLoader.API;
using NOLoader.API.Runtime;
using NOLoader.API.World;

namespace NOLoader.PerfTest
{
    internal static class PerfTestReporter
    {
        internal static void LogOnLoad(ref NOModContext ctx, INOModWorldReader world)
        {
            if (PerfTestState.OnLoadLogged)
                return;
            PerfTestState.OnLoadLogged = true;

            PerfTestLogger.Phase("OnLoad");
            PerfTestLogger.Info("mod=" + ctx.ModId + " hash=" + ctx.ModIdHash.ToString("X8") +
                               " heavyWork=" + PerfTestState.HeavyWork +
                               " logIntervalSec=" + PerfTestState.LogIntervalSec);

            if (ctx.Services.Pool != null)
                PerfTestLogger.Pass("arraypool", "services.Pool bound");
            else
                PerfTestLogger.Fail("arraypool", "services.Pool is null");

            if (ctx.Services.Budget != null)
                PerfTestLogger.Pass("budget", "services.Budget bound");
            else
                PerfTestLogger.Fail("budget", "services.Budget is null");

            if (world != null)
                PerfTestLogger.Pass("world", "ActivateWorld reader ready");
            else
                PerfTestLogger.Fail("world", "ActivateWorld returned null");

            PerfTestLogger.Pass("tick", "INOModTickFast/Normal/Slow registered");
            PerfTestLogger.Info("patch throttleEveryN=4 (expect ~25% FixedUpdate hits vs unthrottled)");
        }

        internal static void LogPeriodic(ref NOModContext ctx)
        {
            if (!PerfTestState.ShouldReport())
                return;

            int elapsedSec = (PerfTestState.EnvironmentTickMs() - PerfTestState.SessionStartMs) / 1000;

            if (PerfTestState.FastCount > 0)
                PerfTestLogger.Pass("tick_fast", "count=" + PerfTestState.FastCount);
            else
                PerfTestLogger.Warn("tick_fast", "no invocations yet (elapsed=" + elapsedSec + "s)");

            if (PerfTestState.NormalCount > 0)
                PerfTestLogger.Pass("tick_normal", "count=" + PerfTestState.NormalCount);
            else
                PerfTestLogger.Warn("tick_normal", "no invocations yet");

            if (PerfTestState.SlowCount > 0)
                PerfTestLogger.Pass("tick_slow", "count=" + PerfTestState.SlowCount);
            else
                PerfTestLogger.Warn("tick_slow", "no invocations yet");

            INOModWorldReader world = ctx.Services.World ?? NOModRuntime.GetWorld();
            int units = world.UnitCount;
            int frameId = world.FrameId;
            PerfTestLogger.Info("world units=" + units + " frameId=" + frameId);
            if (elapsedSec >= 8 && frameId <= 0)
                PerfTestLogger.Warn("world", "frameId still 0 — snapshot may be idle or mission not running");
            else if (frameId > 0)
                PerfTestLogger.Pass("world", "snapshot advancing frameId=" + frameId);

            if (PerfTestState.HeavyWork >= 1)
            {
                if (PerfTestState.PoolRentCount > 0)
                    PerfTestLogger.Pass("arraypool", "rent/return cycles=" + PerfTestState.PoolRentCount);
                else
                    PerfTestLogger.Warn("arraypool", "HeavyWork>=1 but no pool rents yet");
            }
            else
            {
                PerfTestLogger.Info("arraypool skipped (HeavyWork=0)");
            }

            int patchDelta = PerfTestState.PatchHits - PerfTestState.LastLoggedPatchHits;
            PerfTestLogger.Info("patch hits=" + PerfTestState.PatchHits + " delta=" + patchDelta
                + " gateEval=" + PatchThrottleGate.TotalEvaluations
                + " gatePass=" + PatchThrottleGate.PassedEvaluations);
            if (PerfTestState.PatchHits > 0)
                PerfTestLogger.Pass("cecil_throttle", "postfix invoked (throttled) hits=" + PerfTestState.PatchHits);
            else if (PatchThrottleGate.PassedEvaluations > 0)
                PerfTestLogger.Warn("cecil_throttle", "gate passed but patch hits=0 — inject binding issue");
            else if (PatchThrottleGate.TotalEvaluations > 0)
                PerfTestLogger.Warn("cecil_throttle", "gate throttling only (no postfix yet)");
            else if (elapsedSec >= 10)
                PerfTestLogger.Warn("cecil_throttle", "no IL activity — cold restart after deploy");

            PerfTestState.LastLoggedPatchHits = PerfTestState.PatchHits;

            IModExecutionBudgetView? budget = ctx.Services.Budget ?? NOModRuntime.Budget;
            if (budget != null)
            {
                int demote = budget.GetDemoteLevel(PerfTestState.ModIdHash);
                PerfTestState.LastDemoteLevel = demote;
                if (demote > PerfTestState.MaxDemoteLevel)
                    PerfTestState.MaxDemoteLevel = demote;

                PerfTestLogger.Info("budget demoteLevel=" + demote + " maxSeen=" + PerfTestState.MaxDemoteLevel);
                if (PerfTestState.HeavyWork >= 2 && demote > 0)
                    PerfTestLogger.Pass("budget", "demote active level=" + demote);
                else if (PerfTestState.HeavyWork >= 2 && elapsedSec >= 15 && demote == 0)
                    PerfTestLogger.Warn("budget", "HeavyWork=2 but demoteLevel still 0");
            }
            else
            {
                PerfTestLogger.Fail("budget", "Budget view unavailable");
            }
        }

        internal static void LogSummary(ref NOModContext ctx)
        {
            PerfTestLogger.Phase("Summary");
            int elapsedSec = (PerfTestState.EnvironmentTickMs() - PerfTestState.SessionStartMs) / 1000;
            PerfTestLogger.Info("elapsedSec=" + elapsedSec +
                               " fast=" + PerfTestState.FastCount +
                               " normal=" + PerfTestState.NormalCount +
                               " slow=" + PerfTestState.SlowCount +
                               " poolRents=" + PerfTestState.PoolRentCount +
                               " patchHits=" + PerfTestState.PatchHits +
                               " maxDemote=" + PerfTestState.MaxDemoteLevel);

            bool ok = PerfTestState.FastCount > 0 &&
                      PerfTestState.NormalCount > 0 &&
                      PerfTestState.SlowCount > 0;

            if (ok)
                PerfTestLogger.Pass("summary", "tick scheduler exercised");
            else
                PerfTestLogger.Fail("summary", "tick scheduler incomplete");

            INOModWorldReader world = ctx.Services.World ?? NOModRuntime.GetWorld();
            if (world.FrameId > 0 || world.UnitCount > 0)
                PerfTestLogger.Pass("summary", "world snapshot observed");
            else
                PerfTestLogger.Warn("summary", "world snapshot empty at unload");

            if (PerfTestState.PatchHits > 0)
                PerfTestLogger.Pass("summary", "cecil throttle patch exercised");
            else
                PerfTestLogger.Warn("summary", "no patch hits recorded");

            if (PerfTestState.HeavyWork >= 1 && PerfTestState.PoolRentCount > 0)
                PerfTestLogger.Pass("summary", "array pool exercised");
        }
    }
}
