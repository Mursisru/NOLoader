using UnityEngine;
using NOLoader.HudCommon;

namespace NOLoader.MissileLaunchArcHud
{
    internal static class LaunchArcRingSprite
    {
        public const int TextureSize = 2048;

        private const float PixelsPerUnit = 100f;

        private static Sprite _spritePartial;
        private static Sprite _spriteFull;
        private static int _cacheKey;

        internal static Sprite Get(bool fullCircle)
        {
            int key = ComputeCacheKey();
            if (key != _cacheKey)
                InvalidateCache();

            return fullCircle ? GetFull() : GetPartial();
        }

        internal static void InvalidateCache()
        {
            DestroySprite(ref _spriteFull);
            DestroySprite(ref _spritePartial);
            _cacheKey = 0;
        }

        private static int ComputeCacheKey()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + MissileLaunchArcHudConfigCache.RingInnerRadius01.GetHashCode();
                h = h * 31 + MissileLaunchArcHudConfigCache.RingOuterRadius01.GetHashCode();
                h = h * 31 + MissileLaunchArcHudConfigCache.PartialSideArcDegrees.GetHashCode();
                return h;
            }
        }

        private static void DestroySprite(ref Sprite sprite)
        {
            if (sprite == null)
                return;
            if (sprite.texture != null)
                Object.Destroy(sprite.texture);
            Object.Destroy(sprite);
            sprite = null;
        }

        private static Sprite GetFull()
        {
            if (_spriteFull != null)
                return _spriteFull;

            _cacheKey = ComputeCacheKey();
            _spriteFull = BuildRing(fullCircle: true);
            return _spriteFull;
        }

        private static Sprite GetPartial()
        {
            if (_spritePartial != null)
                return _spritePartial;

            _cacheKey = ComputeCacheKey();
            _spritePartial = BuildRing(fullCircle: false);
            return _spritePartial;
        }

        private static Sprite BuildRing(bool fullCircle)
        {
            int size = TextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float inner01 = Mathf.Clamp(MissileLaunchArcHudConfigCache.RingInnerRadius01, 0.5f, 0.99f);
            float outer01 = Mathf.Clamp(MissileLaunchArcHudConfigCache.RingOuterRadius01, inner01 + 0.002f, 0.995f);

            float cx = (size - 1) * 0.5f;
            float cy = cx;
            float maxR = cx;
            float inner = maxR * inner01;
            float outer = maxR * outer01;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r < inner || r > outer)
                        continue;

                    if (!fullCircle)
                    {
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (angle < 0f)
                            angle += 360f;
                        if (!IsPartialArcAngle(angle))
                            continue;
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static bool IsPartialArcAngle(float angleDeg)
        {
            float span = Mathf.Clamp(MissileLaunchArcHudConfigCache.PartialSideArcDegrees, 10f, 89f);
            bool rightSide = angleDeg <= span || angleDeg >= 360f - span;
            bool leftSide = angleDeg >= 180f - span && angleDeg <= 180f + span;
            return rightSide || leftSide;
        }
    }
}
