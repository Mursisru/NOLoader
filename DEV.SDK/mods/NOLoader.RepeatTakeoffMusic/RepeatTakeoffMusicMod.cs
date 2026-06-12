using NOLoader.API;
using NOLoader.ModConfig;
using UnityEngine;

namespace NOLoader.RepeatTakeoffMusic
{
    public sealed class RepeatTakeoffMusicMod : INOMod
    {
        public void OnLoad(ref NOModContext ctx)
        {
            RepeatTakeoffMusicConfigCache.Load(ModIniConfig.Load(ctx.ModRoot));
#if NOLoader_DEV
            Debug.Log("[NOLoader] RepeatTakeoffMusic loaded");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }
    }
}
