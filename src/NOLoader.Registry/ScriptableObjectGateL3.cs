using System;
using System.Reflection;
using NOLoader.API;

namespace NOLoader.Registry
{
    /// <summary>Gate L3 metadata checks — no Unity ECall (unit-test safe).</summary>
    public static class ScriptableObjectGateL3
    {
        public static bool ValidateMissile(ref MissileEntry entry)
        {
            return ValidateMissile(ref entry, out string? _);
        }

        public static bool ValidateMissile(ref MissileEntry entry, out string? reason)
        {
            reason = null;
            if (entry.Definition == null)
            {
                reason = "Definition is null";
                return false;
            }

            if (entry.JsonKeyHash == 0)
            {
                reason = "JsonKeyHash is zero";
                return false;
            }

            return ScriptableObjectGateL3Unity.ValidateMissilePrefab(ref entry, out reason);
        }

        public static bool ValidateWeaponMount(ref WeaponMountEntry entry)
        {
            return ValidateWeaponMount(ref entry, out string? _);
        }

        public static bool ValidateWeaponMount(ref WeaponMountEntry entry, out string? reason)
        {
            reason = null;
            if (entry.Asset == null)
            {
                reason = "Asset is null";
                return false;
            }

            if (entry.JsonKeyHash == 0)
            {
                reason = "JsonKeyHash is zero";
                return false;
            }

            Type assetType = entry.Asset.GetType();
            FieldInfo? jsonKeyField = assetType.GetField("jsonKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (jsonKeyField?.GetValue(entry.Asset) is string jsonKey && string.IsNullOrWhiteSpace(jsonKey))
            {
                reason = "jsonKey empty on WeaponMount";
                return false;
            }

            MethodInfo? init = assetType.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public);
            if (init == null)
            {
                reason = "WeaponMount.Initialize missing";
                return false;
            }

            return true;
        }

        public static bool ValidateAircraft(ref AircraftEntry entry)
        {
            return ValidateAircraft(ref entry, out string? _);
        }

        public static bool ValidateAircraft(ref AircraftEntry entry, out string? reason)
        {
            reason = null;
            if (entry.Definition == null)
            {
                reason = "Definition is null";
                return false;
            }

            if (entry.JsonKeyHash == 0)
            {
                reason = "JsonKeyHash is zero";
                return false;
            }

            return ScriptableObjectGateL3Unity.ValidateAircraftPrefab(ref entry, out reason);
        }
    }
}
