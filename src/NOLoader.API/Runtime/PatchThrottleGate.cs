using System.Threading;

namespace NOLoader.API.Runtime
{
    /// <summary>IL-injected gate for RDYTU Cecil patch throttling (called from patched game methods).</summary>
    public static class PatchThrottleGate
    {
        private static int[] _counters = new int[0];
        private static readonly object GateLock = new object();

        /// <summary>Diagnostics for mod IL throttle verification (RDYTU).</summary>
        public static long TotalEvaluations;

        /// <summary>Diagnostics: ShouldRun returned true.</summary>
        public static long PassedEvaluations;

        public static bool ShouldRun(int patchSlotId, int everyN)
        {
            System.Threading.Interlocked.Increment(ref TotalEvaluations);
            if (everyN <= 1)
            {
                System.Threading.Interlocked.Increment(ref PassedEvaluations);
                return true;
            }
            if (patchSlotId < 0)
            {
                System.Threading.Interlocked.Increment(ref PassedEvaluations);
                return true;
            }

            EnsureCapacity(patchSlotId + 1);
            int count = System.Threading.Interlocked.Increment(ref _counters[patchSlotId]);
            if (count % everyN == 0)
            {
                System.Threading.Interlocked.Increment(ref PassedEvaluations);
                return true;
            }

            return false;
        }

        private static void EnsureCapacity(int size)
        {
            if (_counters.Length >= size)
                return;

            lock (GateLock)
            {
                if (_counters.Length >= size)
                    return;

                int newLen = size < 16 ? 16 : size;
                var next = new int[newLen];
                if (_counters.Length > 0)
                    System.Array.Copy(_counters, next, _counters.Length);
                _counters = next;
            }
        }
    }
}
