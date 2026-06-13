using System.Globalization;
using NOLoader.API;
using NOLoader.API.World;

namespace NOLoader.CoreBalancerVerify
{
    internal static class CoreBalancerVerifyReporter
    {
        internal static void LogOnLoad(ref NOModContext ctx)
        {
            if (CoreBalancerVerifyState.OnLoadLogged)
                return;

            CoreBalancerVerifyState.OnLoadLogged = true;
            CoreBalancerVerifyLogger.Phase("OnLoad");
            CoreBalancerVerifyLogger.Info("mod=" + ctx.ModId + " hash=" + ctx.ModIdHash.ToString("X8")
                + " patches=0 (CoreBalancer field test DEV2O8+)");
            CoreBalancerVerifyLogger.Pass("manifest", "no Cecil patches");

            INOModWorldReader world = NOModRuntime.ActivateWorld();
            CoreBalancerVerifyLogger.Info("world activated frame=" + world.FrameId
                + " units=" + world.UnitCount);

            SchedulerSample sched = CoreBalancerVerifyProbe.SampleScheduler();
            CoreBalancerVerifyLogger.Info(CoreBalancerVerifyProbe.FormatScheduler(in sched));

            if (sched.Available)
            {
                CoreBalancerVerifyState.SchedulerPassCount++;
                CoreBalancerVerifyLogger.Pass("scheduler", "workers online queue=" + sched.QueueDepth);
            }
            else
            {
                CoreBalancerVerifyState.SchedulerWarnCount++;
                CoreBalancerVerifyLogger.Warn("scheduler",
                    "IsAvailable=false — set core_balancer=1 in noloader_config.ini for worker pipeline test");
            }

            StableWorldSample stable = CoreBalancerVerifyProbe.SampleStableWorld();
            CoreBalancerVerifyLogger.Info(CoreBalancerVerifyProbe.FormatStableWorld(in stable));
            if (stable.HasReader)
                CoreBalancerVerifyLogger.Pass("stable_world", "reader bound on main thread");
            else
                CoreBalancerVerifyLogger.Warn("stable_world", "StableWorld null until double_buffer publishes");

            CoreBalancerVerifyLogger.Info("fly mission 30s+ with core_balancer=1; expect pipeline + worker stableWorld PASS");
        }

        internal static void LogPeriodic(ref NOModContext ctx)
        {
            int elapsedSec = (CoreBalancerVerifyState.EnvironmentTickMs() - CoreBalancerVerifyState.SessionStartMs) / 1000;

            SchedulerSample sched = CoreBalancerVerifyProbe.SampleScheduler();
            if (sched.QueueDepth > CoreBalancerVerifyState.MaxSchedulerQueue)
                CoreBalancerVerifyState.MaxSchedulerQueue = sched.QueueDepth;

            CoreBalancerVerifyLogger.Info("elapsed=" + elapsedSec + "s "
                + CoreBalancerVerifyProbe.FormatScheduler(in sched)
                + " compute=" + CoreBalancerVerifyState.ComputeRuns
                + " apply=" + CoreBalancerVerifyState.ApplyRuns
                + " runComputeApplied=" + CoreBalancerVerifyState.RunComputeApplied);

            if (sched.Available && CoreBalancerVerifyState.ComputeRuns > 0 && CoreBalancerVerifyState.ApplyRuns > 0)
            {
                if (CoreBalancerVerifyState.ComputeRuns == CoreBalancerVerifyState.ApplyRuns)
                {
                    CoreBalancerVerifyState.PipelinePassCount++;
                    CoreBalancerVerifyLogger.Pass("pipeline",
                        "background compute/apply matched runs=" + CoreBalancerVerifyState.ComputeRuns);
                }
                else
                {
                    CoreBalancerVerifyState.PipelineFailCount++;
                    CoreBalancerVerifyLogger.Fail("pipeline",
                        "compute=" + CoreBalancerVerifyState.ComputeRuns + " apply=" + CoreBalancerVerifyState.ApplyRuns);
                }
            }
            else if (sched.Available && elapsedSec >= 10)
            {
                CoreBalancerVerifyLogger.Warn("pipeline",
                    "no background runs yet (need mission tick + core_balancer=1)");
            }

            if (CoreBalancerVerifyState.RunComputeApplied > 0)
            {
                CoreBalancerVerifyLogger.Pass("run_compute",
                    "apply callback ok result=" + CoreBalancerVerifyState.LastRunComputeResult.ToString("F3", CultureInfo.InvariantCulture));
            }

            StableWorldSample stable = CoreBalancerVerifyProbe.SampleStableWorld();
            if (stable.HasReader)
            {
                CoreBalancerVerifyState.LastStableFrameId = stable.FrameId;
                CoreBalancerVerifyState.LastStableUnitCount = stable.UnitCount;
                CoreBalancerVerifyLogger.Pass("stable_world_main",
                    "frame=" + stable.FrameId + " units=" + stable.UnitCount);
            }
        }

        internal static void LogSummary(ref NOModContext ctx)
        {
            CoreBalancerVerifyLogger.Phase("Summary");
            int elapsedSec = (CoreBalancerVerifyState.EnvironmentTickMs() - CoreBalancerVerifyState.SessionStartMs) / 1000;
            SchedulerSample sched = CoreBalancerVerifyProbe.SampleScheduler();

            CoreBalancerVerifyLogger.Info("elapsedSec=" + elapsedSec
                + " slow=" + CoreBalancerVerifyState.SlowCount
                + " schedulerAvail=" + sched.Available
                + " maxQueue=" + CoreBalancerVerifyState.MaxSchedulerQueue
                + " compute=" + CoreBalancerVerifyState.ComputeRuns
                + " apply=" + CoreBalancerVerifyState.ApplyRuns
                + " runComputeApplied=" + CoreBalancerVerifyState.RunComputeApplied
                + " stableWorkerPass=" + CoreBalancerVerifyState.StableWorldPassCount
                + " stableWorkerFail=" + CoreBalancerVerifyState.StableWorldFailCount
                + " lastStableFrame=" + CoreBalancerVerifyState.LastStableFrameId
                + " lastStableUnits=" + CoreBalancerVerifyState.LastStableUnitCount);

            bool ok = CoreBalancerVerifyState.SlowCount > 0;

            if (sched.Available)
            {
                ok = ok && CoreBalancerVerifyState.ComputeRuns > 0
                    && CoreBalancerVerifyState.ApplyRuns > 0
                    && CoreBalancerVerifyState.PipelineFailCount == 0
                    && CoreBalancerVerifyState.StableWorldPassCount > 0;
            }
            else
            {
                ok = ok && CoreBalancerVerifyState.RunComputeApplied > 0;
            }

            if (ok && sched.Available)
                CoreBalancerVerifyLogger.Pass("summary", "worker pipeline + stableWorld on worker OK");
            else if (ok && !sched.Available)
                CoreBalancerVerifyLogger.Warn("summary",
                    "inline RunCompute OK — enable core_balancer=1 and re-fly for full worker test");
            else
                CoreBalancerVerifyLogger.Fail("summary", "CoreBalancer verify incomplete");
        }
    }
}
