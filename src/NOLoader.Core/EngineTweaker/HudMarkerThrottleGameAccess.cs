using System;
using System.Reflection;
using NOLoader.Core.Interop;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class HudMarkerThrottleGameAccess
    {
        private static FieldInfo? _markersField;
        private static FieldInfo? _selectedField;
        private static FieldInfo? _flashingField;
        private static FieldInfo? _freshField;
        private static FieldInfo? _timeCreatedField;
        private static FieldInfo? _hiddenField;
        private static bool _initialized;

        internal static void EnsureInitialized()
        {
            if (_initialized)
                return;

            Type? combatHud = GameTypeCache.Resolve("CombatHUD");
            _markersField = combatHud?.GetField("markers", BindingFlags.Instance | BindingFlags.NonPublic);

            Type? markerType = GameTypeCache.Resolve("HUDUnitMarker");
            if (markerType != null)
            {
                _selectedField = markerType.GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic);
                _flashingField = markerType.GetField("flashing", BindingFlags.Instance | BindingFlags.NonPublic);
                _freshField = markerType.GetField("fresh", BindingFlags.Instance | BindingFlags.NonPublic);
                _timeCreatedField = markerType.GetField("timeCreated", BindingFlags.Instance | BindingFlags.NonPublic);
                _hiddenField = markerType.GetField("hidden", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            _initialized = true;
        }

        internal static int ReadMarkerCount(object combatHud)
        {
            EnsureInitialized();
            if (_markersField?.GetValue(combatHud) is System.Collections.ICollection list)
                return list.Count;
            return 0;
        }

        internal static bool IsHighPriority(object marker)
        {
            EnsureInitialized();
            if (marker == null)
                return false;

            if (_hiddenField != null && _hiddenField.GetValue(marker) is bool hidden && hidden)
                return false;

            if (_selectedField?.GetValue(marker) is bool selected && selected)
                return true;

            if (_flashingField?.GetValue(marker) is bool flashing && flashing)
                return true;

            if (_freshField?.GetValue(marker) is bool fresh && fresh)
            {
                if (_timeCreatedField?.GetValue(marker) is float created
                    && Time.timeSinceLevelLoad - created <= 1f)
                    return true;
            }

            return false;
        }
    }
}
