using System;

namespace NOLoader.ModOptimizerVerify
{
    internal static class ModOptimizerVerifyState
    {
        internal static bool OnLoadLogged;
        internal static int SlowCount;
        internal static int SpawnedCount;
        internal static int ReflectionPingCount;
        internal static bool ReflectionDelegateOk;
        internal static bool CollisionLayerOk;
        internal static bool CollisionLayerWarned;
        internal static bool SceneLocatorOk;
        internal static bool FindRedirectOk;
        internal static bool PassLogged;
        internal static int SessionStartMs = Environment.TickCount;

        internal static bool ShouldReport() => SlowCount > 0 && (SlowCount % 3) == 0;
    }
}
