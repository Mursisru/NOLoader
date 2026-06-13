using System;
using System.Reflection;
using NOLoader.Core.Interop;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class EngineTweakerGameAccess
    {
        private static FieldInfo? _displayDetailField;
        private static FieldInfo? _unitSystemField;
        private static FieldInfo? _aircraftField;
        private static FieldInfo? _animatorField;
        private static FieldInfo? _detailUnitIndexField;
        private static FieldInfo? _followingUnitField;
        private static FieldInfo? _mainCameraField;
        private static FieldInfo? _allUnitsField;
        private static MethodInfo? _isLocalAircraftUnit;
        private static MethodInfo? _getLocalAircraft;
        private static PropertyInfo? _transformProperty;
        private static PropertyInfo? _positionProperty;
        private static bool _initialized;

        internal static void EnsureInitialized()
        {
            if (_initialized)
                return;

            Type? unitType = GameTypeCache.Resolve("Unit");
            _displayDetailField = unitType?.GetField("displayDetail", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? playerSettings = GameTypeCache.Resolve("PlayerSettings");
            _unitSystemField = playerSettings?.GetField("unitSystem", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Type? hudAppManager = GameTypeCache.Resolve("HUDAppManager");
            _aircraftField = hudAppManager?.GetField("aircraft", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? mfdAppManager = GameTypeCache.Resolve("MFDAppManager");
            if (_aircraftField == null)
                _aircraftField = mfdAppManager?.GetField("aircraft", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? pilotDismounted = GameTypeCache.Resolve("PilotDismounted");
            _animatorField = pilotDismounted?.GetField("animator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? pilot = GameTypeCache.Resolve("Pilot");
            if (_animatorField == null)
                _animatorField = pilot?.GetField("animator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? cameraStateManager = GameTypeCache.Resolve("CameraStateManager");
            _detailUnitIndexField = cameraStateManager?.GetField("detailUnitIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _followingUnitField = cameraStateManager?.GetField("followingUnit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _mainCameraField = cameraStateManager?.GetField("mainCamera", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? gameManager = GameTypeCache.Resolve("GameManager");
            if (gameManager != null && unitType != null)
                _isLocalAircraftUnit = gameManager.GetMethod("IsLocalAircraft", BindingFlags.Static | BindingFlags.Public, null, new[] { unitType }, null);
            _getLocalAircraft = gameManager?.GetMethod("GetLocalAircraft", BindingFlags.Static | BindingFlags.Public);

            Type? unitRegistry = GameTypeCache.Resolve("UnitRegistry");
            _allUnitsField = unitRegistry?.GetField("allUnits", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Type? component = typeof(Component);
            _transformProperty = component.GetProperty("transform", BindingFlags.Instance | BindingFlags.Public);
            _positionProperty = typeof(Transform).GetProperty("position", BindingFlags.Instance | BindingFlags.Public);

            _initialized = true;
        }

        internal static bool IsMetricUnitSystem()
        {
            EnsureInitialized();
            if (_unitSystemField == null)
                return true;

            object? value = _unitSystemField.GetValue(null);
            return value == null || Convert.ToInt32(value) == 0;
        }

        internal static float ReadDisplayDetail(object unit)
        {
            EnsureInitialized();
            if (_displayDetailField == null)
                return 1f;

            object? value = _displayDetailField.GetValue(unit);
            return value == null ? 1f : Convert.ToSingle(value);
        }

        internal static void WriteDisplayDetail(object unit, float value)
        {
            EnsureInitialized();
            _displayDetailField?.SetValue(unit, value);
        }

        internal static bool TryGetLocalPlayerUnit(out object? unit)
        {
            unit = null;
            EnsureInitialized();
            if (_getLocalAircraft == null)
                return false;

            object?[] args = { null };
            if (_getLocalAircraft.Invoke(null, args) is bool ok && ok)
            {
                unit = args[0];
                return unit != null;
            }

            return false;
        }

        internal static bool IsLocalPlayerUnit(object unit)
        {
            EnsureInitialized();
            if (_isLocalAircraftUnit == null || unit == null)
                return false;

            object? result = _isLocalAircraftUnit.Invoke(null, new[] { unit });
            return result is bool isLocal && isLocal;
        }

        internal static bool IsFollowingUnit(object unit)
        {
            EnsureInitialized();
            if (unit == null)
                return false;

            object? csm = ResolveCameraStateManager();
            if (csm == null || _followingUnitField == null)
                return false;

            object? following = _followingUnitField.GetValue(csm);
            return ReferenceEquals(following, unit);
        }

        private static object? ResolveCameraStateManager()
        {
            Type? csmType = GameTypeCache.Resolve("CameraStateManager");
            if (csmType == null)
                return null;

            FieldInfo? singleton = csmType.GetField("i", BindingFlags.Static | BindingFlags.Public);
            return singleton?.GetValue(null);
        }

        internal static bool TryReadAircraft(object manager, out object? aircraft)
        {
            EnsureInitialized();
            aircraft = _aircraftField?.GetValue(manager);
            return aircraft != null;
        }

        internal static bool TryReadTransformPosition(object obj, out Vector3 position)
        {
            position = default;
            EnsureInitialized();
            if (_transformProperty == null || _positionProperty == null)
                return false;

            object? transform = _transformProperty.GetValue(obj);
            if (transform == null)
                return false;

            object? pos = _positionProperty.GetValue(transform);
            if (pos is Vector3 vector)
            {
                position = vector;
                return true;
            }

            return false;
        }

        internal static bool TryReadCameraState(object csm, out int detailUnitIndex, out object? followingUnit, out Camera? mainCamera)
        {
            detailUnitIndex = 0;
            followingUnit = null;
            mainCamera = null;
            EnsureInitialized();
            if (_detailUnitIndexField == null || _followingUnitField == null || _mainCameraField == null)
                return false;

            detailUnitIndex = Convert.ToInt32(_detailUnitIndexField.GetValue(csm));
            followingUnit = _followingUnitField.GetValue(csm);
            mainCamera = _mainCameraField.GetValue(csm) as Camera;
            return true;
        }

        internal static bool TryGetUnitAtIndex(int index, out object? unit)
        {
            unit = null;
            EnsureInitialized();
            if (_allUnitsField?.GetValue(null) is System.Collections.IList list
                && index >= 0
                && index < list.Count)
            {
                unit = list[index];
                return unit != null;
            }

            return false;
        }

        internal static void SetAnimatorEnabled(object instance, bool enabled)
        {
            EnsureInitialized();
            object? animator = _animatorField?.GetValue(instance);
            if (animator == null)
                return;

            PropertyInfo? prop = animator.GetType().GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
            prop?.SetValue(animator, enabled);
        }

        internal static bool TryReadPilotAircraft(object pilot, out object? aircraft)
        {
            aircraft = null;
            EnsureInitialized();
            FieldInfo? field = pilot.GetType().GetField("aircraft", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            aircraft = field?.GetValue(pilot);
            return aircraft != null;
        }
    }
}
