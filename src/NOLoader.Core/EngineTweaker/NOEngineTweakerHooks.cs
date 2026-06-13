using NOLoader.API;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;

namespace NOLoader.Core.EngineTweaker
{
    public static class NOEngineTweakerHooks
    {
        // PrefixSkip: true = run original Update, false = skip. Invert skip intent.
        public static bool HUDAppManagerUpdatePrefix(object __instance)
            => !HudRefreshSkipState.ShouldSkipManagerUpdate(__instance);

        public static bool MFDAppManagerUpdatePrefix(object __instance)
            => !HudRefreshSkipState.ShouldSkipManagerUpdate(__instance);

        public static void CameraStateManagerUpdatePostfix(object __instance)
        {
            EngineFrameCacheImpl.Instance.PopulateFromCameraState(__instance);
        }

        public static bool AnimateWheelsPrefixSkip(object __instance, float speed)
        {
            if (__instance == null)
                return true;
            return !EngineCullingState.ShouldSkipVisualUpdate(__instance);
        }

        public static void PilotDismountedFixedUpdatePrefix(object __instance)
        {
            if (__instance == null)
                return;

            EngineTweakerGameAccess.SetAnimatorEnabled(__instance, !EngineCullingState.ShouldSkipVisualUpdate(__instance));
        }

        public static void PilotUpdatePrefix(object __instance)
        {
            if (__instance == null)
                return;

            if (!EngineTweakerGameAccess.TryReadPilotAircraft(__instance, out object? aircraft) || aircraft == null)
                return;

            EngineTweakerGameAccess.SetAnimatorEnabled(__instance, !EngineCullingState.ShouldSkipVisualUpdate(aircraft));
        }

        public static bool RegisterCanvasElementForGraphicRebuildPrefixSkip(object element)
            => !CanvasRebuildLimiter.ShouldBlockCanvasRebuild(element);
    }

    internal static class NOEngineTweakerBootstrap
    {
        private static bool _initialized;

        internal static void Initialize()
        {
#if NOLoader_DEV
            return;
#else
            if (_initialized || !RuntimeConfig.EngineTweakerEnabled)
                return;

            _initialized = true;
            EngineTweakerGameAccess.EnsureInitialized();
            EngineStringCache.Initialize(RuntimeConfig.StringCacheMax);
            EngineStringCache.ResetStats();
            EngineFrameCacheImpl.Instance.ResetStats();
            EngineCullingState.ResetStats();
            CanvasRebuildLimiter.ResetStats();
            HudRefreshSkipState.Clear();
            CanvasRebuildLimiter.ClearFrame();

            NOModRuntime.FrameCache = EngineFrameCacheImpl.Instance;
            GameplayMechanicsGuard.PinLocalPlayerSimState();

            RingBufferLog.WriteAscii("[NOLoader] NOEngineTweaker initialized string="
                + RuntimeConfig.StringCacheEnabled
                + " cull=" + RuntimeConfig.CullingOptimizerEnabled
                + " frame=" + RuntimeConfig.FrameCacheEnabled
                + " canvas=" + RuntimeConfig.CanvasLimiterEnabled);

            GpuRender.GpuRenderBootstrap.OnUnityReady();
#endif
        }

        internal static void OnSceneChanged()
        {
#if !NOLoader_DEV
            HudRefreshSkipState.Clear();
            CanvasRebuildLimiter.ClearFrame();
            GameplayMechanicsGuard.PinLocalPlayerSimState();
#endif
        }

        internal static void LogPeriodicStats()
        {
#if !NOLoader_DEV
            if (!RuntimeConfig.EngineTweakerEnabled)
                return;

            GameplayMechanicsGuard.PinLocalPlayerSimState();

            RingBufferLog.WriteAscii("[EngineTweaker] string_cache hits=" + EngineStringCache.Hits
                + " miss=" + EngineStringCache.Misses
                + " cull_skip=" + EngineCullingState.SkippedAnim
                + " frame_reads=" + EngineFrameCacheImpl.Instance.Reads
                + " canvas_block=" + CanvasRebuildLimiter.Blocked);
#endif
        }
    }
}
