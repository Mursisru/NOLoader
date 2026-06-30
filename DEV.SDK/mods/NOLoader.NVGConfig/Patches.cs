namespace NOLoader.NVGConfig
{
    internal static class Patches
    {
        public static void StartPostfix(NightVision __instance)
        {
            Bootstrap();
            NightVisionColorLogic.CacheFromStart(__instance);
        }

        public static void UpdatePostfix(NightVision __instance)
        {
            Bootstrap();
            NVGConfigCache.Refresh();
            NightVisionColorLogic.ApplyIfNeeded(__instance);
        }

        public static void OnSwitchCamPostfix(NightVision __instance)
        {
            Bootstrap();
            NightVisionColorLogic.InvalidateAndRecache(__instance);
        }

        private static void Bootstrap()
        {
            NVGConfigCache.EnsureInitialized();
            NVGConfigDriver.Ensure();
        }
    }
}
