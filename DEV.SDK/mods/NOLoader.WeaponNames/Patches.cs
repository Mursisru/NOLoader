using System.Collections.Generic;

namespace NOLoader.WeaponNames
{
    public static class Patches
    {
        /// <summary>Prefix on WeaponMount.Initialize — rename before mountName is built (aircraft loadout).</summary>
        public static void InitializePrefix(WeaponMount mount)
        {
            ApplyToWeaponInfo(mount?.info);
        }

        /// <summary>Postfix on Encyclopedia.AfterLoad — rename missile entries shown in encyclopedia browser.</summary>
        public static void AfterLoadPostfix(Encyclopedia instance)
        {
            if (instance?.missiles == null)
                return;

            foreach (MissileDefinition missile in instance.missiles)
                ApplyToUnitDefinition(missile);
        }

        private static void ApplyToWeaponInfo(WeaponInfo? info)
        {
            if (info == null || string.IsNullOrEmpty(info.weaponName))
                return;

            if (!WeaponNameTable.TryGetDisplayName(info.weaponName, out string displayName))
                return;

            info.weaponName = displayName;
        }

        private static void ApplyToUnitDefinition(UnitDefinition? definition)
        {
            if (definition == null)
                return;

            if (!string.IsNullOrEmpty(definition.unitName)
                && WeaponNameTable.TryGetDisplayName(definition.unitName, out string unitDisplay))
            {
                definition.unitName = unitDisplay;
            }

            if (!string.IsNullOrEmpty(definition.code)
                && WeaponNameTable.TryGetDisplayName(definition.code, out string codeDisplay))
            {
                definition.code = codeDisplay;
            }
        }
    }
}
