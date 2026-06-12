using NOLoader.API;
using NOLoader.HudCommon;
using NOLoader.ModConfig;
using UnityEngine;

namespace NOLoader.MissileLaunchArcHud
{
    public sealed class MissileLaunchArcHudMod : INOMod
#if NOLoader_RDYTU
        , INOModTickNormal
#endif
    {
        public void OnLoad(ref NOModContext ctx)
        {
            MissileLaunchArcHudConfigCache.Load(ModIniConfig.Load(ctx.ModRoot));
            FlightHudHost.Register(go =>
            {
                if (go.GetComponent<LaunchArcHudController>() == null)
                    go.AddComponent<LaunchArcHudController>();
            });
#if NOLoader_DEV
            Debug.Log("[NOLoader] MissileLaunchArcHud loaded");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }

#if NOLoader_RDYTU
        public void OnNormalUpdate(ref NOModContext ctx, float dt)
        {
            LaunchArcHudController.Instance?.RunTick();
        }
#endif
    }
}
