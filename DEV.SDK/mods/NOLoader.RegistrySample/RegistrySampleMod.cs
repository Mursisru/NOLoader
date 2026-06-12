using System;
using System.Collections;
using System.Reflection;
using NOLoader.API;
using NOLoader.Registry;
using UnityEngine;

namespace NOLoader.RegistrySample
{
    /// <summary>
    /// Registers alias hash keys pointing at existing Encyclopedia assets (no new SO files).
    /// Demonstrates NOModRegistry + RegistryGameBridge end-to-end.
    /// </summary>
    public sealed class RegistrySampleMod : INOMod
    {
        private const string HostName = "NOLoader.RegistrySample.Host";

        public void OnLoad(ref NOModContext ctx)
        {
            if (GameObject.Find(HostName) != null)
                return;

            LoaderLog.Info("RegistrySample waiting for Encyclopedia...");
            var host = new GameObject(HostName);
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<RegistrySampleHost>();
        }

        public void OnUnload(ref NOModContext ctx)
        {
            LoaderLog.Info("RegistrySample unloaded");
        }
    }

    internal sealed class RegistrySampleHost : MonoBehaviour
    {
        private void Start() => StartCoroutine(RegisterWhenReady());

        private IEnumerator RegisterWhenReady()
        {
            object? encyclopedia = null;
            for (int i = 0; i < 120 && encyclopedia == null; i++)
            {
                encyclopedia = TryGetEncyclopedia();
                if (encyclopedia == null)
                    yield return new WaitForSecondsRealtime(0.25f);
            }

            if (encyclopedia == null)
            {
                LoaderLog.Fail("registrysample.encyclopedia", "Encyclopedia.i not available");
                yield break;
            }

            int registered = RegisterAliases(encyclopedia);
            if (registered == 0)
            {
                LoaderLog.Fail("registrysample.register", "no entries registered");
                yield break;
            }

            if (!NOModRegistry.ApplyToEncyclopedia())
            {
                LoaderLog.Fail("registrysample.apply", "ApplyToEncyclopedia failed");
                yield break;
            }

            LoaderLog.Pass("registrysample.apply (" + registered + " aliases)");
            LoaderLog.Info("RegistrySample complete — missiles="
                + NOModRegistry.GetMissiles().Count
                + " weapons=" + NOModRegistry.GetWeaponMounts().Count
                + " aircraft=" + NOModRegistry.GetAircraft().Count);
        }

        private static object? TryGetEncyclopedia()
        {
            Type? encType = FindType("Encyclopedia");
            return encType?.GetProperty("i", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static int RegisterAliases(object encyclopedia)
        {
            int count = 0;
            string? missileErr = null;
            string? weaponErr = null;
            string? aircraftErr = null;
            Type encType = encyclopedia.GetType();

            object? missile = GetListFirst(encType, encyclopedia, "missiles");
            if (missile != null && TryBuildMissileAlias(missile, out MissileEntry missileEntry)
                && NOModRegistryPipeline.TryPublishMissile(ref missileEntry, out missileErr))
            {
                LoaderLog.Pass("registrysample.missile_alias");
                count++;
            }
            else if (missileErr != null)
                LoaderLog.Fail("registrysample.missile_alias", missileErr);

            object? weapon = GetListFirst(encType, encyclopedia, "weaponMounts");
            if (weapon != null && TryBuildWeaponAlias(weapon, out WeaponMountEntry weaponEntry)
                && NOModRegistryPipeline.TryPublishWeaponMount(ref weaponEntry, out weaponErr))
            {
                LoaderLog.Pass("registrysample.weapon_alias");
                count++;
            }
            else if (weaponErr != null)
                LoaderLog.Fail("registrysample.weapon_alias", weaponErr);

            object? aircraft = GetListFirst(encType, encyclopedia, "aircraft");
            if (aircraft != null && TryBuildAircraftAlias(aircraft, out AircraftEntry aircraftEntry)
                && NOModRegistryPipeline.TryPublishAircraft(ref aircraftEntry, out aircraftErr))
            {
                LoaderLog.Pass("registrysample.aircraft_alias");
                count++;
            }
            else if (aircraftErr != null)
                LoaderLog.Fail("registrysample.aircraft_alias", aircraftErr);

            return count;
        }

        private static bool TryBuildMissileAlias(object definition, out MissileEntry entry)
        {
            entry = default;
            PropertyInfo? prefabProp = definition.GetType().GetProperty("unitPrefab", BindingFlags.Public | BindingFlags.Instance);
            object? prefab = prefabProp?.GetValue(definition);
            if (prefab == null)
                return false;

            GameObject? go = prefab as GameObject;
            if (go == null)
                return false;

            Mesh? mesh = go.GetComponentInChildren<MeshFilter>()?.sharedMesh;
            Transform? enginePoint = go.transform;

            Type? missileType = FindType("Missile");
            if (missileType != null)
            {
                Component? missile = go.GetComponent(missileType);
                FieldInfo? motorsField = missileType.GetField("motors", BindingFlags.NonPublic | BindingFlags.Instance);
                if (motorsField?.GetValue(missile) is Array motors && motors.Length > 0)
                {
                    object? motor = motors.GetValue(0);
                    FieldInfo? transformField = motor?.GetType().GetField("transform", BindingFlags.Public | BindingFlags.Instance);
                    if (transformField?.GetValue(motor) is Transform t)
                        enginePoint = t;
                }
            }

            string aliasKey = "noloader.registrysample.missile.alias";
            entry = new MissileEntry
            {
                JsonKeyHash = StringHash.Murmur32(aliasKey),
                Definition = definition as UnityEngine.Object,
                Mesh = mesh,
                EngineSpawnPoint = enginePoint
            };
            return entry.Definition != null && entry.Mesh != null && entry.EngineSpawnPoint != null;
        }

        private static bool TryBuildWeaponAlias(object weaponMount, out WeaponMountEntry entry)
        {
            entry = default;
            string aliasKey = "noloader.registrysample.weapon.alias";
            entry = new WeaponMountEntry
            {
                JsonKeyHash = StringHash.Murmur32(aliasKey),
                DisplayNameHash = StringHash.Murmur32(aliasKey + ".name"),
                Asset = weaponMount as UnityEngine.Object
            };
            return entry.Asset != null;
        }

        private static bool TryBuildAircraftAlias(object definition, out AircraftEntry entry)
        {
            entry = default;
            string aliasKey = "noloader.registrysample.aircraft.alias";
            entry = new AircraftEntry
            {
                JsonKeyHash = StringHash.Murmur32(aliasKey),
                Definition = definition as UnityEngine.Object
            };
            return entry.Definition != null;
        }

        private static object? GetListFirst(Type encType, object encyclopedia, string fieldName)
        {
            FieldInfo? field = encType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field?.GetValue(encyclopedia) is IList list && list.Count > 0)
                return list[0];
            return null;
        }

        private static Type? FindType(string name)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t = asm.GetType(name, throwOnError: false);
                if (t != null)
                    return t;
            }
            return null;
        }
    }
}
