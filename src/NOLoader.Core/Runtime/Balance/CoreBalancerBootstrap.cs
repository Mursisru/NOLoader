#if !NOLoader_DEV
using System;
using NOLoader.API;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;

namespace NOLoader.Core.Runtime.Balance
{
    internal static class CoreBalancerBootstrap
    {
        private static CoreTopology? _topology;
        private static bool _initialized;

        internal static CoreTopology? Topology => _topology;

        internal static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (!RuntimeConfig.CoreBalancerEnabled)
            {
                NOModRuntime.Scheduler = NOModComputeSchedulerStub.Instance;
                RingBufferLog.WriteAscii("[CoreBalancer] disabled (core_balancer=0)");
                return;
            }

            int workers = RuntimeConfig.ModWorkerCount;
            if (workers <= 0)
                workers = Math.Max(1, Environment.ProcessorCount / 4);
            if (workers > 8)
                workers = 8;

            _topology = CoreTopology.Detect(workers);

            ulong modMask = _topology.ParseMask(RuntimeConfig.ModAffinityMask, _topology.ModWorkerAffinityMask);
            RingBufferLog.WriteAscii("[CoreBalancer] topology logical=" + _topology.LogicalCount
                + " physical=" + _topology.PhysicalCount
                + " hybrid=" + _topology.IsHybrid
                + " pCores=" + _topology.PerformanceCoreCount
                + " eCores=" + _topology.EfficientCoreCount);

            NOMulticoreScheduler.Instance.Start(workers, modMask);
            NOModRuntime.Scheduler = NOMulticoreScheduler.Instance;

            ApplyMainThreadAffinity();
            PinNOLoaderBackgroundThreads();
        }

        internal static void PinThreadForBackgroundWork()
        {
            if (_topology == null || !RuntimeConfig.CoreBalancerEnabled)
                return;

            ThreadAffinityHelper.TryPinCurrentThread(_topology.BackgroundAffinityMask);
        }

        private static void ApplyMainThreadAffinity()
        {
            if (_topology == null)
                return;

            string mode = RuntimeConfig.MainThreadAffinity;
            if (string.Equals(mode, "0", System.StringComparison.Ordinal)
                || string.Equals(mode, "false", System.StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(mode))
                return;

            ulong mask = _topology.MainThreadAffinityMask;
            if (!string.Equals(mode, "auto", System.StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mode, "1", System.StringComparison.Ordinal)
                && !string.Equals(mode, "true", System.StringComparison.OrdinalIgnoreCase))
            {
                mask = _topology.ParseMask(mode, mask);
            }

            UnityMainThread.Post(() =>
            {
                if (ThreadAffinityHelper.TryPinCurrentThread(mask))
                {
                    RingBufferLog.WriteAscii("[CoreBalancer] main affinity=0x"
                        + mask.ToString("X", System.Globalization.CultureInfo.InvariantCulture));
                }
            });
        }

        private static void PinNOLoaderBackgroundThreads()
        {
            // Log flush / unity-wait threads call PinThreadForBackgroundWork at thread entry when enabled.
        }
    }
}
#endif
