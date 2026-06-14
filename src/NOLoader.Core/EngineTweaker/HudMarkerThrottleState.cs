using System.Collections.Generic;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class HudMarkerThrottleState
    {
        private static int _frameId = -1;
        private static int _budget;
        private static int _processed;
        private static int _skipped;
        private static int _markerCount;
        private static long _totalSkipped;
        private static readonly HashSet<object> _updatedKeys = new HashSet<object>();

        internal static long TotalSkipped => _totalSkipped;

        internal static void BeginFrame(object combatHud)
        {
            if (TrackIrStabilityGuard.ShouldProtectVanillaCameraFrame())
                return;

            if (!RuntimeConfig.HudMarkerThrottleEnabled)
                return;

            int frame = Time.frameCount;
            if (frame == _frameId)
                return;

            _frameId = frame;
            _processed = 0;
            _skipped = 0;
            _updatedKeys.Clear();
            _markerCount = HudMarkerThrottleGameAccess.ReadMarkerCount(combatHud);
            _budget = ComputeBudget(_markerCount);

            if (frame % 600 == 0 && _markerCount > 0)
            {
                RingBufferLog.WriteAscii("[HudMarkerThrottle] budget=" + _budget
                    + " markers=" + _markerCount);
            }
        }

        internal static bool ShouldSkipUpdatePosition(object marker)
        {
            if (TrackIrStabilityGuard.ShouldProtectVanillaCameraFrame())
                return false;

            if (!RuntimeConfig.HudMarkerThrottleEnabled || marker == null)
                return false;

            if (HudMarkerThrottleGameAccess.IsHighPriority(marker))
            {
                TrackUpdated(marker);
                return false;
            }

            if (_processed < _budget)
            {
                TrackUpdated(marker);
                return false;
            }

            _skipped++;
            _totalSkipped++;
            return true;
        }

        internal static void TrackUpdated(object marker)
        {
            if (marker == null)
                return;

            _processed++;
            _updatedKeys.Add(marker);
        }

        internal static bool ShouldSkipJamming(object marker)
        {
            if (TrackIrStabilityGuard.ShouldProtectVanillaCameraFrame())
                return false;

            if (!RuntimeConfig.HudMarkerThrottleEnabled || marker == null)
                return false;

            return !_updatedKeys.Contains(marker);
        }

        internal static void ResetStats()
        {
            _totalSkipped = 0;
        }

        private static int ComputeBudget(int markerCount)
        {
            int configured = RuntimeConfig.HudMarkersPerFrame;
            if (configured > 0)
                return System.Math.Min(markerCount, configured);

            if (markerCount <= 4)
                return markerCount;

            return System.Math.Min(12, System.Math.Max(4, markerCount / 4));
        }
    }
}
