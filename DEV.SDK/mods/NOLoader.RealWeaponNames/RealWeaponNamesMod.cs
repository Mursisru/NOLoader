using NOLoader.API;
using NOLoader.ModConfig;
using NOLoader.RealWeaponNames.Data;
using UnityEngine;

namespace NOLoader.RealWeaponNames
{
    public sealed class RealWeaponNamesMod : INOMod
    {
        public void OnLoad(ref NOModContext ctx)
        {
            var cfg = ModIniConfig.Load(ctx.ModRoot);
            RealWeaponNamesState.SetEnabled(cfg.GetBool("General", "Enabled", true));
#if NOLoader_DEV
            Debug.Log("[NOLoader] RealWeaponNames loaded enabled=" + RealWeaponNamesState.IsEnabled);
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }
    }
}
