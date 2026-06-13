using System;
using NOLoader.API.World;

namespace NOLoader.API
{
    /// <summary>Static perf/runtime facade — RDYTU Core binds implementations at bootstrap.</summary>
    public static class NOModRuntime
    {
        public static INOModArrayPool Pool { get; internal set; } = NOModArrayPoolStub.Instance;

        public static INOModWorldReader EmptyWorld { get; } = new EmptyWorldReader();

        public static INOModWorldReader? World { get; internal set; }

        /// <summary>Immutable published world copy for worker-side reads (CoreBalancer double-buffer).</summary>
        public static INOModWorldReader? StableWorld { get; internal set; }

        public static INOModFrameCache? FrameCache { get; internal set; }

        public static IModExecutionBudgetView? Budget { get; internal set; }

        public static INOModComputeScheduler Scheduler { get; internal set; } = NOModComputeSchedulerStub.Instance;

        internal static Func<INOModWorldReader>? ActivateWorldCallback { get; set; }

        public static INOModWorldReader GetWorld()
        {
            return World ?? EmptyWorld;
        }

        public static INOModWorldReader ActivateWorld()
        {
            if (World != null)
                return World;
            return ActivateWorldCallback?.Invoke() ?? EmptyWorld;
        }

        private sealed class EmptyWorldReader : INOModWorldReader
        {
            public int FrameId => 0;
            public int UnitCount => 0;
            public NOWorldUnit GetUnit(int index) => default;
        }
    }

    internal sealed class NOModArrayPoolStub : INOModArrayPool
    {
        public static readonly NOModArrayPoolStub Instance = new NOModArrayPoolStub();

        public int[] RentInt(int length) => new int[length];

        public float[] RentFloat(int length) => new float[length];

        public void Return(int[] array) { }

        public void Return(float[] array) { }
    }
}
