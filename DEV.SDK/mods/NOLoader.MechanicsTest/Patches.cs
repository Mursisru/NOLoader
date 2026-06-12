using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NOLoader.MechanicsTest
{
    public static class Patches
    {
        private static readonly FieldInfo? GunFireRateField =
            typeof(Gun).GetField("fireRate", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunFireIntervalField =
            typeof(Gun).GetField("fireInterval", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunMuzzleVelocityField =
            typeof(Gun).GetField("muzzleVelocity", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunHeatField =
            typeof(Gun).GetField("heat", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunHeatEnabledField =
            typeof(Gun).GetField("heatEnabled", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Type? GunHeatType =
            typeof(Gun).GetNestedType("Heat", BindingFlags.NonPublic);

        private static readonly FieldInfo? HeatBaseFireRateField =
            GunHeatType?.GetField("baseFireRate", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MissileMotorsField =
            typeof(Missile).GetField("motors", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MotorThrustField =
            typeof(Missile).GetNestedType("Motor", BindingFlags.NonPublic)
                ?.GetField("thrust", BindingFlags.Instance | BindingFlags.Public);

        private static readonly Dictionary<int, float> GunBaseFireRates = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> WeaponInfoBaseMuzzleVelocity = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> MotorBaseThrust = new Dictionary<int, float>();
        private static readonly HashSet<int> ScaledMissileInstances = new HashSet<int>();

        public static void GunAwakePostfix(Gun gun)
        {
            CaptureGunBaseFireRate(gun);
            ApplyGunFireRate(gun);
            ApplyGunMuzzleVelocity(gun);
            ApplyGunHeatBaseFireRate(gun);
        }

        /// <summary>After heat.Update inside FixedUpdate — re-apply scale every physics tick.</summary>
        public static void GunFixedUpdatePostfix(Gun gun)
        {
            ApplyGunFireRate(gun);
            ApplyGunMuzzleVelocity(gun);
        }

        public static void WeaponMountInitializePostfix(WeaponMount mount)
        {
            ApplyWeaponMount(mount);
        }

        public static void EncyclopediaAfterLoadPostfix(Encyclopedia instance)
        {
            if (instance?.weaponMounts == null)
                return;

            foreach (WeaponMount mount in instance.weaponMounts)
                ApplyWeaponMount(mount);
        }

        public static void MissileOnEnablePostfix(Missile missile)
        {
            if (missile == null)
                return;

            int id = missile.GetInstanceID();
            if (!ScaledMissileInstances.Add(id))
                return;

            ApplyMissileMotors(missile);
        }

        private static void CaptureGunBaseFireRate(Gun gun)
        {
            if (gun == null || GunFireRateField == null)
                return;

            int id = gun.GetInstanceID();
            if (GunBaseFireRates.ContainsKey(id))
                return;

            float rate = (float)GunFireRateField.GetValue(gun);
            if (rate > 0f)
                GunBaseFireRates[id] = rate;
        }

        private static void ApplyGunFireRate(Gun gun)
        {
            if (gun == null || GunFireRateField == null || GunFireIntervalField == null)
                return;

            int id = gun.GetInstanceID();
            if (!GunBaseFireRates.TryGetValue(id, out float original))
            {
                original = (float)GunFireRateField.GetValue(gun);
                if (original <= 0f)
                    return;
                GunBaseFireRates[id] = original;
            }

            float scaled = original * MechanicsConfig.FireRateScale;
            GunFireRateField.SetValue(gun, scaled);
            GunFireIntervalField.SetValue(gun, 60f / scaled);
        }

        private static void ApplyGunHeatBaseFireRate(Gun gun)
        {
            if (gun == null || GunHeatField == null || HeatBaseFireRateField == null || GunHeatEnabledField == null)
                return;

            if (!(bool)GunHeatEnabledField.GetValue(gun))
                return;

            if (GunHeatField.GetValue(gun) is not object heatBox)
                return;

            int id = gun.GetInstanceID();
            if (!GunBaseFireRates.TryGetValue(id, out float original))
                return;

            HeatBaseFireRateField.SetValue(heatBox, original * MechanicsConfig.FireRateScale);
        }

        private static void ApplyGunMuzzleVelocity(Gun gun)
        {
            if (gun == null || gun.info == null)
                return;

            ScaleWeaponInfoMuzzleVelocity(gun.info);

            if (GunMuzzleVelocityField != null)
                GunMuzzleVelocityField.SetValue(gun, gun.info.muzzleVelocity);
        }

        private static void ApplyWeaponMount(WeaponMount mount)
        {
            if (mount == null)
                return;

            if (mount.info != null)
            {
                ScaleWeaponInfoMuzzleVelocity(mount.info);
                mount.info.maxSpeed = -1f;
            }

            if (mount.info?.weaponPrefab != null)
                ApplyMissileMotors(mount.info.weaponPrefab.GetComponent<Missile>());
        }

        private static void ScaleWeaponInfoMuzzleVelocity(WeaponInfo info)
        {
            int id = info.GetInstanceID();
            if (!WeaponInfoBaseMuzzleVelocity.TryGetValue(id, out float original))
            {
                original = info.muzzleVelocity;
                WeaponInfoBaseMuzzleVelocity[id] = original;
            }

            info.muzzleVelocity = original * MechanicsConfig.MuzzleVelocityScale;
        }

        private static void ApplyMissileMotors(Missile? missile)
        {
            if (missile == null || MissileMotorsField == null || MotorThrustField == null)
                return;

            if (!(MissileMotorsField.GetValue(missile) is System.Array motors))
                return;

            int missileId = missile.GetInstanceID();
            for (int i = 0; i < motors.Length; i++)
            {
                object? motor = motors.GetValue(i);
                if (motor == null)
                    continue;

                int key = unchecked(missileId * 397 ^ i);
                if (!MotorBaseThrust.TryGetValue(key, out float original))
                {
                    original = (float)MotorThrustField.GetValue(motor)!;
                    MotorBaseThrust[key] = original;
                }

                MotorThrustField.SetValue(motor, original * MechanicsConfig.MissileThrustScale);
            }
        }
    }
}
