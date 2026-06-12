using UnityEngine;
using NOLoader.HudCommon;
using UnityEngine.UI;

namespace NOLoader.MissileLaunchArcHud
{
    internal static class LaunchArcHudPalette
    {
        internal static bool IsLaunchZoneBlink(LaunchArcGameApi.LaunchArcSnapshot snap)
        {
            return snap.NezPhase == LaunchArcNezPhase.InsideNez && snap.InArc;
        }

        internal static Color GetArcRingColor(LaunchArcGameApi.LaunchArcSnapshot snap)
        {
            if (IsLaunchZoneBlink(snap))
                return GetBlinkRedColor();

            return GetCalmArcColor(snap);
        }

        internal static Color GetCalmArcRingColor(LaunchArcGameApi.LaunchArcSnapshot snap)
        {
            return GetCalmArcColor(snap);
        }

        internal static Material GetArcRingMaterial()
        {
            if (MissileLaunchArcHudConfigCache.MatchFlightHudStyle
                && FlightHudStyleReader.TryGetMissileLineStyle(out FlightHudStyleReader.LineStyle style))
            {
                return style.Material;
            }

            return null;
        }

        internal static Color GetTargetRingColor(LaunchArcGameApi.LaunchArcSnapshot snap)
        {
            if (IsLaunchZoneBlink(snap))
                return GetBlinkRedColor();

            return GetCalmArcColor(snap);
        }

        internal static Material GetTargetRingMaterial() => GetArcRingMaterial();

        internal static void ApplyRingStyle(Image image, Color color, Material referenceMaterial)
        {
            if (image == null)
                return;

            if (referenceMaterial != null)
                image.material = referenceMaterial;
            else if (image.material != null)
                image.material = null;

            image.color = color;
        }

        private static Color GetCalmArcColor(LaunchArcGameApi.LaunchArcSnapshot snap)
        {
            Color rgb = ResolveCalmHudRgb();
            float alpha = ResolveRingAlpha(snap.InArc, preferDimWhenOutOfArc: true);
            return new Color(rgb.r, rgb.g, rgb.b, alpha);
        }

        private static Color ResolveCalmHudRgb()
        {
            if (MissileLaunchArcHudConfigCache.MatchFlightHudStyle
                && FlightHudStyleReader.TryGetMissileLineStyle(out FlightHudStyleReader.LineStyle style))
            {
                return new Color(style.Color.r, style.Color.g, style.Color.b, 1f);
            }

            if (!MissileLaunchArcHudConfigCache.UseHudColorForCalmArc)
                return Color.white;

            return new Color(
                PlayerSettings.hudColorR / 255f,
                PlayerSettings.hudColorG / 255f,
                PlayerSettings.hudColorB / 255f,
                1f);
        }

        private static Color GetBlinkRedColor()
        {
            Color red = ResolveNezRedRgb();
            if (!SampleBlinkVisible())
                return new Color(red.r, red.g, red.b, 0f);

            float alpha = ResolveRingAlpha(inArc: true, preferDimWhenOutOfArc: false);
            return new Color(red.r, red.g, red.b, alpha);
        }

        private static float ResolveRingAlpha(bool inArc, bool preferDimWhenOutOfArc)
        {
            if (MissileLaunchArcHudConfigCache.MatchFlightHudStyle)
            {
                float hudA = FlightHudStyleReader.GetReferenceAlpha(preferDimWhenOutOfArc && !inArc);
                float mul = inArc
                    ? MissileLaunchArcHudConfigCache.FlightHudAlphaInArcMul
                    : MissileLaunchArcHudConfigCache.FlightHudAlphaOutOfArcMul;
                return Mathf.Clamp01(hudA * mul);
            }

            float legacy = inArc
                ? MissileLaunchArcHudConfigCache.CalmArcAlphaInZone
                : MissileLaunchArcHudConfigCache.CalmArcAlphaOutOfArc;
            return Mathf.Clamp01(legacy);
        }

        internal static bool SampleBlinkVisible()
        {
            float hz = Mathf.Max(0.5f, MissileLaunchArcHudConfigCache.NezBlinkHz);
            return Mathf.Repeat(Time.unscaledTime * hz, 1f) < 0.5f;
        }

        private static Color ResolveNezRedRgb()
        {
            string hex = MissileLaunchArcHudConfigCache.NezRedColorHtml;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed))
                return new Color(parsed.r, parsed.g, parsed.b, 1f);

            return new Color(1f, 0.12f, 0.1f, 1f);
        }
    }
}
