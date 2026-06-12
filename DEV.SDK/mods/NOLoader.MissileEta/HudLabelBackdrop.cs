using UnityEngine;
using NOLoader.HudCommon;
using UnityEngine.UI;

namespace NOLoader.MissileEta
{
    internal static class HudLabelBackdrop
    {
        private static Sprite _whiteSprite;

        internal const float DefaultFontScale = 1.45f;
        internal const float DefaultVerticalOffsetPx = 8f;

        internal static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            return _whiteSprite;
        }

        internal static Image CreateBackdrop(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();

            var image = go.AddComponent<Image>();
            image.sprite = GetWhiteSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.material = null;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }

        internal static void FitBackdropToText(Image backdrop, Text text, Color accentColor)
        {
            if (backdrop == null || text == null)
                return;

            Canvas.ForceUpdateCanvases();
            float w = Mathf.Max(1f, text.preferredWidth);
            float h = Mathf.Max(1f, text.preferredHeight);
            var textRect = text.rectTransform;
            textRect.sizeDelta = new Vector2(w, h);
            backdrop.rectTransform.sizeDelta = new Vector2(w, h);

            float alpha = Mathf.Clamp01(MissileEtaConfigCache.LabelBackgroundAlpha);
            backdrop.enabled = alpha > 0.001f;
            if (!backdrop.enabled)
                return;

            Color bg = accentColor;
            bg.a = alpha;
            backdrop.color = bg;
        }

        internal static Color TextOnAccent(Color accent)
        {
            Color c = accent;
            c.a = 1f;
            return c;
        }

        internal static int ScaleFontPx(float basePx)
        {
            return Mathf.Max(10, Mathf.RoundToInt(HudScreenScale.Px(basePx * MissileEtaConfigCache.LabelFontScale)));
        }

        internal static float VerticalOffsetPx()
        {
            return HudScreenScale.Px(MissileEtaConfigCache.LabelVerticalOffsetPx);
        }
    }
}
