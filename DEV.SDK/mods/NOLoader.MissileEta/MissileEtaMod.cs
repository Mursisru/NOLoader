using NOLoader.API;
using NOLoader.HudCommon;
using NOLoader.ModConfig;
using UnityEngine;

namespace NOLoader.MissileEta
{
    public sealed class MissileEtaMod : INOMod
#if NOLoader_RDYTU
        , INOModTickFast
#endif
    {
        public void OnLoad(ref NOModContext ctx)
        {
            MissileEtaConfigCache.Load(ModIniConfig.Load(ctx.ModRoot));
            FlightHudHost.Register(go =>
            {
                if (go.GetComponent<MissileEtaController>() == null)
                    go.AddComponent<MissileEtaController>();
            });
#if NOLoader_DEV
            Debug.Log("[NOLoader] MissileEta loaded");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }

#if NOLoader_RDYTU
        public void OnFastUpdate(ref NOModContext ctx, float dt)
        {
            MissileEtaController.Instance?.RunTick();
        }
#endif
    }
}
