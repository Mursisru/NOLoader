using System;
using NOLoader.Core;

namespace NOLoader.Core.Runtime.Balance
{
    internal static class ThreadGuard
    {
        [ThreadStatic]
        private static bool _workerThread;

        internal static void MarkWorkerThread() => _workerThread = true;

        internal static void ClearWorkerThread() => _workerThread = false;

        internal static bool IsWorkerThread => _workerThread;

        internal static void AssertWorker()
        {
            if (!_workerThread)
                throw new InvalidOperationException("CoreBalancer worker-only path invoked on non-worker thread");
        }

        internal static void AssertMainThread()
        {
            if (_workerThread)
                throw new InvalidOperationException("Unity/main-only path invoked on CoreBalancer worker thread");

            if (!UnityMainThread.IsMainThread)
                throw new InvalidOperationException("Unity main thread required");
        }
    }
}
