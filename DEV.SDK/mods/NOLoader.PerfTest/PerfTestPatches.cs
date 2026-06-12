namespace NOLoader.PerfTest
{
    internal static class PerfTestPatches
    {
        public static void FixedUpdatePostfix(Aircraft __instance)
        {
            if (__instance == null)
                return;

            PerfTestState.PatchHits++;
            if (!PerfTestState.PatchFirstHitLogged)
            {
                PerfTestState.PatchFirstHitLogged = true;
                PerfTestLogger.Info("patch first hit aircraft=" + __instance.name);
            }

            if (PerfTestState.HeavyWork >= 1)
                PerfTestState.Spin(16);
        }
    }
}
