using System;
using System.Reflection;
using NOLoader.Core.Interop;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class FpsAdaptiveDetailGovernor
    {
        private const float WindowSeconds = 1.5f;
        private const float ThrottleBelowFps = 57f;
        private const float RecoverAboveFps = 63f;
        private const float ThrottledTreeMul = 0.55f;

        private static bool _initialized;
        private static bool _throttleActive;
        private static float _userTreeMul = 1f;
        private static float _windowTimer;
        private static float _fpsAccum;
        private static int _fpsSamples;
        private static int _detailSkipLogCount;

        private static FieldInfo? _detailRendererSingletonField;
        private static FieldInfo? _treeRendererField;
        private static FieldInfo? _treeRangeMulField;
        private static FieldInfo? _detailSettingsField;
        private static PropertyInfo? _treeRangeProperty;

        internal static bool ThrottleActive => _throttleActive;

        internal static void Reset()
        {
            _throttleActive = false;
            _windowTimer = 0f;
            _fpsAccum = 0f;
            _fpsSamples = 0;
            _detailSkipLogCount = 0;
            _initialized = false;
        }

        internal static bool ShouldSkipDetailLateUpdate() => false;

        internal static void ForceRestoreIfThrottled()
        {
            if (!_throttleActive)
                return;

            DeactivateThrottle();
        }

        internal static void Tick()
        {
            if (!RuntimeConfig.FpsAdaptiveDetailEnabled)
                return;

            EnsureInitialized();

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            _windowTimer += dt;
            _fpsAccum += 1f / dt;
            _fpsSamples++;

            if (_windowTimer < WindowSeconds)
                return;

            float avgFps = _fpsSamples > 0 ? _fpsAccum / _fpsSamples : 60f;
            _windowTimer = 0f;
            _fpsAccum = 0f;
            _fpsSamples = 0;

            if (!_throttleActive && avgFps < ThrottleBelowFps)
                ActivateThrottle();
            else if (_throttleActive && avgFps >= RecoverAboveFps)
                DeactivateThrottle();
        }

        private static void ActivateThrottle()
        {
            _throttleActive = true;
            ReadUserTreeMultiplier();
            ApplyTreeMultiplier(System.Math.Min(_userTreeMul, ThrottledTreeMul));

            if (_detailSkipLogCount < 3)
            {
                _detailSkipLogCount++;
                RingBufferLog.WriteAscii("[EngineTweaker] adaptive_detail=on treeMul="
                    + ThrottledTreeMul.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private static void DeactivateThrottle()
        {
            _throttleActive = false;
            ApplyTreeMultiplier(_userTreeMul);

            if (_detailSkipLogCount <= 3)
            {
                RingBufferLog.WriteAscii("[EngineTweaker] adaptive_detail=off treeMul="
                    + _userTreeMul.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private static void ReadUserTreeMultiplier()
        {
            EnsureInitialized();
            if (_detailSettingsField == null || _treeRangeProperty == null)
            {
                _userTreeMul = 1f;
                return;
            }

            object? settings = _detailSettingsField.GetValue(null);
            if (settings == null)
            {
                _userTreeMul = 1f;
                return;
            }

            object? value = _treeRangeProperty.GetValue(settings);
            _userTreeMul = value == null ? 1f : Convert.ToSingle(value);
        }

        private static void ApplyTreeMultiplier(float multiplier)
        {
            object? detailRenderer = ResolveDetailRenderer();
            if (detailRenderer == null || _treeRendererField == null || _treeRangeMulField == null)
                return;

            object? treeRenderer = _treeRendererField.GetValue(detailRenderer);
            if (treeRenderer == null)
                return;

            _treeRangeMulField.SetValue(treeRenderer, multiplier);
        }

        private static object? ResolveDetailRenderer()
        {
            EnsureInitialized();
            if (_detailRendererSingletonField == null)
                return null;

            return _detailRendererSingletonField.GetValue(null);
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            Type? detailRendererType = GameTypeCache.Resolve("NuclearOption.Effects.DetailRenderer");
            if (detailRendererType == null)
                detailRendererType = GameTypeCache.Resolve("DetailRenderer");

            _detailRendererSingletonField = detailRendererType?.GetField("i", BindingFlags.Static | BindingFlags.Public);
            _treeRendererField = detailRendererType?.GetField("treeRenderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? treeRendererType = GameTypeCache.Resolve("NuclearOption.Effects.TreeRenderer");
            if (treeRendererType == null)
                treeRendererType = GameTypeCache.Resolve("TreeRenderer");

            _treeRangeMulField = treeRendererType?.GetField("TreeRangeMultiplier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? playerSettings = GameTypeCache.Resolve("PlayerSettings");
            _detailSettingsField = playerSettings?.GetField("DetailSettings", BindingFlags.Static | BindingFlags.Public);

            Type? detailSettingsType = GameTypeCache.Resolve("NuclearOption.Effects.DetailSettings");
            if (detailSettingsType == null)
                detailSettingsType = GameTypeCache.Resolve("DetailSettings");

            _treeRangeProperty = detailSettingsType?.GetProperty("TreeRangeMultiplier", BindingFlags.Instance | BindingFlags.Public);

            _initialized = true;
        }
    }
}
