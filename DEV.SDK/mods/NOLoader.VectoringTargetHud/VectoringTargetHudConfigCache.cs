using NOLoader.ModConfig;

namespace NOLoader.VectoringTargetHud
{
    internal static class VectoringTargetHudConfigCache
    {
        internal static float UpdateRateHz = 20f;
        internal static float HoldWindowSeconds = 1f;
        internal static float PositionSmoothing = 0.2f;
        internal static float MaxScreenStepPx = 260f;
        internal static float SwitchHysteresisPx = 28f;
        internal static float NoseDotDistanceMeters = 35f;
        internal static float NoseDotDistanceByRangeFactor = 0.02f;
        internal static float NoseDotDistanceMaxMeters = 550f;
        internal static float NoseDotDistanceMinMeters = 20f;
        internal static float NearDistanceMeters = 1200f;
        internal static float NearDistanceScale = 0.55f;
        internal static float LineThicknessPx = 2.5f;
        internal static float LineLengthPx = 90f;
        internal static float LineAlpha = 0.9f;
        internal static string ShapeMode = "Prism";
        internal static float PrismBaseWidthPx = 18f;
        internal static float PrismTipWidthPx = 2.5f;
        internal static float PrismDepthSkew = 0.35f;
        internal static float PrismAlphaGradient = 0.45f;
        internal static float PrismMinLengthPx = 10f;
        internal static float PrismBaseOffsetPx = 14f;
        internal static float SpeedLengthFactor = 0.35f;
        internal static float MaxSpeedForLength = 500f;
        internal static float PerspectiveThicknessBoost = 0.4f;
        internal static float MinLineLengthPx = 8f;
        internal static float LiveColorR = 0.2f;
        internal static float LiveColorG = 1f;
        internal static float LiveColorB = 0.35f;
        internal static float HoldColorR = 1f;
        internal static float HoldColorG = 0.95f;
        internal static float HoldColorB = 0.1f;
        internal static bool DebugMode;

        internal static void Load(ModIniConfig cfg)
        {
            UpdateRateHz = cfg.GetFloat("General", "UpdateRateHz", 20f);
            HoldWindowSeconds = cfg.GetFloat("General", "HoldWindowSeconds", 1f);
            PositionSmoothing = cfg.GetFloat("General", "PositionSmoothing", 0.2f);
            MaxScreenStepPx = cfg.GetFloat("General", "MaxScreenStepPx", 260f);
            SwitchHysteresisPx = cfg.GetFloat("General", "SwitchHysteresisPx", 28f);
            NoseDotDistanceMeters = cfg.GetFloat("General", "NoseDotDistanceMeters", 35f);
            NoseDotDistanceByRangeFactor = cfg.GetFloat("General", "NoseDotDistanceByRangeFactor", 0.02f);
            NoseDotDistanceMaxMeters = cfg.GetFloat("General", "NoseDotDistanceMaxMeters", 550f);
            NoseDotDistanceMinMeters = cfg.GetFloat("General", "NoseDotDistanceMinMeters", 20f);
            NearDistanceMeters = cfg.GetFloat("General", "NearDistanceMeters", 1200f);
            NearDistanceScale = cfg.GetFloat("General", "NearDistanceScale", 0.55f);
            LineThicknessPx = cfg.GetFloat("Visual", "LineThicknessPx", 2.5f);
            LineLengthPx = cfg.GetFloat("Visual", "LineLengthPx", 90f);
            LineAlpha = cfg.GetFloat("Visual", "LineAlpha", 0.9f);
            ShapeMode = cfg.GetString("Visual", "ShapeMode", "Prism");
            PrismBaseWidthPx = cfg.GetFloat("Prism", "PrismBaseWidthPx", 18f);
            PrismTipWidthPx = cfg.GetFloat("Prism", "PrismTipWidthPx", 2.5f);
            PrismDepthSkew = cfg.GetFloat("Prism", "PrismDepthSkew", 0.35f);
            PrismAlphaGradient = cfg.GetFloat("Prism", "PrismAlphaGradient", 0.45f);
            PrismMinLengthPx = cfg.GetFloat("Prism", "PrismMinLengthPx", 10f);
            PrismBaseOffsetPx = cfg.GetFloat("Prism", "PrismBaseOffsetPx", 14f);
            SpeedLengthFactor = cfg.GetFloat("Visual", "SpeedLengthFactor", 0.35f);
            MaxSpeedForLength = cfg.GetFloat("Visual", "MaxSpeedForLength", 500f);
            PerspectiveThicknessBoost = cfg.GetFloat("Visual", "PerspectiveThicknessBoost", 0.4f);
            MinLineLengthPx = cfg.GetFloat("Visual", "MinLineLengthPx", 8f);
            LiveColorR = cfg.GetFloat("Visual", "LiveColorR", 0.2f);
            LiveColorG = cfg.GetFloat("Visual", "LiveColorG", 1f);
            LiveColorB = cfg.GetFloat("Visual", "LiveColorB", 0.35f);
            HoldColorR = cfg.GetFloat("Visual", "HoldColorR", 1f);
            HoldColorG = cfg.GetFloat("Visual", "HoldColorG", 0.95f);
            HoldColorB = cfg.GetFloat("Visual", "HoldColorB", 0.1f);
            DebugMode = cfg.GetBool("Debug", "DebugMode", false);
        }
    }
}
