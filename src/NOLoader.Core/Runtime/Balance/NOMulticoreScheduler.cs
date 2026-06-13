using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using NOLoader.API;
using NOLoader.Core;
using NOLoader.Core.Logging;

namespace NOLoader.Core.Runtime.Balance
{
    internal sealed class NOMulticoreScheduler : INOModComputeScheduler
    {
        public static readonly NOMulticoreScheduler Instance = new NOMulticoreScheduler();

        private readonly BlockingCollection<WorkItem> _queue = new BlockingCollection<WorkItem>();
        private Thread[] _workers = Array.Empty<Thread>();
        private int _started;
        private ulong _workerAffinityMask;
        private long _droppedJobs;
        private const int MaxQueueDepth = 256;

        public bool IsAvailable => _started != 0;

        public int QueueDepth => _queue.Count;

        internal void Start(int workerCount, ulong workerAffinityMask)
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                return;

            _workerAffinityMask = workerAffinityMask;
            if (workerCount < 1)
                workerCount = 1;
            if (workerCount > 8)
                workerCount = 8;

            _workers = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                int workerId = i;
                var thread = new Thread(() => WorkerLoop(workerId))
                {
                    IsBackground = true,
                    Name = "NOLoader.ModWorker." + workerId,
                    Priority = ThreadPriority.BelowNormal
                };
                _workers[i] = thread;
                thread.Start();
            }

            RingBufferLog.WriteAscii("[CoreBalancer] workers=" + workerCount
                + " affinity=0x" + workerAffinityMask.ToString("X", System.Globalization.CultureInfo.InvariantCulture));
        }

        internal void Shutdown()
        {
            if (Interlocked.CompareExchange(ref _started, 0, 1) != 1)
                return;

            _queue.CompleteAdding();
            for (int i = 0; i < _workers.Length; i++)
            {
                try
                {
                    _workers[i].Join(500);
                }
                catch
                {
                    // ignore
                }
            }

            _workers = Array.Empty<Thread>();
        }

        public void RunCompute(Action compute, Action applyOnMain)
        {
            if (_started == 0)
            {
                compute();
                UnityMainThread.Post(applyOnMain);
                return;
            }

            if (_queue.Count >= MaxQueueDepth)
            {
                Interlocked.Increment(ref _droppedJobs);
                RingBufferLog.WriteAscii("[CoreBalancer] queue full — dropped job");
                return;
            }

            _queue.Add(new WorkItem(compute, applyOnMain));
        }

        internal bool TryEnqueueBackground(Action compute, Action applyOnMain)
        {
            if (_started == 0)
                return false;

            if (_queue.Count >= MaxQueueDepth)
            {
                Interlocked.Increment(ref _droppedJobs);
                return false;
            }

            _queue.Add(new WorkItem(compute, applyOnMain));
            return true;
        }

        internal long DroppedJobs => Interlocked.Read(ref _droppedJobs);

        private void WorkerLoop(int workerId)
        {
            ThreadGuard.MarkWorkerThread();
            if (_workerAffinityMask != 0)
                ThreadAffinityHelper.TryPinCurrentThread(_workerAffinityMask);

            foreach (WorkItem item in _queue.GetConsumingEnumerable())
            {
                long start = Stopwatch.GetTimestamp();
                bool computeOk = false;
                try
                {
                    ThreadGuard.AssertWorker();
                    item.Compute();
                    computeOk = true;
                }
                catch (Exception ex)
                {
                    RingBufferLog.WriteAscii("[CoreBalancer] worker error: " + ex.GetType().Name);
                }

                double workerMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                if (computeOk)
                {
                    UnityMainThread.Post(() =>
                    {
                        try
                        {
                            item.Apply();
                        }
                        catch (Exception ex)
                        {
                            RingBufferLog.WriteAscii("[CoreBalancer] apply error: " + ex.GetType().Name);
                        }
                    });
                }

                if (workerMs > 2.0)
                {
                    RingBufferLog.WriteAscii("[CoreBalancer] workerMs="
                        + workerMs.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                        + " worker=" + workerId);
                }
            }

            ThreadGuard.ClearWorkerThread();
        }

        private readonly struct WorkItem
        {
            internal WorkItem(Action compute, Action apply)
            {
                Compute = compute;
                Apply = apply;
            }

            internal Action Compute { get; }
            internal Action Apply { get; }
        }
    }
}
