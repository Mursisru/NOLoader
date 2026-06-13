using System.Globalization;
using NOLoader.API;
using NOLoader.API.World;

namespace NOLoader.CoreBalancerVerify
{
    internal struct SchedulerSample
    {
        internal bool Available;
        internal int QueueDepth;
    }

    internal struct StableWorldSample
    {
        internal bool HasReader;
        internal int FrameId;
        internal int UnitCount;
    }

    internal static class CoreBalancerVerifyProbe
    {
        internal static SchedulerSample SampleScheduler()
        {
            return new SchedulerSample
            {
                Available = NOModRuntime.Scheduler.IsAvailable,
                QueueDepth = NOModRuntime.Scheduler.QueueDepth
            };
        }

        internal static StableWorldSample SampleStableWorld()
        {
            INOModWorldReader? stable = NOModRuntime.StableWorld;
            if (stable == null)
            {
                return new StableWorldSample { HasReader = false };
            }

            return new StableWorldSample
            {
                HasReader = true,
                FrameId = stable.FrameId,
                UnitCount = stable.UnitCount
            };
        }

        internal static float RunDeterministicMath(int iterations)
        {
            float acc = 0f;
            for (int i = 1; i <= iterations; i++)
                acc += (i % 97) * 0.001f;
            return acc;
        }

        internal static string FormatScheduler(in SchedulerSample sample)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "scheduler avail={0} queue={1}",
                sample.Available,
                sample.QueueDepth);
        }

        internal static string FormatStableWorld(in StableWorldSample sample)
        {
            if (!sample.HasReader)
                return "stableWorld=missing";

            return string.Format(CultureInfo.InvariantCulture,
                "stableWorld frame={0} units={1}",
                sample.FrameId,
                sample.UnitCount);
        }
    }
}
