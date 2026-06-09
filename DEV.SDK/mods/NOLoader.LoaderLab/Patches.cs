using System;
using System.Collections.Generic;
using System.Reflection;
using NOLoader.Registry;
using UnityEngine;

namespace NOLoader.LoaderLab
{
    public static class Patches
    {
        private static readonly FieldInfo? GunFireRateField =
            typeof(Gun).GetField("fireRate", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunFireIntervalField =
            typeof(Gun).GetField("fireInterval", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunHeatField =
            typeof(Gun).GetField("heat", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunHeatEnabledField =
            typeof(Gun).GetField("heatEnabled", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunTracerRatioField =
            typeof(Gun).GetField("tracerRatio", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunTracerSizeField =
            typeof(Gun).GetField("tracerSize", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? GunTracerColorField =
            typeof(Gun).GetField("tracerColor", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Type? GunHeatType =
            typeof(Gun).GetNestedType("Heat", BindingFlags.NonPublic);

        private static readonly FieldInfo? HeatBaseFireRateField =
            GunHeatType?.GetField("baseFireRate", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? EngineThrustField =
            typeof(Missile).GetField("engineCurrentThrust", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MissileMotorsField =
            typeof(Missile).GetField("motors", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MotorThrustField =
            typeof(Missile).GetNestedType("Motor", BindingFlags.NonPublic)
                ?.GetField("thrust", BindingFlags.Instance | BindingFlags.Public);

        private static readonly FieldInfo? BlastYieldField =
            typeof(Missile).GetField("blastYield", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Dictionary<int, float> GunBaseFireRates = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> MotorBaseThrust = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> MissileBaseYield = new Dictionary<int, float>();
        private static readonly HashSet<int> ScaledMissiles = new HashSet<int>();

        private static readonly Color HyperTracerColor = new Color(0.2f, 1f, 0.35f, 1f);

        public static void GunAwakePostfix(Gun gun)
        {
            if (!LoaderLabState.Active)
                return;

            CaptureGunBaseFireRate(gun);
            ApplyHyperFireRate(gun);
            ApplyHyperHeatBase(gun);
            ApplyHyperTracerVisuals(gun);
        }

        public static void GunFixedUpdatePostfix(Gun gun)
        {
            if (!LoaderLabState.Active)
                return;

            ApplyHyperFireRate(gun);
        }

        public static void GunSpawnBulletPostfix(Gun gun)
        {
            if (!LoaderLabState.Active || gun == null)
                return;

            ApplyHyperTracerVisuals(gun);
        }

        public static void MissileOnEnablePostfix(Missile missile)
        {
            if (!LoaderLabState.Active || missile == null)
                return;

            int id = missile.GetInstanceID();
            if (!ScaledMissiles.Add(id))
                return;

            ApplyMissileMotorBoost(missile);
            ScaleMissileYield(missile);
        }

        public static void DetonatePrefix(Missile missile, Vector3 normal, bool hitArmor, bool hitTerrain)
        {
            if (!LoaderLabState.Active || missile == null || BlastYieldField == null)
                return;

            ScaleMissileYield(missile);
        }

        public static void MotorThrustPostfix(Missile missile)
        {
            if (missile == null || EngineThrustField == null)
                return;

            float thrust = (float)EngineThrustField.GetValue(missile);
            float safe = PhysicsSafetyCatch.SanitizeForce(thrust);
            if (Math.Abs(thrust - safe) > 0.01f)
                LoaderLabState.RecordPhysicsCatch();

            if (LoaderLabState.Active)
                safe *= LoaderLabConfig.MissileThrustScale;

            EngineThrustField.SetValue(missile, safe);
        }

        private static void CaptureGunBaseFireRate(Gun gun)
        {
            if (GunFireRateField == null)
                return;

            int id = gun.GetInstanceID();
            if (GunBaseFireRates.ContainsKey(id))
                return;

            float rate = (float)GunFireRateField.GetValue(gun);
            if (rate > 0f)
                GunBaseFireRates[id] = rate;
        }

        private static void ApplyHyperFireRate(Gun gun)
        {
            if (GunFireRateField == null || GunFireIntervalField == null)
                return;

            int id = gun.GetInstanceID();
            if (!GunBaseFireRates.TryGetValue(id, out float original))
            {
                original = (float)GunFireRateField.GetValue(gun);
                if (original <= 0f)
                    return;
                GunBaseFireRates[id] = original;
            }

            float scaled = original * LoaderLabConfig.FireRateScale;
            GunFireRateField.SetValue(gun, scaled);
            GunFireIntervalField.SetValue(gun, 60f / scaled);
        }

        private static void ApplyHyperHeatBase(Gun gun)
        {
            if (GunHeatField == null || HeatBaseFireRateField == null || GunHeatEnabledField == null)
                return;

            if (!(bool)GunHeatEnabledField.GetValue(gun))
                return;

            if (GunHeatField.GetValue(gun) is not object heatBox)
                return;

            int id = gun.GetInstanceID();
            if (!GunBaseFireRates.TryGetValue(id, out float original))
                return;

            HeatBaseFireRateField.SetValue(heatBox, original * LoaderLabConfig.FireRateScale);
        }

        private static void ApplyHyperTracerVisuals(Gun gun)
        {
            if (GunTracerRatioField != null)
                GunTracerRatioField.SetValue(gun, 1);

            if (GunTracerSizeField != null)
            {
                float size = (float)GunTracerSizeField.GetValue(gun);
                if (size < 0.5f)
                    size = 1f;
                GunTracerSizeField.SetValue(gun, size * LoaderLabConfig.TracerSizeScale);
            }

            if (GunTracerColorField != null)
                GunTracerColorField.SetValue(gun, HyperTracerColor);
        }

        private static void ApplyMissileMotorBoost(Missile missile)
        {
            if (MissileMotorsField == null || MotorThrustField == null)
                return;

            if (!(MissileMotorsField.GetValue(missile) is Array motors))
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

                MotorThrustField.SetValue(motor, original * LoaderLabConfig.MissileThrustScale);
            }
        }

        private static void ScaleMissileYield(Missile missile)
        {
            if (BlastYieldField == null)
                return;

            int id = missile.GetInstanceID();
            if (!MissileBaseYield.TryGetValue(id, out float original))
            {
                original = (float)BlastYieldField.GetValue(missile);
                MissileBaseYield[id] = original;
            }

            BlastYieldField.SetValue(missile, original * LoaderLabConfig.ExplosionYieldScale);
        }
    }
}
