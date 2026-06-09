using NOLoader.API;
using UnityEngine;

namespace NOLoader.WeaponNames
{
    public sealed class WeaponNamesMod : INOMod
    {
        public void OnLoad(ref NOModContext ctx)
        {
            Debug.Log("[NOLoader] WeaponNames mod loaded");
        }

        public void OnUnload(ref NOModContext ctx) { }
    }
}
