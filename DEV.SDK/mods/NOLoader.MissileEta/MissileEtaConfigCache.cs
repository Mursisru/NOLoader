using NOLoader.ModConfig;
using NOLoader.HudCommon;

namespace NOLoader.MissileEta
{
    internal static class MissileEtaConfigCache
    {
        internal static bool Enabled;
        internal static bool MatchFlightHudStyle;
        internal static float UpdateHz;
        internal static bool ShowOwnEta;
        internal static float OwnFontSizePx;
        internal static int OwnDecimalPlaces;
        internal static bool ShowArhCountdown;
        internal static string ArhPrefix = "R";
        internal static float ArhBlinkHz;
        internal static string ArhLostColorHtml = "#4499FF";
        internal static bool ShowOwnOffScreenArrows;
        internal static float OwnArrowAlphaMul;
        internal static float OnScreenMarginPx;
        internal static float LabelFontScale;
        internal static float LabelVerticalOffsetPx;
        internal static float LabelBackgroundAlpha;
        internal static bool ShowIncoming;
        internal static float IncomingFontSizePx;
        internal static int IncomingDecimalPlaces;
        internal static string IncomingColorHtml = "#FF2020";
        internal static float IncomingAlphaMul;
        internal static bool ShowOffScreenArrows;
        internal static float EdgeMarginPx;
        internal static float ArrowLengthPx;
        internal static float IncomingArrowAlphaMul;
        internal static float ArrowPositionSmoothing;
        internal static float ArrowMaxScreenStepPx;
        internal static float ArrowTextGapPx;
        internal static int MaxLabels;
        internal static float MaxEtaSeconds;
        internal static float HoldInvalidSeconds;
        internal static int RawMedianWindow;
        internal static float ClosureSmoothHz;
        internal static float MaxDecreasePerSec;
        internal static float MaxIncreasePerSec;
        internal static float MinClosureMps;
        internal static float DisplayQuantizeStep;
        internal static float BlendLosWeight;
        internal static bool UseDeltaVProjection;
        internal static bool UseCpaWhenCoasting;
        internal static float BoostClosureWeight;
        internal static float SpeedClosureWeight;
        internal static float LaunchSpeedAlignWeight;
        internal static float LaunchDeltaVWeight;
        internal static float LaunchSettleSeconds;
        internal static float CpaMinAgeSeconds;
        internal static float MinAlignDot;
        internal static bool DebugLog;
        internal static bool DebugVerbose;

        internal static bool ShowOwnEtaCached;
        internal static bool ShowIncomingCached;
        internal static MissileEtaFilterSettings Filter;
        internal static MissileEtaPhysicsSettings Physics;

        internal static void Load(ModIniConfig cfg)
        {
            Enabled = cfg.GetBool("MissileEta", "Enabled", true);
            MatchFlightHudStyle = cfg.GetBool("MissileEta", "MatchFlightHudStyle", true);
            UpdateHz = cfg.GetFloat("MissileEta", "UpdateHz", 0f);
            ShowOwnEta = cfg.GetBool("MissileEta.Own", "ShowOwnEta", true);
            OwnFontSizePx = cfg.GetFloat("MissileEta.Own", "FontSizePx", 14f);
            OwnDecimalPlaces = cfg.GetInt("MissileEta.Own", "DecimalPlaces", 1);
            ShowArhCountdown = cfg.GetBool("MissileEta.Own", "ShowArhCountdown", true);
            ArhPrefix = cfg.GetString("MissileEta.Own", "ArhPrefix", "R");
            ArhBlinkHz = cfg.GetFloat("MissileEta.Own", "ArhBlinkHz", 2.5f);
            ArhLostColorHtml = cfg.GetString("MissileEta.Own", "ArhLostColorHtml", "#4499FF");
            ShowOwnOffScreenArrows = cfg.GetBool("MissileEta.Own", "ShowOwnOffScreenArrows", true);
            OwnArrowAlphaMul = cfg.GetFloat("MissileEta.Own", "ArrowAlphaMul", 1f);
            OnScreenMarginPx = cfg.GetFloat("MissileEta.Own", "OnScreenMarginPx", 40f);
            LabelFontScale = cfg.GetFloat("MissileEta.Own", "LabelFontScale", 1.45f);
            LabelVerticalOffsetPx = cfg.GetFloat("MissileEta.Own", "LabelVerticalOffsetPx", 8f);
            LabelBackgroundAlpha = cfg.GetFloat("MissileEta.Own", "LabelBackgroundAlpha", 0.18f);
            ShowIncoming = cfg.GetBool("MissileEta.Incoming", "ShowIncoming", true);
            IncomingFontSizePx = cfg.GetFloat("MissileEta.Incoming", "FontSizePx", 14f);
            IncomingDecimalPlaces = cfg.GetInt("MissileEta.Incoming", "DecimalPlaces", 1);
            IncomingColorHtml = cfg.GetString("MissileEta.Incoming", "IncomingColorHtml", "#FF2020");
            IncomingAlphaMul = cfg.GetFloat("MissileEta.Incoming", "IncomingAlphaMul", 1f);
            ShowOffScreenArrows = cfg.GetBool("MissileEta.Incoming", "ShowOffScreenArrows", true);
            EdgeMarginPx = cfg.GetFloat("MissileEta.Incoming", "EdgeMarginPx", 48f);
            ArrowLengthPx = cfg.GetFloat("MissileEta.Incoming", "ArrowLengthPx", 36f);
            IncomingArrowAlphaMul = cfg.GetFloat("MissileEta.Incoming", "ArrowAlphaMul", 0.85f);
            ArrowPositionSmoothing = cfg.GetFloat("MissileEta.Incoming", "PositionSmoothing", 0.2f);
            ArrowMaxScreenStepPx = cfg.GetFloat("MissileEta.Incoming", "MaxScreenStepPx", 260f);
            ArrowTextGapPx = cfg.GetFloat("MissileEta.Incoming", "ArrowTextGapPx", 6f);
            MaxLabels = cfg.GetInt("MissileEta.Limits", "MaxLabels", 16);
            MaxEtaSeconds = cfg.GetFloat("MissileEta.Limits", "MaxEtaSeconds", 120f);
            HoldInvalidSeconds = cfg.GetFloat("MissileEta.Filter", "HoldInvalidSeconds", 0.35f);
            RawMedianWindow = cfg.GetInt("MissileEta.Filter", "RawMedianWindow", 3);
            ClosureSmoothHz = cfg.GetFloat("MissileEta.Filter", "ClosureSmoothHz", 4f);
            MaxDecreasePerSec = cfg.GetFloat("MissileEta.Filter", "MaxDecreasePerSec", 12f);
            MaxIncreasePerSec = cfg.GetFloat("MissileEta.Filter", "MaxIncreasePerSec", 0.5f);
            MinClosureMps = cfg.GetFloat("MissileEta.Filter", "MinClosureMps", 1f);
            DisplayQuantizeStep = cfg.GetFloat("MissileEta.Filter", "DisplayQuantizeStep", 0.1f);
            BlendLosWeight = cfg.GetFloat("MissileEta.Filter", "BlendLosWeight", 0f);
            UseDeltaVProjection = cfg.GetBool("MissileEta.Physics", "UseDeltaVProjection", true);
            UseCpaWhenCoasting = cfg.GetBool("MissileEta.Physics", "UseCpaWhenCoasting", true);
            BoostClosureWeight = cfg.GetFloat("MissileEta.Physics", "BoostClosureWeight", 0.75f);
            SpeedClosureWeight = cfg.GetFloat("MissileEta.Physics", "SpeedClosureWeight", 0.65f);
            LaunchSpeedAlignWeight = cfg.GetFloat("MissileEta.Physics", "LaunchSpeedAlignWeight", 0.35f);
            LaunchDeltaVWeight = cfg.GetFloat("MissileEta.Physics", "LaunchDeltaVWeight", 0.15f);
            LaunchSettleSeconds = cfg.GetFloat("MissileEta.Physics", "LaunchSettleSeconds", 1.5f);
            CpaMinAgeSeconds = cfg.GetFloat("MissileEta.Physics", "CpaMinAgeSeconds", 2.5f);
            MinAlignDot = cfg.GetFloat("MissileEta.Physics", "MinAlignDot", 0.15f);
            DebugLog = cfg.GetBool("MissileEta.Debug", "DebugLog", false);
            DebugVerbose = cfg.GetBool("MissileEta.Debug", "DebugVerbose", false);
        }

        internal static void RefreshForTick()
        {
            ShowOwnEtaCached = ShowOwnEta;
            ShowIncomingCached = ShowIncoming;
            Filter = MissileEtaCalculator.GetFilterSettings();
            Physics = MissileEtaCalculator.GetPhysicsSettings();
        }
    }
}
