using System.Collections.Generic;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class HudRefreshSkipState
    {
        private static readonly Dictionary<int, Snapshot> _snapshots = new Dictionary<int, Snapshot>();

        private struct Snapshot
        {
            public float Speed;
            public float Altitude;
        }

        internal static bool ShouldSkipManagerUpdate(object manager)
        {
            if (!Runtime.RuntimeConfig.HudRefreshSkipEnabled)
                return false;

            if (!EngineTweakerGameAccess.TryReadAircraft(manager, out object? aircraft) || aircraft == null)
                return false;

            int key = aircraft.GetHashCode();
            float speed = ReadFloat(aircraft, "speed");
            float altitude = ReadAltitude(aircraft);

            if (_snapshots.TryGetValue(key, out Snapshot last)
                && Mathf.Abs(last.Speed - speed) < 0.05f
                && Mathf.Abs(last.Altitude - altitude) < 0.5f)
                return true;

            _snapshots[key] = new Snapshot { Speed = speed, Altitude = altitude };
            return false;
        }

        internal static void Clear()
        {
            _snapshots.Clear();
        }

        private static float ReadFloat(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            object? value = field?.GetValue(target);
            return value == null ? 0f : System.Convert.ToSingle(value);
        }

        private static float ReadAltitude(object aircraft)
        {
            if (EngineTweakerGameAccess.TryReadTransformPosition(aircraft, out Vector3 pos))
                return pos.y;
            return ReadFloat(aircraft, "radarAlt");
        }
    }
}
