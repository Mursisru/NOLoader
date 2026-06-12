using NOLoader.API;
using NOLoader.HudCommon;
using NOLoader.ModConfig;
using UnityEngine;

namespace NOLoader.VectoringTargetHud
{
    public sealed class VectoringTargetHudMod : INOMod
#if NOLoader_RDYTU
        , INOModTickFast, INOModTickNormal
#endif
    {
        public void OnLoad(ref NOModContext ctx)
        {
            VectoringTargetHudConfigCache.Load(ModIniConfig.Load(ctx.ModRoot));
            FlightHudHost.Register(go =>
            {
                if (go.GetComponent<TargetHudLineController>() == null)
                    go.AddComponent<TargetHudLineController>();
            });
#if NOLoader_DEV
            Debug.Log("[NOLoader] VectoringTargetHud loaded");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }

#if NOLoader_RDYTU
        public void OnFastUpdate(ref NOModContext ctx, float dt)
        {
            TargetHudLineController.Instance?.RunDrawTick();
        }

        public void OnNormalUpdate(ref NOModContext ctx, float dt)
        {
            TargetHudLineController.Instance?.RunTargetTick();
        }
#endif
    }
}
