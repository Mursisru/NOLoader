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
        public static int WorldSnapshotStride { get; private set; } = 1;
        /// <summary>RDYTU perf: combined mod budget per frame (ms).</summary>
        public static double ModBudgetMs { get; private set; } = 0.5;

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
