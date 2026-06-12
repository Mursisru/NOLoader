using NOLoader.API;

namespace NOLoader.HudCommon
{
    public sealed class HudCommonMod : INOMod
    {
        public void OnLoad(ref NOModContext ctx)
        {
#if NOLoader_DEV
            UnityEngine.Debug.Log("[NOLoader] HudCommon loaded — shared FlightHud.Awake host");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }
    }
}
