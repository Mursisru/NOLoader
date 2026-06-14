namespace NOLoader.Core.EngineTweaker
{
    public static class HudMarkerThrottleHooks
    {
        public static void UpdateMarkersPrefix(object __instance)
        {
            HudMarkerThrottleState.BeginFrame(__instance);
        }

        /// <summary>PrefixSkip: false = skip original UpdatePosition.</summary>
        public static bool UpdatePositionPrefixSkip(object __instance)
            => !HudMarkerThrottleState.ShouldSkipUpdatePosition(__instance);

        /// <summary>PrefixSkip: false = skip original JammingDistortion.</summary>
        public static bool JammingDistortionPrefixSkip(object __instance, float jammingStrength)
            => !HudMarkerThrottleState.ShouldSkipJamming(__instance);
    }
}
