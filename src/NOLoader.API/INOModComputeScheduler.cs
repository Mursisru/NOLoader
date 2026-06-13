using System;

namespace NOLoader.API
{
    /// <summary>Enqueue pure compute on CoreBalancer workers; apply runs on Unity main thread.</summary>
    public interface INOModComputeScheduler
    {
        bool IsAvailable { get; }

        /// <summary>Non-blocking: compute on worker, apply on next main-thread dispatch.</summary>
        void RunCompute(Action compute, Action applyOnMain);

        /// <summary>Pending jobs in queue (diagnostics).</summary>
        int QueueDepth { get; }
    }

    internal sealed class NOModComputeSchedulerStub : INOModComputeScheduler
    {
        public static readonly NOModComputeSchedulerStub Instance = new NOModComputeSchedulerStub();

        public bool IsAvailable => false;

        public int QueueDepth => 0;

        public void RunCompute(Action compute, Action applyOnMain)
        {
            compute();
            applyOnMain();
        }
    }
}
