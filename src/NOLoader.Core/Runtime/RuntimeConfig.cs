using System;
using System.IO;

namespace NOLoader.Core.Runtime
{
    /// <summary>One-time INI read at bootstrap — RDYTU defaults favor zero gameplay overhead.</summary>
    public static class RuntimeConfig
    {
        public static bool RingLogEnabled { get; private set; } = true;
        /// <summary>Global Rigidbody.AddForce hooks (off in RDYTU — thousands of calls per physics tick).</summary>
        public static bool PhysicsCatchUnity { get; private set; }
        /// <summary>Missile/Motor::Thrust sanitizer — off in RDYTU unless INI enables (per-frame on missiles).</summary>
        public static bool PhysicsCatchMotor { get; private set; } = true;
        /// <summary>Gate L4 exception subscription (only when mods present).</summary>
        public static bool ExceptionTracking { get; private set; } = true;
        /// <summary>Subscribe to Unity log only when a mod may block mission load.</summary>
        public static bool ExceptionTrackingNeedsSubscription { get; private set; }
        /// <summary>Mission stage uses sceneLoaded events (legacy key ignored).</summary>
        public static float StagePollSeconds { get; private set; } = 0.35f;
        /// <summary>Ring log flush interval ms (background only).</summary>
        public static int RingFlushIntervalMs { get; private set; } = 4000;

        /// <summary>RDYTU perf: normal tick every N frames.</summary>
        public static int NormalTickStride { get; private set; } = 6;
        /// <summary>RDYTU perf: slow tick interval seconds.</summary>
        public static float SlowTickIntervalSec { get; private set; } = 1f;
        /// <summary>RDYTU perf: world snapshot refresh stride (frames).</summary>
        public static int WorldSnapshotStride { get; private set; } = 4;
        /// <summary>RDYTU perf: combined mod budget per frame (ms).</summary>
        public static double ModBudgetMs { get; private set; } = 0.5;

        /// <summary>Master switch for NOEngineTweaker IL patches (RDYTU).</summary>
        public static bool EngineTweakerEnabled { get; private set; }
        public static bool StringCacheEnabled { get; private set; }
        public static bool HudRefreshSkipEnabled { get; private set; }
        public static bool CullingOptimizerEnabled { get; private set; }
        /// <summary>Skip GroundVehicle::AnimateWheels when off-screen or low LOD.</summary>
        public static bool CullingGroundWheelsEnabled { get; private set; }
        /// <summary>Skip pilot animator on distant units — off in production (thrust regression risk).</summary>
        public static bool CullingPilotAnimEnabled { get; private set; }
        /// <summary>When true, visual skip only for units outside main camera frustum.</summary>
        public static bool CullingOffscreenOnlyEnabled { get; private set; } = true;
        /// <summary>Skip on-screen ground visuals farther than this (meters); 0 = disabled.</summary>
        public static float CullingOnScreenMaxM { get; private set; } = 400f;
        /// <summary>Disable GroundVehicle Renderer components when compound cull applies.</summary>
        public static bool CullingGroundRendererEnabled { get; private set; }
        /// <summary>Temporarily lower grass/tree GPU when sustained FPS drops.</summary>
        public static bool FpsAdaptiveDetailEnabled { get; private set; }
        public static bool FrameCacheEnabled { get; private set; }
        public static bool CanvasLimiterEnabled { get; private set; }
        public static float CullDistanceM { get; private set; } = 5000f;
        public static float DisplayDetailMin { get; private set; } = 1f;
        public static int StringCacheMax { get; private set; } = 2000;

        /// <summary>Round-robin budget for CombatHUD marker WorldToScreenPoint per frame.</summary>
        public static bool HudMarkerThrottleEnabled { get; private set; }
        /// <summary>0 = auto (min 4, max 12, M/4).</summary>
        public static int HudMarkersPerFrame { get; private set; }

        public static bool CoreBalancerEnabled { get; private set; }
        public static int ModWorkerCount { get; private set; } = 0;
        public static string ModAffinityMask { get; private set; } = "auto";
        public static string MainThreadAffinity { get; private set; } = "0";
        public static bool DoubleBufferEnabled { get; private set; } = true;
        public static double ModComputeBudgetMs { get; private set; } = 2.0;

        public static bool GpuRenderEnabled { get; private set; }
        public static bool GfxNativeJobsEnabled { get; private set; } = true;
        public static bool GpuMetricsEnabled { get; private set; } = true;
        public static bool GpuHudPassEnabled { get; private set; }
        public static bool GpuFxInstancingEnabled { get; private set; }

        public static bool ModOptimizerEnabled { get; private set; }
        public static bool ModTickAnalyzerEnabled { get; private set; } = true;
        public static bool ModReflectionCacheEnabled { get; private set; } = true;
        public static bool ModSceneLocatorEnabled { get; private set; } = true;
        public static bool ModCollisionLayersEnabled { get; private set; }
        public static bool ModShaderWarmupEnabled { get; private set; } = true;
        public static int ModLayerProjectile { get; private set; } = 27;
        public static double ModShaderWarmupBudgetMs { get; private set; } = 50.0;

        /// <summary>Skip GPU tweaker + adaptive trees while cockpit TrackIR is active (vanilla camera path).</summary>
        public static bool TrackIrSafeModeEnabled { get; private set; } = true;

#if NOLoader_DEV
        /// <summary>UDP Sim-Connect telemetry (DEV.SDK only).</summary>
        public static int TelemetryCaptureStride { get; private set; } = 1;
#endif

        public static void Load(string gameRoot)
        {
#if NOLoader_DEV
            RingLogEnabled = true;
            PhysicsCatchUnity = true;
            PhysicsCatchMotor = true;
            ExceptionTracking = true;
            ExceptionTrackingNeedsSubscription = true;
            StagePollSeconds = 0.35f;
            RingFlushIntervalMs = 4000;
            TelemetryCaptureStride = 1;
            return;
#else
            RingLogEnabled = false;
            PhysicsCatchUnity = false;
            PhysicsCatchMotor = false;
            ExceptionTracking = true;
            ExceptionTrackingNeedsSubscription = false;
            StagePollSeconds = 1.0f;
            RingFlushIntervalMs = 8000;

            string iniPath = Path.Combine(gameRoot, "noloader_config.ini");
            if (!File.Exists(iniPath))
                return;

            foreach (string rawLine in File.ReadAllLines(iniPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[')
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                Apply(key, value);
            }
#endif
        }

        private static void Apply(string key, string value)
        {
            switch (key)
            {
                case "ring_log":
                case "ringlog":
                    RingLogEnabled = ParseBool(value, RingLogEnabled);
                    break;
                case "physics_catch_unity":
                case "physics_catch_rigidbody":
                    PhysicsCatchUnity = ParseBool(value, PhysicsCatchUnity);
                    break;
                case "physics_catch_motor":
                    PhysicsCatchMotor = ParseBool(value, PhysicsCatchMotor);
                    break;
                case "exception_tracking":
                    ExceptionTracking = ParseBool(value, ExceptionTracking);
                    break;
                case "exception_tracking_subscribe":
                    ExceptionTrackingNeedsSubscription = ParseBool(value, ExceptionTrackingNeedsSubscription);
                    break;
                case "stage_poll_seconds":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float poll)
                        && poll >= 0.15f)
                        StagePollSeconds = poll;
                    break;
                case "ring_flush_ms":
                    if (int.TryParse(value, out int flush) && flush >= 1000)
                        RingFlushIntervalMs = flush;
                    break;
                case "normal_tick_stride":
                    if (int.TryParse(value, out int normalStride) && normalStride >= 1)
                        NormalTickStride = normalStride;
                    break;
                case "slow_tick_interval_sec":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float slowSec) && slowSec > 0f)
                        SlowTickIntervalSec = slowSec;
                    break;
                case "world_snapshot_stride":
                    if (int.TryParse(value, out int worldStride) && worldStride >= 1)
                        WorldSnapshotStride = worldStride;
                    break;
                case "mod_budget_ms":
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double budget) && budget > 0)
                        ModBudgetMs = budget;
                    break;
                case "engine_tweaker":
                    EngineTweakerEnabled = ParseBool(value, EngineTweakerEnabled);
                    break;
                case "string_cache":
                    StringCacheEnabled = ParseBool(value, StringCacheEnabled);
                    break;
                case "hud_refresh_skip":
                    HudRefreshSkipEnabled = ParseBool(value, HudRefreshSkipEnabled);
                    break;
                case "culling_optimizer":
                    CullingOptimizerEnabled = ParseBool(value, CullingOptimizerEnabled);
                    if (CullingOptimizerEnabled)
                    {
                        CullingGroundWheelsEnabled = true;
                        CullingPilotAnimEnabled = true;
                    }
                    break;
                case "culling_ground_wheels":
                    CullingGroundWheelsEnabled = ParseBool(value, CullingGroundWheelsEnabled);
                    break;
                case "culling_pilot_anim":
                    CullingPilotAnimEnabled = ParseBool(value, CullingPilotAnimEnabled);
                    break;
                case "culling_offscreen_only":
                    CullingOffscreenOnlyEnabled = ParseBool(value, CullingOffscreenOnlyEnabled);
                    break;
                case "culling_on_screen_max_m":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float onScreenMax) && onScreenMax >= 0f)
                        CullingOnScreenMaxM = onScreenMax;
                    break;
                case "culling_ground_renderer":
                    CullingGroundRendererEnabled = ParseBool(value, CullingGroundRendererEnabled);
                    break;
                case "fps_adaptive_detail":
                    FpsAdaptiveDetailEnabled = ParseBool(value, FpsAdaptiveDetailEnabled);
                    break;
                case "trackir_safe_mode":
                    TrackIrSafeModeEnabled = ParseBool(value, TrackIrSafeModeEnabled);
                    break;
                case "frame_cache":
                    FrameCacheEnabled = ParseBool(value, FrameCacheEnabled);
                    break;
                case "canvas_limiter":
                    CanvasLimiterEnabled = ParseBool(value, CanvasLimiterEnabled);
                    break;
                case "cull_distance_m":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float cull) && cull > 100f)
                        CullDistanceM = cull;
                    break;
                case "display_detail_min":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float detailMin) && detailMin >= 0f)
                        DisplayDetailMin = detailMin;
                    break;
                case "string_cache_max":
                    if (int.TryParse(value, out int cacheMax) && cacheMax >= 256)
                        StringCacheMax = cacheMax;
                    break;
                case "hud_marker_throttle":
                    HudMarkerThrottleEnabled = ParseBool(value, HudMarkerThrottleEnabled);
                    break;
                case "hud_markers_per_frame":
                    if (int.TryParse(value, out int hudBudget) && hudBudget >= 0)
                        HudMarkersPerFrame = hudBudget;
                    break;
                case "core_balancer":
                    CoreBalancerEnabled = ParseBool(value, CoreBalancerEnabled);
                    break;
                case "mod_worker_count":
                    if (int.TryParse(value, out int workerCount) && workerCount >= 1 && workerCount <= 8)
                        ModWorkerCount = workerCount;
                    break;
                case "mod_affinity_mask":
                    if (!string.IsNullOrWhiteSpace(value))
                        ModAffinityMask = value.Trim();
                    break;
                case "main_thread_affinity":
                    if (!string.IsNullOrWhiteSpace(value))
                        MainThreadAffinity = value.Trim();
                    break;
                case "double_buffer":
                    DoubleBufferEnabled = ParseBool(value, DoubleBufferEnabled);
                    break;
                case "mod_compute_budget_ms":
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double computeBudget)
                        && computeBudget > 0)
                        ModComputeBudgetMs = computeBudget;
                    break;
                case "gpu_render":
                    GpuRenderEnabled = ParseBool(value, GpuRenderEnabled);
                    break;
                case "gfx_native_jobs":
                    GfxNativeJobsEnabled = ParseBool(value, GfxNativeJobsEnabled);
                    break;
                case "gpu_metrics":
                    GpuMetricsEnabled = ParseBool(value, GpuMetricsEnabled);
                    break;
                case "gpu_hud_pass":
                    GpuHudPassEnabled = ParseBool(value, GpuHudPassEnabled);
                    break;
                case "gpu_fx_instancing":
                    GpuFxInstancingEnabled = ParseBool(value, GpuFxInstancingEnabled);
                    break;
                case "mod_optimizer":
                    ModOptimizerEnabled = ParseBool(value, ModOptimizerEnabled);
                    break;
                case "mod_tick_analyzer":
                    ModTickAnalyzerEnabled = ParseBool(value, ModTickAnalyzerEnabled);
                    break;
                case "mod_reflection_cache":
                    ModReflectionCacheEnabled = ParseBool(value, ModReflectionCacheEnabled);
                    break;
                case "mod_scene_locator":
                    ModSceneLocatorEnabled = ParseBool(value, ModSceneLocatorEnabled);
                    break;
                case "mod_collision_layers":
                    ModCollisionLayersEnabled = ParseBool(value, ModCollisionLayersEnabled);
                    break;
                case "mod_shader_warmup":
                    ModShaderWarmupEnabled = ParseBool(value, ModShaderWarmupEnabled);
                    break;
                case "mod_layer_projectile":
                    if (int.TryParse(value, out int layer) && layer >= 0 && layer <= 31)
                        ModLayerProjectile = layer;
                    break;
                case "mod_shader_warmup_budget_ms":
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double warmupBudget)
                        && warmupBudget > 0)
                        ModShaderWarmupBudgetMs = warmupBudget;
                    break;
            }
        }

        private static bool ParseBool(string value, bool defaultValue)
        {
            if (string.Equals(value, "1", StringComparison.Ordinal) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "0", StringComparison.Ordinal) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
                return false;
            return defaultValue;
        }
    }
}
