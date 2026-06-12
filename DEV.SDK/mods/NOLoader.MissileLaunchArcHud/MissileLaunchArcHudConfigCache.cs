using NOLoader.ModConfig;

namespace NOLoader.MissileLaunchArcHud
{
    internal static class MissileLaunchArcHudConfigCache
    {
        internal static bool Enabled;
        internal static bool RequireTargetLock;
        internal static bool HideForLaserGuided;
        internal static float ArcRadiusScale;
        internal static bool MatchFlightHudStyle;
        internal static float FlightHudAlphaInArcMul;
        internal static float FlightHudAlphaOutOfArcMul;
        internal static float CalmArcAlphaInZone;
        internal static float CalmArcAlphaOutOfArc;
        internal static bool UseHudColorForCalmArc;
        internal static bool DrawBehindHud;
        internal static bool PartialRing;
        internal static float FullRingBelowScale;
        internal static float RingInnerRadius01;
        internal static float RingOuterRadius01;
        internal static float PartialSideArcDegrees;
        internal static float UpdateHz;
        internal static bool NezMarkersEnabled;
        internal static float NezApproachBandMeters;
        internal static float NezApproachRingSizePx;
        internal static float NezApproachRingAlpha;
        internal static float NezInnerRingSizePx;
        internal static float NezInnerOuterGapPx;
        internal static string NezRedColorHtml = "#FF2020";
        internal static float NezBlinkHz;
        internal static float NezBlinkAlphaMax;
        internal static float TargetRingInsideArcFraction;
        internal static float TargetRingInsideArcHoldSeconds;

        internal static void Load(ModIniConfig cfg)
        {
            Enabled = cfg.GetBool("LaunchArc", "Enabled", true);
            RequireTargetLock = cfg.GetBool("LaunchArc", "RequireTargetLock", false);
            HideForLaserGuided = cfg.GetBool("LaunchArc", "HideForLaserGuided", true);
            ArcRadiusScale = cfg.GetFloat("LaunchArc", "ArcRadiusScale", 1.16f);
            MatchFlightHudStyle = cfg.GetBool("LaunchArc", "MatchFlightHudStyle", true);
            FlightHudAlphaInArcMul = cfg.GetFloat("LaunchArc", "FlightHudAlphaInArcMul", 1f);
            FlightHudAlphaOutOfArcMul = cfg.GetFloat("LaunchArc", "FlightHudAlphaOutOfArcMul", 1f);
            CalmArcAlphaInZone = cfg.GetFloat("LaunchArc", "CalmArcAlphaInZone", 0.42f);
            CalmArcAlphaOutOfArc = cfg.GetFloat("LaunchArc", "CalmArcAlphaOutOfArc", 0.34f);
            UseHudColorForCalmArc = cfg.GetBool("LaunchArc", "UseHudColorForCalmArc", true);
            DrawBehindHud = cfg.GetBool("LaunchArc", "DrawBehindHud", true);
            UpdateHz = cfg.GetFloat("LaunchArc", "UpdateHz", 0f);
            PartialRing = cfg.GetBool("LaunchArc.RingShape", "PartialRing", true);
            FullRingBelowScale = cfg.GetFloat("LaunchArc.RingShape", "FullRingBelowScale", 0.42f);
            RingInnerRadius01 = cfg.GetFloat("LaunchArc.RingShape", "RingInnerRadius01", 0.924f);
            RingOuterRadius01 = cfg.GetFloat("LaunchArc.RingShape", "RingOuterRadius01", 0.929f);
            PartialSideArcDegrees = cfg.GetFloat("LaunchArc.RingShape", "PartialSideArcDegrees", 72f);
            NezMarkersEnabled = cfg.GetBool("LaunchArc.Nez", "NezMarkersEnabled", true);
            NezApproachBandMeters = cfg.GetFloat("LaunchArc.Nez", "NezApproachBandMeters", 3500f);
            NezApproachRingSizePx = cfg.GetFloat("LaunchArc.Nez", "NezApproachRingSizePx", 10f);
            NezApproachRingAlpha = cfg.GetFloat("LaunchArc.Nez", "NezApproachRingAlpha", 0.55f);
            NezInnerRingSizePx = cfg.GetFloat("LaunchArc.Nez", "NezInnerRingSizePx", 14f);
            NezInnerOuterGapPx = cfg.GetFloat("LaunchArc.Nez", "NezInnerOuterGapPx", 3f);
            NezRedColorHtml = cfg.GetString("LaunchArc.Nez", "NezRedColorHtml", "#FF2020");
            NezBlinkHz = cfg.GetFloat("LaunchArc.Nez", "NezBlinkHz", 2f);
            NezBlinkAlphaMax = cfg.GetFloat("LaunchArc.Nez", "NezBlinkAlphaMax", 0.95f);
            TargetRingInsideArcFraction = cfg.GetFloat("LaunchArc.TargetRing", "TargetRingInsideArcFraction", 0.72f);
            TargetRingInsideArcHoldSeconds = cfg.GetFloat("LaunchArc.TargetRing", "TargetRingInsideArcHoldSeconds", 0.35f);
        }
    }
}
