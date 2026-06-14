using System;
using System.Reflection;
using NOLoader.Core.Interop;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class GroundVehicleVisualAccess
    {
        private static FieldInfo? _speedField;
        private static FieldInfo? _engineIdleField;
        private static FieldInfo? _engineDriveField;
        private static PropertyInfo? _rbProperty;
        private static PropertyInfo? _transformProperty;
        private static PropertyInfo? _positionProperty;
        private static PropertyInfo? _velocityProperty;
        private static PropertyInfo? _forwardProperty;
        private static PropertyInfo? _isClientOnlyProperty;
        private static bool _initialized;

        internal static void EnsureInitialized()
        {
            if (_initialized)
                return;

            Type? gvType = GameTypeCache.Resolve("GroundVehicle");
            _speedField = gvType?.GetField("speed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _engineIdleField = gvType?.GetField("engineIdleSound", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _engineDriveField = gvType?.GetField("engineDriveSound", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? unitType = GameTypeCache.Resolve("Unit");
            _rbProperty = unitType?.GetProperty("rb", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? component = typeof(Component);
            _transformProperty = component.GetProperty("transform", BindingFlags.Instance | BindingFlags.Public);

            Type? transform = typeof(Transform);
            _positionProperty = transform.GetProperty("position", BindingFlags.Instance | BindingFlags.Public);
            _forwardProperty = transform.GetProperty("forward", BindingFlags.Instance | BindingFlags.Public);

            Type? rbType = typeof(Rigidbody);
            _velocityProperty = rbType.GetProperty("velocity", BindingFlags.Instance | BindingFlags.Public);

            _isClientOnlyProperty = ResolveIsClientOnlyProperty(gvType);

            _initialized = true;
        }

        internal static void RunCheapClientUpdate(object instance)
        {
            EnsureInitialized();
            if (instance == null)
                return;

            if (IsClientOnly(instance))
                SyncClientSpeed(instance);

            StopEngineSounds(instance);
        }

        private static bool IsClientOnly(object instance)
        {
            if (_isClientOnlyProperty == null)
                return false;

            object? value = _isClientOnlyProperty.GetValue(instance);
            return value is bool clientOnly && clientOnly;
        }

        private static void SyncClientSpeed(object instance)
        {
            if (_speedField == null || _rbProperty == null || _transformProperty == null
                || _velocityProperty == null || _forwardProperty == null)
                return;

            object? rb = _rbProperty.GetValue(instance);
            object? transform = _transformProperty.GetValue(instance);
            if (rb == null || transform == null)
                return;

            object? velocityObj = _velocityProperty.GetValue(rb);
            object? forwardObj = _forwardProperty.GetValue(transform);
            if (velocityObj is not Vector3 velocity || forwardObj is not Vector3 forward)
                return;

            _speedField.SetValue(instance, Vector3.Dot(velocity, forward));
        }

        private static void StopEngineSounds(object instance)
        {
            StopAudioSource(_engineIdleField?.GetValue(instance));
            StopAudioSource(_engineDriveField?.GetValue(instance));
        }

        private static void StopAudioSource(object? audioSource)
        {
            if (audioSource == null)
                return;

            Type audioType = audioSource.GetType();
            PropertyInfo? isPlaying = audioType.GetProperty("isPlaying", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo? stop = audioType.GetMethod("Stop", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (isPlaying == null || stop == null)
                return;

            if (isPlaying.GetValue(audioSource) is bool playing && playing)
                stop.Invoke(audioSource, null);
        }

        private static PropertyInfo? ResolveIsClientOnlyProperty(Type? startType)
        {
            for (Type? type = startType; type != null; type = type.BaseType)
            {
                PropertyInfo? prop = type.GetProperty("IsClientOnly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                    return prop;
            }

            return null;
        }
    }
}
