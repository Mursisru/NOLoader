#if !NOLoader_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NOLoader.Core.EngineTweaker;
using NOLoader.Core.Runtime;
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
        private static Type? _markerType;
        private static FieldInfo? _markerUnitField;
        private static FieldInfo? _markerHiddenField;
        private static FieldInfo? _markerImageField;
        private static MethodInfo? _globalPositionMethod;
        private static MethodInfo? _toLocalPositionMethod;
        private static PropertyInfo? _imageColorProp;

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

            _markerType = Type.GetType("HUDUnitMarker, Assembly-CSharp");
            if (_markerType != null)
            {
                _markerUnitField = _markerType.GetField("unit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _markerHiddenField = _markerType.GetField("hidden", BindingFlags.Instance | BindingFlags.NonPublic);
                _markerImageField = _markerType.GetField("image", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            Type? unitType = Type.GetType("Unit, Assembly-CSharp");
            _globalPositionMethod = unitType?.GetMethod("GlobalPosition", BindingFlags.Instance | BindingFlags.Public);
            Type? globalPosType = Type.GetType("GlobalPosition, Assembly-CSharp");
            _toLocalPositionMethod = globalPosType?.GetMethod("ToLocalPosition", BindingFlags.Instance | BindingFlags.Public);
            _imageColorProp = typeof(Image).GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
        }

        internal static bool TryGetCombatHudInstance(out object? instance)
        {
            instance = null;
            EnsureInitialized();
            if (_combatHudType == null)
                return false;

            Type? singleton = Type.GetType("SceneSingleton`1, Assembly-CSharp");
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

            if (!TryGetMainCamera(out Camera? camera) || camera == null)
                return 0;

            Vector3 camPos = camera.transform.position;
            Vector3 camForward = camera.transform.forward;

            for (int i = 0; i < markers.Count; i++)
            {
                object? marker = markers[i];
                if (marker == null || _markerImageField == null)
                    continue;

                if (_markerHiddenField?.GetValue(marker) is bool hidden && hidden)
                    continue;

                if (_markerImageField.GetValue(marker) is not Image image)
                    continue;

                if (!TryGetMarkerScreenPosition(marker, camera, camPos, camForward, out Vector3 screen))
                    continue;

                screenPositions.Add(screen);
                colors.Add(_imageColorProp?.GetValue(image) is Color c ? c : Color.white);
            }

            return screenPositions.Count;
        }

        internal static void RestoreMarkerUiVisibility()
        {
            if (!TryGetCombatHudInstance(out object? hud) || hud == null || _markersField == null)
                return;

            if (_markersField.GetValue(hud) is not IList markers)
                return;

            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] == null || _markerImageField?.GetValue(markers[i]) is not Image image)
                    continue;
                image.enabled = true;
            }
        }

        internal static int CaptureHitMarkerPositions(List<Vector3> screenPositions)
        {
            screenPositions.Clear();
            if (!TryGetCombatHudInstance(out object? hud) || hud == null || _hitMarkersField == null)
                return 0;

            if (_hitMarkersField.GetValue(hud) is not IList hitMarkers)
                return 0;

            Type? hitType = hitMarkers.Count > 0 ? hitMarkers[0]?.GetType() : null;
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

        private static bool TryGetMarkerScreenPosition(
            object marker,
            Camera camera,
            Vector3 camPos,
            Vector3 camForward,
            out Vector3 screen)
        {
            screen = default;
            if (_markerUnitField == null || _globalPositionMethod == null || _toLocalPositionMethod == null)
                return false;

            object? unit = _markerUnitField.GetValue(marker);
            if (unit == null)
                return false;

            object? globalPos;
            try
            {
                globalPos = _globalPositionMethod.Invoke(unit, null);
            }
            catch
            {
                return false;
            }

            if (globalPos == null)
                return false;

            object? localPos;
            try
            {
                localPos = _toLocalPositionMethod.Invoke(globalPos, null);
            }
            catch
            {
                return false;
            }

            if (localPos is not Vector3 world)
                return false;

            if (Vector3.Dot(world - camPos, camForward) < 0f)
                return false;

            screen = camera.WorldToScreenPoint(world);
            screen.z = 0f;
            return true;
        }

        private static bool TryGetMainCamera(out Camera? camera)
        {
            camera = null;
            EngineTweakerGameAccess.EnsureInitialized();
            Type? csmType = Type.GetType("CameraStateManager, Assembly-CSharp");
            if (csmType == null)
                return false;

            FieldInfo? singleton = csmType.GetField("i", BindingFlags.Static | BindingFlags.Public);
            object? csm = singleton?.GetValue(null);
            if (csm == null)
                return false;

            if (!EngineTweakerGameAccess.TryReadCameraState(csm, out _, out _, out camera) || camera == null)
                return false;

            return true;
        }
    }
}
#endif
