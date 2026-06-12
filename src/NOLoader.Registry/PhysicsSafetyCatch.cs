using System;

namespace NOLoader.Registry
{
    /// <summary>Physics Safety Catch — clamp invalid mod values before game physics.</summary>
    public static class PhysicsSafetyCatch
    {
        private const float MinMass = 0.001f;
        private const float MaxMass = 1000000f;
        private const float MaxForce = 1e8f;

        public static void Install()
        {
            // Cecil hook: Missile/Motor::Thrust — applied via CoreBootstrapPatches at startup.
        }

        public static float SanitizeMass(float mass)
        {
            if (float.IsNaN(mass) || float.IsInfinity(mass) || mass <= 0f)
                return MinMass;
            return Math.Min(Math.Max(mass, MinMass), MaxMass);
        }

        public static float SanitizeForce(float force)
        {
            if (float.IsNaN(force) || float.IsInfinity(force))
                return 0f;
            return Math.Min(Math.Max(force, -MaxForce), MaxForce);
        }

        public static UnityEngine.Vector3 SanitizeVector3(UnityEngine.Vector3 v)
        {
            if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z))
                return UnityEngine.Vector3.zero;
            if (float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                return UnityEngine.Vector3.zero;
            return v;
        }
    }
}
