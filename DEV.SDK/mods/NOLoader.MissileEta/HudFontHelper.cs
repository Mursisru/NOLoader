using UnityEngine;
using NOLoader.HudCommon;
using UnityEngine.UI;

namespace NOLoader.MissileEta
{
    internal static class HudFontHelper
    {
        internal static Font ResolveHudFont()
        {
            var alt = Object.FindObjectOfType<Altitude>();
            if (alt != null)
            {
                var field = typeof(Altitude).GetField("radarAlt",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field?.GetValue(alt) is Text sample && sample.font != null)
                    return sample.font;
            }

            Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacy != null)
                return legacy;

            legacy = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (legacy != null)
                return legacy;

            return Font.CreateDynamicFontFromOSFont("Arial", 16);
        }
    }
}
