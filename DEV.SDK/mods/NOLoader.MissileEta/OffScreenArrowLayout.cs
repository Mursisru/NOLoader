using UnityEngine;
using NOLoader.HudCommon;
using UnityEngine.UI;

namespace NOLoader.MissileEta
{
    internal static class OffScreenArrowLayout
    {
        internal static void Place(
            Vector2 edgePosition,
            Vector2 dirFromCenter,
            float arrowLengthPx,
            Transform prismTransform,
            RectTransform prismRect,
            Text etaText,
            Image textBackdrop,
            Transform textTransform)
        {
            Vector2 dir = dirFromCenter.sqrMagnitude > 0.0001f
                ? dirFromCenter.normalized
                : Vector2.up;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            HudScreenPlacement.PlaceTransform(prismTransform, edgePosition);
            prismRect.rotation = Quaternion.Euler(0f, 0f, angle);

            Canvas.ForceUpdateCanvases();
            float textHalfW = Mathf.Max(etaText.preferredWidth * 0.5f, HudScreenScale.Px(8f));
            float gap = HudScreenScale.Px(MissileEtaConfigCache.ArrowTextGapPx);
            float inset = gap + textHalfW;

            Vector2 textPos = edgePosition - dir * inset;
            HudScreenPlacement.PlaceTransform(textTransform, textPos);
            if (textBackdrop != null)
                HudScreenPlacement.PlaceTransform(textBackdrop.transform, textPos);
        }
    }
}
