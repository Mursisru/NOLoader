using System;
using System.Reflection;
using NOLoader.Core.Interop;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    /// <summary>Full vanilla camera path in cockpit TrackIR / extreme look — bypasses all tweaker IL side effects.</summary>
    internal static class TrackIrStabilityGuard
    {
        private const float ExtremePanDegrees = 85f;
        private const float ExtremeTiltDegrees = 35f;

        private static bool _initialized;
        private static bool _lastProtect;
        private static int _frameId = -1;
        private static bool _frameProtect;
        private static FieldInfo? _useTrackIrField;
        private static FieldInfo? _cameraModeField;
        private static FieldInfo? _cockpitStateField;
        private static PropertyInfo? _currentStateProperty;
        private static FieldInfo? _panViewField;
        private static FieldInfo? _tiltViewField;
        private static object? _cockpitModeValue;

        internal static bool ShouldProtectVanillaCameraFrame()
        {
            int frame = Time.frameCount;
            if (frame != _frameId)
            {
                _frameId = frame;
                _frameProtect = ComputeProtect();
                if (_frameProtect != _lastProtect)
                {
                    _lastProtect = _frameProtect;
                    if (RuntimeConfig.RingLogEnabled)
                    {
                        RingBufferLog.WriteAscii("[TrackIrSafe] protect="
                            + (_frameProtect ? "on" : "off"));
                    }
                }
            }

            return _frameProtect;
        }

        internal static bool ShouldSkipHeavyFrameWork() => ShouldProtectVanillaCameraFrame();

        private static bool ComputeProtect()
        {
            if (!RuntimeConfig.TrackIrSafeModeEnabled)
                return false;

            if (!IsCockpitCameraMode())
                return false;

            if (IsTrackIrEnabled())
                return true;

            return IsExtremeCockpitLook();
        }

        private static bool IsCockpitCameraMode()
        {
            EnsureInitialized();
            if (_cameraModeField == null || _cockpitModeValue == null)
                return TryGetCockpitState(out _);

            object? mode = _cameraModeField.GetValue(null);
            return mode != null && mode.Equals(_cockpitModeValue);
        }

        private static bool IsTrackIrEnabled()
        {
            EnsureInitialized();
            if (_useTrackIrField == null)
                return false;

            object? value = _useTrackIrField.GetValue(null);
            return value is bool enabled && enabled;
        }

        private static bool IsExtremeCockpitLook()
        {
            if (!TryGetCockpitState(out object? cockpitState) || cockpitState == null)
                return false;

            EnsureInitialized();
            if (_panViewField != null)
            {
                object? pan = _panViewField.GetValue(cockpitState);
                if (pan != null && Math.Abs(Convert.ToSingle(pan)) >= ExtremePanDegrees)
                    return true;
            }

            if (_tiltViewField != null)
            {
                object? tilt = _tiltViewField.GetValue(cockpitState);
                if (tilt != null && Math.Abs(Convert.ToSingle(tilt)) >= ExtremeTiltDegrees)
                    return true;
            }

            return false;
        }

        private static bool TryGetCockpitState(out object? cockpitState)
        {
            cockpitState = null;
            EnsureInitialized();

            object? csm = EngineTweakerGameAccess.TryGetCameraStateManager();
            if (csm == null || _cockpitStateField == null)
                return false;

            object? cockpit = _cockpitStateField.GetValue(csm);
            if (cockpit == null)
                return false;

            if (_currentStateProperty != null)
            {
                object? current = _currentStateProperty.GetValue(csm);
                if (!ReferenceEquals(current, cockpit))
                    return false;
            }

            cockpitState = cockpit;
            return true;
        }

        internal static void ResetState()
        {
            _lastProtect = false;
            _frameId = -1;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            Type? playerSettings = GameTypeCache.Resolve("PlayerSettings");
            _useTrackIrField = playerSettings?.GetField("useTrackIR", BindingFlags.Static | BindingFlags.Public);

            Type? csmType = GameTypeCache.Resolve("CameraStateManager");
            _cameraModeField = csmType?.GetField("cameraMode", BindingFlags.Static | BindingFlags.Public);
            _currentStateProperty = csmType?.GetProperty("currentState", BindingFlags.Instance | BindingFlags.Public);
            _cockpitStateField = csmType?.GetField("cockpitState", BindingFlags.Instance | BindingFlags.Public);

            Type? cameraModeType = GameTypeCache.Resolve("CameraMode");
            if (cameraModeType != null && cameraModeType.IsEnum)
                _cockpitModeValue = Enum.Parse(cameraModeType, "cockpit");

            Type? cockpitType = GameTypeCache.Resolve("CameraCockpitState");
            _panViewField = cockpitType?.GetField("panView", BindingFlags.Instance | BindingFlags.NonPublic);
            _tiltViewField = cockpitType?.GetField("tiltView", BindingFlags.Instance | BindingFlags.NonPublic);

            _initialized = true;
        }
    }
}
