using Mirage;
using NuclearOption.Networking;
using NuclearOption.SavedMission;

namespace NOLoader.UniversalLoadout
{
    internal static class Patches
    {
        /// <summary>Before dropdown fill — add encyclopedia weapons to hardpoint options.</summary>
        public static void GetAvailableWeaponsPrefix(
            Player? player,
            HardpointSet hardpointSet,
            Airbase? airbase,
            FactionHQ? hq,
            bool allowEmpty,
            System.Collections.Generic.List<WeaponMount> outAvailable)
        {
            if (!UniversalLoadoutConfig.Enabled)
                return;

            UniversalLoadoutCatalog.EnsureHardpointExpanded(hardpointSet);
        }

        /// <summary>Before server vet — same expanded list so spawn is not rejected.</summary>
        public static void VetLoadoutPrefix(
            AircraftDefinition definition,
            Loadout requestedLoadout,
            Player player,
            Airbase airbase,
            INetworkPlayer sender)
        {
            if (!UniversalLoadoutConfig.Enabled || definition?.unitPrefab == null)
                return;

            Aircraft? aircraft = definition.unitPrefab.GetComponent<Aircraft>();
            if (aircraft?.weaponManager == null)
                return;

            UniversalLoadoutCatalog.EnsureAllHardpointsExpanded(aircraft.weaponManager);
        }
    }
}
