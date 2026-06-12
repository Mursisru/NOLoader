using UnityEngine;
using NOLoader.HudCommon;

namespace NOLoader.MissileLaunchArcHud
{
    /// <summary>Thin HUD rings for NEZ target markers (two sizes = no overlap).</summary>
    internal static class NezTargetRingSprite
    {
        private const int TextureSize = 512;
        private const float PixelsPerUnit = 100f;

        private static Sprite _thin;
        private static Sprite _thinOuter;

        internal static Sprite GetThinInner()
        {
            if (_thin == null)
                _thin = Build(0.83f, 0.95f);
            return _thin;
        }

        internal static Sprite GetThinOuter()
        {
            if (_thinOuter == null)
                _thinOuter = Build(0.85f, 0.97f);
            return _thinOuter;
        }

        private static Sprite Build(float inner01, float outer01)
        {
            int size = TextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float cx = (size - 1) * 0.5f;
            float maxR = cx;
            float inner = maxR * inner01;
            float outer = maxR * outer01;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cx;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= inner && r <= outer)
                        pixels[y * size + x] = new Color32(255, 255, 255, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }
    }
}
