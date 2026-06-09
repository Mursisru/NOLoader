using NOLoader.API;
using UnityEngine;

namespace NOLoader.UniversalLoadout
{
    /// <summary>Allows any missile/bomb/gun mount on aircraft/helo/VTOL hardpoints during loadout selection.</summary>
    public sealed class UniversalLoadoutMod : INOMod
    {
        public void OnLoad(ref NOModContext ctx)
        {
            UniversalLoadoutCatalog.WarmCache();
#if NOLoader_DEV
            Debug.Log("[NOLoader] UniversalLoadout v1.0.1 — hardpoint weaponOptions expanded from encyclopedia");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }
    }
}
