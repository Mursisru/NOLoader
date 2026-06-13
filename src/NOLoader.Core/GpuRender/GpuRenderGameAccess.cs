#if !NOLoader_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOLoader.Core.GpuRender
{
    internal static class GpuRenderGameAccess
    {
        private static bool _ready;
        private static Type? _combatHudType;
        private static FieldInfo? _markersField;
        private static FieldInfo? _hitMarkersField;
        private static MethodInfo? _markerTransformGetter;
        private static MethodInfo? _markerImageGetter;
        private static PropertyInfo? _imageEnabledProp;

        internal static void EnsureInitialized()
        {
            if (_ready)
                return;

            _ready = true;
            _combatHudType = Type.GetType("CombatHUD, Assembly-CSharp");
            if (_combatHudType == null)
                return;

            _markersField = _combatHudType.GetField("markers", BindingFlags.Instance | BindingFlags.NonPublic);
            _hitMarkersField = _combatHudType.GetField("hitMarkers", BindingFlags.Instance | BindingFlags.NonPublic);

            Type markerType = Type.GetType("HUDUnitMarker, Assembly-CSharp") ?? typeof(object);
            _markerTransformGetter = markerType.GetMethod("get__transform", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? markerType.GetField("_transform", BindingFlags.Instance | BindingFlags.NonPublic)?.GetType().GetProperty("transform")?.GetGetMethod();
            _markerImageGetter = markerType.GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType == typeof(Image)
                ? null
                : null;

            Type hudMarker = Type.GetType("HUDUnitMarker, Assembly-CSharp");
            if (hudMarker != null)
            {
                FieldInfo? tf = hudMarker.GetField("_transform", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? img = hudMarker.GetField("image", BindingFlags.Instance | BindingFlags.NonPublic);
                if (tf != null)
                    _markerTransformGetter = tf.GetType().GetProperty("transform")?.GetGetMethod();
                if (img != null)
                    _imageEnabledProp = typeof(Image).GetProperty("enabled");
            }
        }

        internal static bool TryGetCombatHudInstance(out object? instance)
        {
            instance = null;
            EnsureInitialized();
            if (_combatHudType == null)
                return false;

            Type singleton = Type.GetType("SceneSingleton`1, Assembly-CSharp");
            if (singleton == null)
                return false;

            Type combatSingleton = singleton.MakeGenericType(_combatHudType);
            PropertyInfo? prop = combatSingleton.GetProperty("i", BindingFlags.Public | BindingFlags.Static);
            instance = prop?.GetValue(null);
            return instance != null;
        }

        internal static int CaptureHudMarkers(List<Vector3> screenPositions, List<Color> colors)
        {
            screenPositions.Clear();
            colors.Clear();
            if (!TryGetCombatHudInstance(out object? hud) || hud == null || _markersField == null)
                return 0;

            if (_markersField.GetValue(hud) is not IList markers)
                return 0;

            Type markerType = Type.GetType("HUDUnitMarker, Assembly-CSharp");
            FieldInfo? transformField = markerType?.GetField("_transform", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo? imageField = markerType.GetField("image", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < markers.Count; i++)
            {
                object? marker = markers[i];
                if (marker == null || transformField == null || imageField == null)
                    continue;

                if (imageField.GetValue(marker) is not Image image || !image.enabled)
                    continue;

                if (transformField.GetValue(marker) is not Transform tr)
                    continue;

                screenPositions.Add(tr.position);
                colors.Add(image.color);
            }

            return screenPositions.Count;
        }

        internal static int CaptureHitMarkerPositions(List<Vector3> screenPositions)
        {
            screenPositions.Clear();
            if (!TryGetCombatHudInstance(out object? hud) || hud == null || _hitMarkersField == null)
                return 0;

            if (_hitMarkersField.GetValue(hud) is not IList hitMarkers)
                return 0;

            Type hitType = hitMarkers.Count > 0 ? hitMarkers[0]?.GetType() : null;
            if (hitType == null)
                return 0;

            FieldInfo? goField = hitType.GetField("marker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? hitType.GetField("hitMarker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < hitMarkers.Count; i++)
            {
                object? entry = hitMarkers[i];
                if (entry == null || goField == null)
                    continue;

                if (goField.GetValue(entry) is not GameObject go || !go.activeInHierarchy)
                    continue;

                screenPositions.Add(go.transform.position);
            }

            return screenPositions.Count;
        }
    }
}
#endif
