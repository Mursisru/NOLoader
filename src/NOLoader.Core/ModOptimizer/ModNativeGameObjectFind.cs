#if !NOLoader_DEV
using System;
using System.IO;
using System.Reflection;
using NOLoader.Core.Logging;
using NOLoader.Patcher;
using UnityEngine;

namespace NOLoader.Core.ModOptimizer
{
    /// <summary>Native GameObject.Find via vanilla CoreModule backup (redirected Find cannot call itself).</summary>
    internal static class ModNativeGameObjectFind
    {
        private static Func<string, GameObject>? _nativeFind;
        private static bool _initialized;

        internal static bool IsAvailable => _nativeFind != null;

        internal static void Initialize(string gameRoot)
        {
            if (_initialized)
                return;

            _initialized = true;
            if (string.IsNullOrEmpty(gameRoot))
                return;

            _nativeFind = TryCreateNativeFind(gameRoot);
            if (_nativeFind != null)
                RingBufferLog.WriteAscii("[ModOpt] native Find bound (vanilla CoreModule)");
            else
                RingBufferLog.WriteAscii("[ModOpt][WARN] native Find unavailable — non-mod callers use hierarchy fallback");
        }

        internal static GameObject? Invoke(string name)
        {
            if (_nativeFind == null)
                return null;

            try
            {
                return _nativeFind(name);
            }
            catch
            {
                return null;
            }
        }

        private static Func<string, GameObject>? TryCreateNativeFind(string gameRoot)
        {
            const string module = "UnityEngine.CoreModule.dll";
            string path = ManagedModuleGuard.GetVanillaBackupPath(gameRoot, module);
            if (!File.Exists(path))
                path = ManagedModuleGuard.GetLivePath(gameRoot, module) + ManagedModuleGuard.LegacyBackupExtension;
            if (!File.Exists(path))
                return null;

            try
            {
                Assembly asm = Assembly.LoadFrom(path);
                Type? goType = asm.GetType("UnityEngine.GameObject");
                MethodInfo? mi = goType?.GetMethod(
                    "Find",
                    BindingFlags.Public | BindingFlags.Static,
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
