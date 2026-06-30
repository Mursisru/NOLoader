using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NOLoader.NVGConfig
{
    internal static class NightVisionColorLogic
    {
        internal static bool Cached;
        internal static float OrigSaturation;
        internal static float OrigContrast;
        internal static Color OrigColorFilter;
        internal static float OrigHueShift;

        private static NVGMode _lastMode = NVGMode.GreenPhosphor;
        private static bool _lastNightVisActive;
        private static float _lastCustSat;
        private static float _lastCustCon;
        private static float _lastCustR;
        private static float _lastCustG;
        private static float _lastCustB;

        internal static void NotifyConfigChanged()
        {
            _lastMode = (NVGMode)(-1);
        }

        internal static void InvalidateAndRecache(NightVision instance)
        {
            Cached = false;
            CacheFromStart(instance);
            NotifyConfigChanged();
            ApplyIfNeeded(instance);
        }

        internal static void CacheFromStart(NightVision instance)
        {
            if (NightVisionFields.GetPostProcessing(instance) == null)
                return;

            ColorAdjustments? colorAdjustments = NightVisionFields.GetColorAdjustments(instance);
            if (colorAdjustments == null)
                return;

            OrigSaturation = colorAdjustments.saturation.value;
            OrigContrast = colorAdjustments.contrast.value;
            OrigColorFilter = colorAdjustments.colorFilter.value;
            OrigHueShift = colorAdjustments.hueShift.value;
            Cached = true;
        }

        internal static void ApplyIfNeeded(NightVision instance)
        {
            NVGMode mode = NVGConfigCache.SelectedMode;
            bool nightVisActive = NightVisionFields.GetNightVisActive(instance);

            if (nightVisActive && !_lastNightVisActive)
                CacheFromStart(instance);

            if (!Cached)
                CacheFromStart(instance);

            float cS = NVGConfigCache.CustomSaturation;
            float cC = NVGConfigCache.CustomContrast;
            float cR = NVGConfigCache.CustomColorR;
            float cG = NVGConfigCache.CustomColorG;
            float cB = NVGConfigCache.CustomColorB;

            bool customChanged = nightVisActive && mode == NVGMode.Custom &&
                (cS != _lastCustSat || cC != _lastCustCon ||
                 cR != _lastCustR || cG != _lastCustG || cB != _lastCustB);

            // Re-apply every frame while NVG is on — URP can reset volume params after Update.
            if (!nightVisActive
                && mode == _lastMode
                && nightVisActive == _lastNightVisActive
                && !customChanged)
                return;

            _lastMode = mode;
            _lastNightVisActive = nightVisActive;
            _lastCustSat = cS;
            _lastCustCon = cC;
            _lastCustR = cR;
            _lastCustG = cG;
            _lastCustB = cB;

            ColorAdjustments? colorAdjustments = NightVisionFields.GetColorAdjustments(instance);
            if (colorAdjustments == null)
                return;

            if (!nightVisActive && !Cached)
                return;

            if (nightVisActive)
            {
                switch (mode)
                {
                    case NVGMode.GreenPhosphor:
                        SetFloat(colorAdjustments.saturation, OrigSaturation);
                        SetFloat(colorAdjustments.contrast, OrigContrast);
                        SetColor(colorAdjustments.colorFilter, OrigColorFilter);
                        SetFloat(colorAdjustments.hueShift, OrigHueShift);
                        break;
                    case NVGMode.WhitePhosphor:
                        SetFloat(colorAdjustments.saturation, -80f);
                        SetFloat(colorAdjustments.contrast, 40f);
                        SetColor(colorAdjustments.colorFilter, new Color(0.0f, 0.6f, 0.9f));
                        SetFloat(colorAdjustments.hueShift, OrigHueShift);
                        break;
                    case NVGMode.Monochrome:
                        SetFloat(colorAdjustments.saturation, -100f);
                        SetFloat(colorAdjustments.contrast, 40f);
                        SetColor(colorAdjustments.colorFilter, Color.white);
                        SetFloat(colorAdjustments.hueShift, OrigHueShift);
                        break;
                    case NVGMode.FullColor:
                        SetFloat(colorAdjustments.saturation, 40f);
                        SetFloat(colorAdjustments.contrast, 15f);
                        SetColor(colorAdjustments.colorFilter, Color.white);
                        SetFloat(colorAdjustments.hueShift, OrigHueShift);
                        break;
                    case NVGMode.AlienTechnology:
                        SetFloat(colorAdjustments.saturation, -80f);
                        SetFloat(colorAdjustments.contrast, 40f);
                        SetColor(colorAdjustments.colorFilter, new Color(0.8f, 0.0f, 0.9f));
                        SetFloat(colorAdjustments.hueShift, OrigHueShift);
                        break;
                    case NVGMode.Custom:
                        SetFloat(colorAdjustments.saturation, cS);
                        SetFloat(colorAdjustments.contrast, cC);
                        SetColor(colorAdjustments.colorFilter, new Color(cR, cG, cB));
                        SetFloat(colorAdjustments.hueShift, OrigHueShift);
                        break;
                }
            }
            else
            {
                SetFloat(colorAdjustments.saturation, OrigSaturation);
                SetFloat(colorAdjustments.contrast, OrigContrast);
                SetColor(colorAdjustments.colorFilter, OrigColorFilter);
                SetFloat(colorAdjustments.hueShift, OrigHueShift);
            }
        }

        private static void SetFloat(FloatParameter parameter, float value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void SetColor(ColorParameter parameter, Color value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }
    }
}
