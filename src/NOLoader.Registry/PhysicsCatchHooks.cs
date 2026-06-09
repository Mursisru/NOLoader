using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using UnityEngine;

namespace NOLoader.Registry
{
    public static class PhysicsCatchHooks
    {
        private const float MaxForce = 1e8f;
        private const float MinMass = 0.001f;
        private const float MaxMass = 1000000f;

        private static FieldInfo? _motorThrust;
        private static FieldInfo? _motorFuelMass;
        private static FieldInfo? _motorBurnRate;
        private static FieldInfo? _missileRb;
        private static int _interceptCount;

        public static int InterceptCount => _interceptCount;

        public static void ThrustPrefix(object motor, object missile, bool localSim, Vector3 inputs, float throttle)
        {
            if (motor == null || missile == null)
                return;

            EnsureMotorFields(motor.GetType(), missile.GetType());
            if (_motorThrust == null)
                return;

            float thrust = (float)_motorThrust.GetValue(motor)!;
            if (!IsFiniteForce(thrust))
            {
                _motorThrust.SetValue(motor, SanitizeForce(thrust));
                _interceptCount++;
                return;
            }

            if (_motorFuelMass != null)
            {
                float fuel = (float)_motorFuelMass.GetValue(motor)!;
                if (!IsFiniteMass(fuel))
                {
                    _motorFuelMass.SetValue(motor, SanitizeMass(fuel));
                    _interceptCount++;
                }
            }

            if (_motorBurnRate != null)
            {
                float burn = (float)_motorBurnRate.GetValue(motor)!;
                if (burn < 0f || !IsFinite(burn))
                {
                    _motorBurnRate.SetValue(motor, 0f);
                    _interceptCount++;
                }
            }

            if (_missileRb != null)
            {
                object? rb = _missileRb.GetValue(missile);
                if (rb is Rigidbody rigidbody)
                {
                    float mass = rigidbody.mass;
                    if (!IsFiniteMass(mass))
                    {
                        rigidbody.mass = SanitizeMass(mass);
                        _interceptCount++;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RigidbodyAddForcePrefixSkip(object rb, Vector3 force, ForceMode mode)
        {
            if (IsLikelyValidForce(force))
                return true;

            _interceptCount++;
            if (rb is Rigidbody body)
                body.AddForce(SanitizeVector3(force), mode);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RigidbodyAddForceSinglePrefixSkip(object rb, Vector3 force)
        {
            if (IsLikelyValidForce(force))
                return true;

            _interceptCount++;
            if (rb is Rigidbody body)
                body.AddForce(SanitizeVector3(force));
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RigidbodySetMassPrefixSkip(object rb, float mass)
        {
            if (IsFiniteMass(mass))
                return true;

            _interceptCount++;
            if (rb is Rigidbody body)
                body.mass = SanitizeMass(mass);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLikelyValidForce(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z)
                && v.x <= MaxForce && v.x >= -MaxForce
                && v.y <= MaxForce && v.y >= -MaxForce
                && v.z <= MaxForce && v.z >= -MaxForce;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteForce(float v) => IsFinite(v) && v <= MaxForce && v >= -MaxForce;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteMass(float v) => IsFinite(v) && v >= MinMass && v <= MaxMass;

        private static float SanitizeMass(float mass)
            => PhysicsSafetyCatch.SanitizeMass(mass);

        private static float SanitizeForce(float force)
            => PhysicsSafetyCatch.SanitizeForce(force);

        private static Vector3 SanitizeVector3(Vector3 v)
            => PhysicsSafetyCatch.SanitizeVector3(v);

        private static void EnsureMotorFields(Type motorType, Type missileType)
        {
            if (_motorThrust != null)
                return;

            BindingFlags inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _motorThrust = motorType.GetField("thrust", inst);
            _motorFuelMass = motorType.GetField("fuelMass", inst);
            _motorBurnRate = motorType.GetField("burnRate", inst);
            _missileRb = missileType.GetField("rb", inst);
        }
    }
}
