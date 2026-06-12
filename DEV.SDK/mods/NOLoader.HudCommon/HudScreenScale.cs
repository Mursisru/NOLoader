using UnityEngine;

namespace NOLoader.HudCommon
{
    public static class HudScreenScale
    {
        public const float ReferenceHeight = 1080f;

        private static int _cachedWidth = -1;
        private static int _cachedHeight = -1;
        private static float _heightScale = 1f;

        public static float HeightScale
        {
            get
            {
                int w = Screen.width;
                int h = Screen.height;
                if (w != _cachedWidth || h != _cachedHeight)
                {
                    _cachedWidth = w;
                    _cachedHeight = h;
                    _heightScale = h > 0 ? Mathf.Max(0.25f, h / ReferenceHeight) : 1f;
                }
                return _heightScale;
            }
        }

        public static float Px(float referencePixels) => referencePixels * HeightScale;
    }
}
