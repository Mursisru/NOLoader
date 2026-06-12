namespace NOLoader.AutoFlare
{
    internal static class Patches
    {
        public static void FixedUpdatePostfix(Aircraft __instance)
        {
            AutoFlareLogic.Postfix(__instance);
        }
    }
}
