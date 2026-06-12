using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace NOLoader.UniversalLoadout
{
    internal static class UniversalLoadoutCatalog
    {
        private static readonly List<WeaponMount> SelectableCache = new List<WeaponMount>();
        private static readonly HashSet<HardpointSet> ExpandedHardpoints = new HashSet<HardpointSet>();
        private static bool _cacheReady;

        public static void WarmCache()
        {
            if (_cacheReady)
                return;

            _cacheReady = true;
            SelectableCache.Clear();

            Encyclopedia? encyclopedia = Encyclopedia.i;
            if (encyclopedia?.weaponMounts == null)
                return;

            for (int i = 0; i < encyclopedia.weaponMounts.Count; i++)
            {
                WeaponMount mount = encyclopedia.weaponMounts[i];
                if (IsSelectableWeapon(mount))
                    SelectableCache.Add(mount);
            }
        }

        public static bool IsSelectableWeapon(WeaponMount? mount)
        {
            if (mount == null)
                return false;

            if (mount.Cargo || mount.Troops || mount.tailHook || mount.slingloadHook)
                return false;

            if (mount.info == null)
                return false;

            if (mount.info.cargo)
                return false;

            if (mount.NotAllowed(MissionManager.AllowEventContent))
                return false;

            return true;
        }

        public static void EnsureHardpointExpanded(HardpointSet? hardpointSet)
        {
            if (!UniversalLoadoutConfig.Enabled || hardpointSet?.weaponOptions == null)
                return;

            if (!ExpandedHardpoints.Add(hardpointSet))
                return;

            WarmCache();
            if (SelectableCache.Count == 0)
                return;

            var options = hardpointSet.weaponOptions;
            for (int i = 0; i < SelectableCache.Count; i++)
                options.Add(SelectableCache[i]);
        }

        public static void EnsureAllHardpointsExpanded(WeaponManager? weaponManager)
        {
            if (!UniversalLoadoutConfig.Enabled || weaponManager?.hardpointSets == null)
                return;

            for (int i = 0; i < weaponManager.hardpointSets.Length; i++)
                EnsureHardpointExpanded(weaponManager.hardpointSets[i]);
        }
    }
}
