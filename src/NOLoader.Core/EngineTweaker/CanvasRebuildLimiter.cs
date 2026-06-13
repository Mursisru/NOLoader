using System.Collections.Generic;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class CanvasRebuildLimiter
    {
        private static int _frameId = -1;
        private static readonly HashSet<int> _seenElements = new HashSet<int>();
        private static long _blocked;
        private static long _passed;

        internal static long Blocked => _blocked;
        internal static long Passed => _passed;

        internal static void ResetStats()
        {
            _blocked = 0;
            _passed = 0;
        }

        internal static void ClearFrame()
        {
            _frameId = -1;
            _seenElements.Clear();
        }

        public static bool ShouldBlockCanvasRebuild(object element)
        {
            if (!Runtime.RuntimeConfig.CanvasLimiterEnabled || element == null)
                return false;

            int frame = Time.frameCount;
            if (frame != _frameId)
            {
                _frameId = frame;
                _seenElements.Clear();
            }

            int key = element.GetHashCode();
            if (_seenElements.Add(key))
            {
                _passed++;
                return false;
            }

            _blocked++;
            return true;
        }
    }
}
