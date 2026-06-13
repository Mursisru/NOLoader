#if !NOLoader_DEV
using System.Collections.Generic;
using System.Reflection;
using NOLoader.API;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;

namespace NOLoader.Core.ModOptimizer
{
    internal static class ModOptimizerBootstrap
    {
        private static bool _initialized;

        internal static bool IsActive => _initialized && RuntimeConfig.ModOptimizerEnabled;

        internal static bool IsTickAnalyzerActive =>
            IsActive && RuntimeConfig.ModTickAnalyzerEnabled;

        internal static bool IsReflectionCacheActive =>
            IsActive && RuntimeConfig.ModReflectionCacheEnabled;

        internal static bool IsSceneLocatorActive =>
            IsActive && RuntimeConfig.ModSceneLocatorEnabled;

        internal static bool IsCollisionLayersActive =>
            IsActive && RuntimeConfig.ModCollisionLayersEnabled;

        internal static bool IsShaderWarmupActive =>
            IsActive && RuntimeConfig.ModShaderWarmupEnabled;

        internal static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (!RuntimeConfig.ModOptimizerEnabled)
            {
                RingBufferLog.WriteAscii("[ModOpt] disabled (mod_optimizer=0)");
                return;
            }

            NOModRuntime.Reflection = ModReflectionCache.Instance;
            NOModRuntime.Scene = ModSceneLocator.Instance;
            NOModRuntime.Collision = ModCollisionRegistry.Instance;

            RingBufferLog.WriteAscii("[ModOpt] enabled analyzer=" + RuntimeConfig.ModTickAnalyzerEnabled
                + " reflection=" + RuntimeConfig.ModReflectionCacheEnabled
                + " scene=" + RuntimeConfig.ModSceneLocatorEnabled
                + " collision=" + RuntimeConfig.ModCollisionLayersEnabled
                + " warmup=" + RuntimeConfig.ModShaderWarmupEnabled
                + " layer=" + RuntimeConfig.ModLayerProjectile);
        }

        internal static void OnModAssemblyLoaded(Assembly assembly, int modIdHash)
        {
            if (!IsActive)
                return;

            ModAssemblyTracker.Register(assembly, modIdHash);
        }

        internal static void OnModAssemblyUnloaded(Assembly assembly)
        {
            if (!IsActive)
                return;

            int modIdHash = ModAssemblyTracker.TryGetModIdHash(assembly);
            ModAssemblyTracker.Unregister(assembly);
            ModReflectionCache.Instance.ClearMod(modIdHash);
            ModSceneLocator.Instance.ClearMod(modIdHash);
        }
    }
}
#endif
