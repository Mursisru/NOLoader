using System;
using System.Globalization;

namespace NOLoader.Core.EngineTweaker
{
    internal static class EngineStringCache
    {
        private static string[]? _metricSpeedKmh;
        private static string[]? _imperialSpeedKt;
        private static string[]? _metricSpeedGroundKmh;
        private static string[]? _imperialSpeedGroundMph;
        private static string[]? _metricAltWholeM;
        private static string[]? _imperialAltFt;
        private static long _hits;
        private static long _misses;
        private static int _maxBucket;

        internal static long Hits => _hits;
        internal static long Misses => _misses;

        internal static void Initialize(int maxBucket)
        {
            _maxBucket = Math.Max(256, maxBucket);
            _metricSpeedKmh = BuildSpeedKmh(true);
            _imperialSpeedKt = BuildSpeedKt(false);
            _metricSpeedGroundKmh = _metricSpeedKmh;
            _imperialSpeedGroundMph = BuildSpeedMph(false);
            _metricAltWholeM = BuildAltM(true);
            _imperialAltFt = BuildAltFt(false);
        }

        internal static void ResetStats()
        {
            _hits = 0;
            _misses = 0;
        }

        public static string SpeedReading(float speed)
        {
            if (!Runtime.RuntimeConfig.StringCacheEnabled)
                return FormatSpeedReading(speed);

            bool metric = EngineTweakerGameAccess.IsMetricUnitSystem();
            int bucket = (int)Math.Round(metric ? speed * 3.6 : speed * 1.94384f, MidpointRounding.AwayFromZero);
            string[]? table = metric ? _metricSpeedKmh : _imperialSpeedKt;
            if (table != null && bucket >= 0 && bucket < table.Length)
            {
                _hits++;
                return table[bucket];
            }

            _misses++;
            return FormatSpeedReading(speed);
        }

        public static string SpeedReadingGround(float speed)
        {
            if (!Runtime.RuntimeConfig.StringCacheEnabled)
                return FormatSpeedReadingGround(speed);

            bool metric = EngineTweakerGameAccess.IsMetricUnitSystem();
            int bucket = (int)Math.Round(metric ? speed * 3.6 : speed * 2.23694, MidpointRounding.AwayFromZero);
            string[]? table = metric ? _metricSpeedGroundKmh : _imperialSpeedGroundMph;
            if (table != null && bucket >= 0 && bucket < table.Length)
            {
                _hits++;
                return table[bucket];
            }

            _misses++;
            return FormatSpeedReadingGround(speed);
        }

        public static string AltitudeReading(float altitude)
        {
            if (!Runtime.RuntimeConfig.StringCacheEnabled)
                return FormatAltitudeReading(altitude);

            bool metric = EngineTweakerGameAccess.IsMetricUnitSystem();
            if (metric)
            {
                if (Math.Abs(altitude) >= 10f)
                {
                    int bucket = (int)Math.Round(altitude, MidpointRounding.AwayFromZero);
                    if (_metricAltWholeM != null && bucket >= 0 && bucket < _metricAltWholeM.Length)
                    {
                        _hits++;
                        return _metricAltWholeM[bucket];
                    }
                }
            }
            else
            {
                int bucket = (int)Math.Round(altitude * 3.28084f, MidpointRounding.AwayFromZero);
                if (_imperialAltFt != null && bucket >= 0 && bucket < _imperialAltFt.Length)
                {
                    _hits++;
                    return _imperialAltFt[bucket];
                }
            }

            _misses++;
            return FormatAltitudeReading(altitude);
        }

        public static string ClimbRateReading(float speed)
        {
            _misses++;
            return FormatClimbRateReading(speed);
        }

        public static string DistanceReading(float distance)
        {
            _misses++;
            return FormatDistanceReading(distance);
        }

        private static string[] BuildSpeedKmh(bool metric)
        {
            var table = new string[_maxBucket + 1];
            for (int i = 0; i < table.Length; i++)
                table[i] = i.ToString(CultureInfo.InvariantCulture) + "km/h";
            return table;
        }

        private static string[] BuildSpeedKt(bool metric)
        {
            var table = new string[_maxBucket + 1];
            for (int i = 0; i < table.Length; i++)
                table[i] = i.ToString(CultureInfo.InvariantCulture) + "kt";
            return table;
        }

        private static string[] BuildSpeedMph(bool metric)
        {
            var table = new string[_maxBucket + 1];
            for (int i = 0; i < table.Length; i++)
                table[i] = i.ToString(CultureInfo.InvariantCulture) + "mph";
            return table;
        }

        private static string[] BuildAltM(bool metric)
        {
            int size = Math.Min(_maxBucket + 1, 16000);
            var table = new string[size];
            for (int i = 0; i < table.Length; i++)
                table[i] = i.ToString(CultureInfo.InvariantCulture) + "m";
            return table;
        }

        private static string[] BuildAltFt(bool metric)
        {
            int size = Math.Min(_maxBucket + 1, 52000);
            var table = new string[size];
            for (int i = 0; i < table.Length; i++)
                table[i] = i.ToString(CultureInfo.InvariantCulture) + "ft";
            return table;
        }

        private static string FormatSpeedReading(float speed)
        {
            if (EngineTweakerGameAccess.IsMetricUnitSystem())
                return string.Format(CultureInfo.InvariantCulture, "{0:F0}km/h", speed * 3.6);
            return string.Format(CultureInfo.InvariantCulture, "{0:F0}kt", speed * 1.94384f);
        }

        private static string FormatSpeedReadingGround(float speed)
        {
            if (EngineTweakerGameAccess.IsMetricUnitSystem())
                return string.Format(CultureInfo.InvariantCulture, "{0:F0}km/h", speed * 3.6);
            return string.Format(CultureInfo.InvariantCulture, "{0:F0}mph", speed * 2.23694);
        }

        private static string FormatAltitudeReading(float altitude)
        {
            if (!EngineTweakerGameAccess.IsMetricUnitSystem())
                return string.Format(CultureInfo.InvariantCulture, "{0:F0}ft", altitude * 3.28084f);
            if (Math.Abs(altitude) >= 10f)
                return string.Format(CultureInfo.InvariantCulture, "{0:F0}m", altitude);
            return string.Format(CultureInfo.InvariantCulture, "{0:F1}m", altitude);
        }

        private static string FormatClimbRateReading(float speed)
        {
            string sign = speed > 0.5f ? "+" : string.Empty;
            if (!EngineTweakerGameAccess.IsMetricUnitSystem())
                return string.Format(CultureInfo.InvariantCulture, "{0}{1:F0}fpm", sign, speed * 60f * 3.28084f);
            if (Math.Abs(speed) >= 10f)
                return string.Format(CultureInfo.InvariantCulture, "{0}{1:F0}m/s", sign, speed);
            return string.Format(CultureInfo.InvariantCulture, "{0}{1:F1}m/s", sign, speed);
        }

        private static string FormatDistanceReading(float distance)
        {
            if (EngineTweakerGameAccess.IsMetricUnitSystem())
            {
                if (distance > 10000f)
                    return string.Format(CultureInfo.InvariantCulture, "{0:F0}km", distance * 0.001f);
                if (distance > 1000f)
                    return string.Format(CultureInfo.InvariantCulture, "{0:F1}km", distance * 0.001f);
                return string.Format(CultureInfo.InvariantCulture, "{0:F0}m", distance);
            }

            float yards = distance * 1.09361f;
            if (yards < 1000f)
                return string.Format(CultureInfo.InvariantCulture, "{0:F0}yd", yards);
            return string.Format(CultureInfo.InvariantCulture, "{0:F1}nm", distance * 0.000539957f);
        }
    }
}
