using UnityEngine;

namespace NOLoader.HudCommon
{
    public static class Patches
    {
        public static void AwakePostfix(FlightHud __instance)
        {
            if (__instance?.gameObject == null)
                return;

            FlightHudHost.InvokeAll(__instance.gameObject);
        }
    }
}
