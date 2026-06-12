using System;
using System.Collections.Generic;
using System.Reflection;
using NOLoader.RealWeaponNames.Services;
using NuclearOption.MissionEditorScripts;
using TMPro;
using UnityEngine.UI;

namespace NOLoader.RealWeaponNames
{
    internal static class Patches
    {
        private static FieldInfo F(Type t, string n) =>
            t.GetField(n, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static PropertyInfo P(Type t, string n) =>
            t.GetProperty(n, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        public static void WeaponMountInitializePostfix(WeaponMount __instance)
        {
            if (!RealWeaponNamesState.IsEnabled)
                return;

            PatchWeaponMount(__instance);
        }

        public static void WeaponManagerRegisterWeaponPostfix(Weapon weapon, WeaponMount weaponMount)
        {
            if (!RealWeaponNamesState.IsEnabled || weapon == null)
                return;

            if (weapon.info != null)
                WeaponDisplayNameResolver.ApplyWeaponInfoLabels(weapon.info);

            PatchWeaponMount(weaponMount);
        }

        public static void WeaponSelectorPopulateOptionsPostfix(WeaponSelector __instance)
        {
            if (!RealWeaponNamesState.IsSafeForUiPatch())
                return;

            WeaponSelectorUiHelper.RefreshDropdownLabels(__instance);
        }

        public static void WeaponSelectorSetValuePostfix(WeaponSelector __instance)
        {
            if (!RealWeaponNamesState.IsSafeForUiPatch())
                return;

            WeaponSelectorUiHelper.RefreshDropdownLabels(__instance);
        }

        public static void WeaponSelectorDropdownChangedPostfix(WeaponSelector __instance)
        {
            if (!RealWeaponNamesState.IsSafeForUiPatch())
                return;

            WeaponSelectorUiHelper.RefreshDropdownLabels(__instance);
        }

        public static void WeaponSelectorSetInteractablePostfix(WeaponSelector __instance)
        {
            if (!RealWeaponNamesState.IsSafeForUiPatch())
                return;

            WeaponSelectorUiHelper.RefreshDropdownLabels(__instance);
        }

        public static void LoadoutSelectorAssignAircraftPostfix(LoadoutSelector __instance) =>
            RefreshLoadoutSelectors(__instance);

        public static void LoadoutSelectorLoadDefaultsPostfix(LoadoutSelector __instance) =>
            RefreshLoadoutSelectors(__instance);

        public static void LoadoutSelectorUpdateWeaponsPostfix(LoadoutSelector __instance) =>
            RefreshLoadoutSelectors(__instance);

        public static void RadialMenuSetWeaponPostfix(RadialMenuAction __instance)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            __instance.DisplayName = WeaponDisplayNameResolver.Replace(__instance.DisplayName);
        }

        public static void RestrictedItemSetItemPostfix(RestrictedItem __instance)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            var itemName = F(typeof(RestrictedItem), "itemName")?.GetValue(__instance) as Text;
            if (itemName == null || string.IsNullOrEmpty(itemName.text))
                return;

            itemName.text = WeaponDisplayNameResolver.ReplaceInComposite(itemName.text);
        }

        public static void WeaponIndicatorRefreshPostfix(WeaponIndicator __instance)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            var aircraft = F(typeof(WeaponIndicator), "aircraft")?.GetValue(__instance);
            if (aircraft == null)
                return;

            var weaponManager = F(aircraft.GetType(), "weaponManager")?.GetValue(aircraft);
            if (weaponManager == null)
                return;

            var station = F(weaponManager.GetType(), "currentWeaponStation")?.GetValue(weaponManager);
            if (station == null)
                return;

            var weaponInfo = P(station.GetType(), "WeaponInfo")?.GetValue(station) as WeaponInfo;
            if (weaponInfo == null)
                return;

            var nameText = F(typeof(WeaponIndicator), "weaponName")?.GetValue(__instance) as Text;
            if (nameText == null)
                return;

            nameText.text = WeaponDisplayNameResolver.ResolveShortName(weaponInfo);
        }

        public static void WeaponStatusUpdateDisplayPostfix(WeaponStatus __instance)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            var station = F(typeof(WeaponStatus), "weaponStation")?.GetValue(__instance);
            if (station == null)
                return;

            var weaponInfo = P(station.GetType(), "WeaponInfo")?.GetValue(station) as WeaponInfo;
            if (weaponInfo == null)
                return;

            var cargo = (bool)(P(station.GetType(), "Cargo")?.GetValue(station) ?? false);
            var nameText = F(typeof(WeaponStatus), "nameText")?.GetValue(__instance) as Text;
            if (nameText == null)
                return;

            nameText.text = WeaponDisplayNameResolver.ResolveFullName(weaponInfo, cargo);
        }

        public static void TargetMarkerExtraSetupPostfix(TargetMarker __instance)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            var infoName = F(typeof(TargetMarker), "infoName")?.GetValue(__instance) as Text;
            if (infoName == null || string.IsNullOrEmpty(infoName.text))
                return;

            infoName.text = WeaponDisplayNameResolver.ReplaceInComposite(infoName.text);
        }

        public static void AircraftActionsReportPrefix(AircraftActionsReport __instance, ref string report, float displayTime)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch() || string.IsNullOrEmpty(report))
                return;

            if (!WeaponDisplayNameResolver.MightContainMappedName(report))
                return;

            report = WeaponDisplayNameResolver.ReplaceInComposite(report);
        }

        public static void EncyclopediaBrowserSpawnUnitPostfix(EncyclopediaBrowser __instance)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            var unitName = F(typeof(EncyclopediaBrowser), "unitName")?.GetValue(__instance) as TMP_Text;
            if (unitName == null || string.IsNullOrEmpty(unitName.text))
                return;

            unitName.text = WeaponDisplayNameResolver.ReplaceInComposite(unitName.text);
        }

        public static void EncyclopediaWeaponPanelUpdateTextPostfix(object __instance)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            var nameText = F(__instance.GetType(), "nameText")?.GetValue(__instance) as TMP_Text;
            if (nameText == null || string.IsNullOrEmpty(nameText.text))
                return;

            nameText.text = WeaponDisplayNameResolver.ReplaceInComposite(nameText.text);
        }

        public static void EncyclopediaAfterLoadPostfix(Encyclopedia __instance)
        {
            if (!RealWeaponNamesState.IsEnabled || __instance == null)
                return;

            PatchDefinitions(__instance.missiles);
            PatchDefinitions(__instance.otherUnits);
            PatchWeaponMounts(__instance.weaponMounts);
        }

        private static void RefreshLoadoutSelectors(LoadoutSelector instance)
        {
            if (!RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            var selectors = F(typeof(LoadoutSelector), "weaponSelectors")?.GetValue(instance) as List<WeaponSelector>;
            if (selectors == null)
                return;

            for (int i = 0; i < selectors.Count; i++)
                WeaponSelectorUiHelper.RefreshDropdownLabels(selectors[i]);
        }

        private static void PatchDefinitions<T>(List<T> definitions) where T : UnitDefinition
        {
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Count; i++)
                WeaponDisplayNameResolver.ApplyUnitDefinitionLabels(definitions[i]);
        }

        private static void PatchWeaponMounts(List<WeaponMount> weaponMounts)
        {
            if (weaponMounts == null)
                return;

            for (int i = 0; i < weaponMounts.Count; i++)
                PatchWeaponMount(weaponMounts[i]);
        }

        private static void PatchWeaponMount(WeaponMount weaponMount)
        {
            if (weaponMount == null)
                return;

            if (weaponMount.info != null)
                WeaponDisplayNameResolver.ApplyWeaponInfoLabels(weaponMount.info);

            if (!string.IsNullOrEmpty(weaponMount.mountName))
                weaponMount.mountName = WeaponDisplayNameResolver.ReplaceInComposite(weaponMount.mountName);
        }
    }
}
