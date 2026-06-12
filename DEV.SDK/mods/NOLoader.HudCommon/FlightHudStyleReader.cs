using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOLoader.HudCommon
{
    public static class FlightHudStyleReader
    {
        private static FieldInfo GetField(Type type, string name) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private const float MinMeaningfulAlpha = 0.08f;
        private const float LaserDimArcAlpha = 0.5f;

        private static FieldInfo _ladderImagesField;
        private static FieldInfo _distSpanField;
        private static FieldInfo _maxDistImageField;
        private static FieldInfo _minDistImageField;

        public struct LineStyle
        {
            public Color Color;
            public Material Material;
            public bool IsValid;
        }

        public static bool TryGetMissileLineStyle(out LineStyle style)
        {
            style = default;
            if (!TryGetMissileHudImages(out Image[] ladder, out Image distSpan, out Image maxDist, out Image minDist))
                return false;

            if (TrySampleLine(ladder, out style)) return true;
            if (TrySampleLine(distSpan, out style)) return true;
            if (TrySampleLine(maxDist, out style)) return true;
            if (TrySampleLine(minDist, out style)) return true;

            return TrySampleLine(SceneSingleton<FlightHud>.i?.waterline, out style);
        }

        public static float GetReferenceAlpha(bool preferDimArc = false)
        {
            if (TryGetMissileLineStyle(out LineStyle style) && style.Color.a >= MinMeaningfulAlpha)
                return style.Color.a;

            if (TrySampleLaserOuterCircle(out LineStyle laser))
            {
                if (preferDimArc && laser.Color.a >= MinMeaningfulAlpha)
                    return laser.Color.a;
                return laser.Color.a >= MinMeaningfulAlpha ? laser.Color.a : LaserDimArcAlpha;
            }

            return LaserDimArcAlpha;
        }

        private static bool TryGetMissileHudImages(
            out Image[] ladder, out Image distSpan, out Image maxDist, out Image minDist)
        {
            ladder = null;
            distSpan = null;
            maxDist = null;
            minDist = null;

            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null)
                return false;

            var weaponState = GetField(typeof(CombatHUD), "weaponState")?.GetValue(combatHud);
            if (!(weaponState is HUDMissileState missileHud))
                return false;

            EnsureMissileFields();
            ladder = _ladderImagesField?.GetValue(missileHud) as Image[];
            distSpan = _distSpanField?.GetValue(missileHud) as Image;
            maxDist = _maxDistImageField?.GetValue(missileHud) as Image;
            minDist = _minDistImageField?.GetValue(missileHud) as Image;
            return (ladder != null && ladder.Length > 0) || distSpan != null || maxDist != null || minDist != null;
        }

        private static void EnsureMissileFields()
        {
            if (_ladderImagesField == null)
                _ladderImagesField = GetField(typeof(HUDMissileState), "ladderImages");
            if (_distSpanField == null)
                _distSpanField = GetField(typeof(HUDMissileState), "distSpanImage");
            if (_maxDistImageField == null)
                _maxDistImageField = GetField(typeof(HUDMissileState), "maxDistImage");
            if (_minDistImageField == null)
                _minDistImageField = GetField(typeof(HUDMissileState), "minDistImage");
        }

        private static bool TrySampleLaserOuterCircle(out LineStyle style)
        {
            style = default;
            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null)
                return false;

            var weaponState = GetField(typeof(CombatHUD), "weaponState")?.GetValue(combatHud);
            if (!(weaponState is HUDLaserGuidedState laser))
                return false;

            var outer = GetField(typeof(HUDLaserGuidedState), "outerCircle")?.GetValue(laser) as Image;
            return TrySampleLine(outer, out style);
        }

        private static bool TrySampleLine(Image[] images, out LineStyle style)
        {
            style = default;
            if (images == null) return false;
            for (int i = 0; i < images.Length; i++)
            {
                if (TrySampleLine(images[i], out style))
                    return true;
            }
            return false;
        }

        private static bool TrySampleLine(Image image, out LineStyle style)
        {
            style = default;
            if (image == null || !image.gameObject.activeInHierarchy || !image.enabled)
                return false;

            Color c = image.color;
            c.a *= GetCanvasGroupAlpha(image.transform);
            if (c.a <= MinMeaningfulAlpha)
                return false;

            style = new LineStyle { Color = c, Material = image.material, IsValid = true };
            return true;
        }

        private static float GetCanvasGroupAlpha(Transform transform)
        {
            float alpha = 1f;
            Transform t = transform;
            while (t != null)
            {
                var group = t.GetComponent<CanvasGroup>();
                if (group != null)
                    alpha *= group.alpha;
                t = t.parent;
            }
            return alpha;
        }
    }
}
