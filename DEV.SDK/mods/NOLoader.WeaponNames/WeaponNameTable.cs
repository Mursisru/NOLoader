using System.Collections.Generic;

namespace NOLoader.WeaponNames
{
    internal static class WeaponNameTable
    {
        private static readonly Dictionary<string, string> VanillaToDisplay = new Dictionary<string, string>();

        static WeaponNameTable()
        {
            Add("AAM-29 Scythe", "AIM-120 AMRAAM");
            Add("IRM-2", "AIM-9 Sidewinder");
            Add("IRM-S2", "AIM-9 Sidewinder");
            Add("AGM-68", "AGM-65 Maverick");
        }

        private static void Add(string vanilla, string display)
        {
            VanillaToDisplay[vanilla] = display;
        }

        public static bool TryGetDisplayName(string? vanilla, out string displayName)
        {
            displayName = string.Empty;
            if (string.IsNullOrEmpty(vanilla))
                return false;
            return VanillaToDisplay.TryGetValue(vanilla!, out displayName);
        }
    }
}
