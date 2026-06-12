using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NOLoader.API;
using UnityEngine;

namespace NOLoader.Registry
{
    /// <summary>Injects NOModRegistry content into Encyclopedia after game AfterLoad.</summary>
    public static class RegistryGameBridge
    {
        private static FieldInfo? _missilesField;
        private static FieldInfo? _weaponMountsField;
        private static FieldInfo? _aircraftField;
        private static FieldInfo? _indexLookupField;
        private static PropertyInfo? _lookupProp;
        private static PropertyInfo? _weaponLookupProp;
        private static PropertyInfo? _jsonKeyProp;
        private static PropertyInfo? _lookupIndexProp;
        private static MethodInfo? _cacheMassMethod;

        public static void OnEncyclopediaAfterLoad(object encyclopedia)
        {
            if (encyclopedia == null)
                return;

            try
            {
                EnsureReflection(encyclopedia.GetType());
                InjectList(_missilesField, encyclopedia, NOModRegistry.GetMissiles(), "missile");
                InjectList(_weaponMountsField, encyclopedia, NOModRegistry.GetWeaponMounts(), "weaponMount");
                InjectList(_aircraftField, encyclopedia, NOModRegistry.GetAircraft(), "aircraft");
                RebuildLookups(encyclopedia);
            }
            catch (Exception)
            {
                /* inject best-effort — faults surface via mod load / Gate L4 */
            }
        }

        private static void EnsureReflection(Type encyclopediaType)
        {
            if (_missilesField != null)
                return;

            BindingFlags inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _missilesField = encyclopediaType.GetField("missiles", inst);
            _weaponMountsField = encyclopediaType.GetField("weaponMounts", inst);
            _aircraftField = encyclopediaType.GetField("aircraft", inst);
            _indexLookupField = encyclopediaType.GetField("IndexLookup", inst);

            Type encType = encyclopediaType;
            _lookupProp = encType.GetProperty("Lookup", BindingFlags.Static | BindingFlags.Public);
            if (_lookupProp == null)
            {
                FieldInfo? lookupField = encType.GetField("Lookup", BindingFlags.Static | BindingFlags.Public);
                if (lookupField != null)
                    _lookupProp = lookupField.FieldType.GetProperty("Item");
            }

            _weaponLookupProp = encType.GetProperty("WeaponLookup", BindingFlags.Static | BindingFlags.Public);
            if (_weaponLookupProp == null)
            {
                FieldInfo? weaponLookupField = encType.GetField("WeaponLookup", BindingFlags.Static | BindingFlags.Public);
                if (weaponLookupField != null)
                    _weaponLookupProp = weaponLookupField.FieldType.GetProperty("Item");
            }

            _jsonKeyProp = encyclopediaType.Assembly.GetType("IHasJsonKey")?.GetProperty("JsonKey");
            _lookupIndexProp = encyclopediaType.Assembly.GetType("INetworkDefinition")?.GetProperty("LookupIndex");
            _cacheMassMethod = encyclopediaType.Assembly.GetType("UnitDefinition")?.GetMethod("CacheMass");
        }

        private static void InjectList<T>(FieldInfo? field, object encyclopedia, IReadOnlyList<T> entries, string kind)
        {
            if (field == null || entries.Count == 0)
                return;

            if (field.GetValue(encyclopedia) is not IList list)
                return;

            foreach (T entry in entries)
            {
                object? asset = ExtractAsset(entry);
                if (asset == null)
                    continue;

                if (_cacheMassMethod != null && kind != "weaponMount")
                {
                    try { _cacheMassMethod.Invoke(asset, null); }
                    catch { /* optional */ }
                }

                if (kind == "weaponMount")
                {
                    MethodInfo? init = asset.GetType().GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public);
                    try { init?.Invoke(asset, null); } catch { /* optional */ }
                }

                list.Add(asset);
            }
        }

        private static object? ExtractAsset<T>(T entry)
        {
            return entry switch
            {
                MissileEntry m => m.Definition,
                WeaponMountEntry w => w.Asset,
                AircraftEntry a => a.Definition,
                _ => null
            };
        }

        private static void RebuildLookups(object encyclopedia)
        {
            FieldInfo? lookupField = encyclopedia.GetType().GetField("Lookup", BindingFlags.Static | BindingFlags.Public);
            FieldInfo? weaponLookupField = encyclopedia.GetType().GetField("WeaponLookup", BindingFlags.Static | BindingFlags.Public);
            object? indexLookup = _indexLookupField?.GetValue(encyclopedia);

            if (lookupField?.GetValue(null) is IDictionary unitLookup)
            {
                AddToLookup(unitLookup, NOModRegistry.GetMissiles());
                AddToLookup(unitLookup, NOModRegistry.GetAircraft());
            }

            if (weaponLookupField?.GetValue(null) is IDictionary weaponLookup)
                AddToLookup(weaponLookup, NOModRegistry.GetWeaponMounts());

            if (indexLookup is IList indexList)
            {
                AddToIndex(indexList, NOModRegistry.GetMissiles());
                AddToIndex(indexList, NOModRegistry.GetWeaponMounts());
                AddToIndex(indexList, NOModRegistry.GetAircraft());
            }
        }

        private static void AddToLookup<T>(IDictionary lookup, IReadOnlyList<T> entries)
        {
            foreach (T entry in entries)
            {
                object? asset = ExtractAsset(entry);
                if (asset == null)
                    continue;

                string? key = ResolveLookupKey(entry, asset);
                if (string.IsNullOrEmpty(key) || lookup.Contains(key))
                    continue;

                lookup.Add(key, asset);
            }
        }

        private static string? ResolveLookupKey<T>(T entry, object asset)
        {
            string? key = ReadJsonKey(asset);
            if (!string.IsNullOrEmpty(key))
                return key;

            int hash = entry switch
            {
                MissileEntry m => m.JsonKeyHash,
                WeaponMountEntry w => w.JsonKeyHash,
                AircraftEntry a => a.JsonKeyHash,
                _ => 0
            };

            if (hash != 0 && StringHashTable.TryDecode(hash, out string? decoded))
                return decoded;

            return null;
        }

        private static void AddToIndex<T>(IList indexList, IReadOnlyList<T> entries)
        {
            foreach (T entry in entries)
            {
                object? asset = ExtractAsset(entry);
                if (asset == null)
                    continue;

                if (_lookupIndexProp != null)
                    _lookupIndexProp.SetValue(asset, indexList.Count);

                indexList.Add(asset);
            }
        }

        private static string? ReadJsonKey(object asset)
        {
            if (_jsonKeyProp != null)
                return _jsonKeyProp.GetValue(asset) as string;

            FieldInfo? field = asset.GetType().GetField("jsonKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(asset) as string;
        }
    }
}
