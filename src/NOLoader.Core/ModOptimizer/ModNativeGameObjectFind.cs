#if !NOLoader_DEV
using System;
using System.Reflection;
using NOLoader.Core.Logging;
using UnityEngine;

namespace NOLoader.Core.ModOptimizer
{
    /// <summary>Native GameObject.Find via Cecil-injected InternalCall stub in CoreModule.</summary>
    internal static class ModNativeGameObjectFind
    {
        private const string StubTypeName = "UnityEngine.NOLoaderNativeFind";
        private const string StubMethodName = "InvokeNative";

        private static Func<string, GameObject>? _nativeFind;
        private static bool _initialized;

        internal static bool IsAvailable => _nativeFind != null;

        internal static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            _nativeFind = TryBindNativeFind();
            if (_nativeFind != null)
                RingBufferLog.WriteAscii("[ModOpt] native Find bound (CoreModule stub)");
            else
                RingBufferLog.WriteAscii("[ModOpt][WARN] native Find stub missing — restore CoreModule from vanilla backup");
        }

        internal static GameObject? Invoke(string name)
        {
            if (_nativeFind == null)
                return null;

            try
            {
                return _nativeFind(name);
            }
            catch (Exception ex)
            {
                RingBufferLog.WriteAscii("[ModOpt][WARN] native Find failed: " + ex.GetType().Name);
                return null;
            }
        }

        private static Func<string, GameObject>? TryBindNativeFind()
        {
            try
            {
                Type? stubType = typeof(GameObject).Assembly.GetType(StubTypeName, throwOnError: false);
                MethodInfo? mi = stubType?.GetMethod(
                    StubMethodName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
                if (mi == null)
                    return null;

                return (Func<string, GameObject>)Delegate.CreateDelegate(typeof(Func<string, GameObject>), mi);
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
