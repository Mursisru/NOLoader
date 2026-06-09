using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace NOLoader.Core
{
    internal static class UnityMainThread
    {
        private static SynchronizationContext? _context;
        private static int _mainThreadId;

        public static void RegisterBootstrapThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _context = SynchronizationContext.Current ?? ResolveUnityContext();
        }

        public static bool TryEnsureReady()
        {
            if (_mainThreadId != 0 && _context != null)
                return true;

            _context ??= ResolveUnityContext();
            if (_context == null)
                return false;

            if (_mainThreadId == 0)
                _mainThreadId = CaptureMainThreadId();
            return _mainThreadId != 0;
        }

        public static void EnsureReady()
        {
            for (int i = 0; i < 1200; i++)
            {
                if (TryEnsureReady())
                    return;
                Thread.Sleep(50);
            }
        }

        public static bool IsMainThread =>
            _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public static void Invoke(Action action)
        {
            if (IsMainThread)
            {
                action();
                return;
            }

            WaitForContext();
            Exception? captured = null;
            using var wait = new ManualResetEventSlim(false);
            _context!.Post(_ =>
            {
                try { action(); }
                catch (Exception ex) { captured = ex; }
                finally { wait.Set(); }
            }, null);

            if (!wait.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Unity main thread dispatch timed out");
            if (captured != null)
                throw captured;
        }

        public static void Post(Action action)
        {
            if (IsMainThread)
            {
                action();
                return;
            }

            WaitForContext();
            _context!.Post(_ => action(), null);
        }

        private static void WaitForContext()
        {
            EnsureReady();
            if (_context == null)
                throw new InvalidOperationException("Unity main thread is not available");
        }

        private static SynchronizationContext? ResolveUnityContext()
        {
            if (SynchronizationContext.Current is SynchronizationContext current)
                return current;

            Assembly unity = typeof(UnityEngine.Object).Assembly;
            Type? syncType = unity.GetType("UnityEngine.UnitySynchronizationContext");
            if (syncType == null)
                return null;

            foreach (FieldInfo field in syncType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(SynchronizationContext).IsAssignableFrom(field.FieldType))
                    continue;
                if (field.GetValue(null) is SynchronizationContext ctx)
                    return ctx;
            }

            MethodInfo? create = syncType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            if (create != null && create.Invoke(null, null) is SynchronizationContext created)
                return created;

            return null;
        }

        private static int CaptureMainThreadId()
        {
            if (_context == null)
                return 0;

            int threadId = 0;
            using var wait = new ManualResetEventSlim(false);
            _context.Post(_ =>
            {
                threadId = Thread.CurrentThread.ManagedThreadId;
                wait.Set();
            }, null);
            wait.Wait(TimeSpan.FromSeconds(5));
            return threadId;
        }
    }
}
