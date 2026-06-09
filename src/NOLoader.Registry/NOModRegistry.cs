using System;
using System.Collections.Generic;
using System.Reflection;
using NOLoader.API;
namespace NOLoader.Registry
{
    public struct WeaponMountEntry
    {
        public int JsonKeyHash;
        public int DisplayNameHash;
        public UnityEngine.Object? Asset;
    }

    public struct MissileEntry
    {
        public int JsonKeyHash;
        public UnityEngine.Object? Definition;
        public UnityEngine.Mesh? Mesh;
        public UnityEngine.Object? EngineSpawnPoint;
    }

    public struct AircraftEntry
    {
        public int JsonKeyHash;
        public UnityEngine.Object? Definition;
    }

    public static class NOModRegistry
    {
        private static readonly List<WeaponMountEntry> WeaponMounts = new List<WeaponMountEntry>();
        private static readonly List<MissileEntry> Missiles = new List<MissileEntry>();
        private static readonly List<AircraftEntry> Aircraft = new List<AircraftEntry>();
        private static bool _initialized;

        public static void Initialize()
        {
            _initialized = true;
        }

        public static bool RegisterWeaponMount(ref WeaponMountEntry entry)
        {
            if (!_initialized) return false;
            if (!ScriptableObjectGateL3.ValidateWeaponMount(ref entry))
                return false;
            int key = entry.JsonKeyHash;
            if (key == 0) return false;
            if (WeaponMounts.Exists(w => w.JsonKeyHash == key)) return false;
            WeaponMounts.Add(entry);
            return true;
        }

        public static bool RegisterMissile(ref MissileEntry entry)
        {
            if (!_initialized) return false;
            if (!ScriptableObjectGateL3.ValidateMissile(ref entry))
                return false;
            int key = entry.JsonKeyHash;
            if (key == 0) return false;
            if (Missiles.Exists(m => m.JsonKeyHash == key)) return false;
            Missiles.Add(entry);
            return true;
        }

        public static bool RegisterAircraft(ref AircraftEntry entry)
        {
            if (!_initialized) return false;
            if (!ScriptableObjectGateL3.ValidateAircraft(ref entry))
                return false;
            int key = entry.JsonKeyHash;
            if (key == 0) return false;
            if (Aircraft.Exists(a => a.JsonKeyHash == key)) return false;
            Aircraft.Add(entry);
            return true;
        }

        public static IReadOnlyList<WeaponMountEntry> GetWeaponMounts() => WeaponMounts;
        public static IReadOnlyList<MissileEntry> GetMissiles() => Missiles;
        public static IReadOnlyList<AircraftEntry> GetAircraft() => Aircraft;

        /// <summary>Push registered entries into live Encyclopedia (after MainMenu load or mod registration).</summary>
        public static bool ApplyToEncyclopedia()
        {
            if (!_initialized)
                return false;

            try
            {
                Type? encType = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    encType = asm.GetType("Encyclopedia", throwOnError: false);
                    if (encType != null)
                        break;
                }

                if (encType == null)
                    return false;

                PropertyInfo? instProp = encType.GetProperty("i", BindingFlags.Public | BindingFlags.Static);
                object? encyclopedia = instProp?.GetValue(null);
                if (encyclopedia == null)
                    return false;

                RegistryGameBridge.OnEncyclopediaAfterLoad(encyclopedia);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
