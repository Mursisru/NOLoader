using System.Reflection;
using UnityEngine;
using NOLoader.HudCommon;
using UnityEngine.UI;

namespace NOLoader.MissileLaunchArcHud
{
    internal static class LaserOuterCircleTemplate
    {
        private static bool _loaded;
        private static Sprite _sprite;
        private static Material _material;
        private static Vector2 _sizeDelta;
        private static Image.Type _imageType;
        private static bool _preserveAspect;
        private static Vector2 _anchorMin;
        private static Vector2 _anchorMax;
        private static Vector2 _pivot;

        internal static bool IsReady => _loaded;

        internal static float GetReferenceDiameterPx()
        {
            EnsureLoaded();
            if (_loaded && _sizeDelta.x > 1f)
                return _sizeDelta.x;
            return 100f;
        }

        internal static void EnsureLoaded()
        {
            if (_loaded)
                return;

            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null)
                return;

            GameObject prefab = GetField(typeof(CombatHUD), "LaserGuidedUI")?.GetValue(combatHud) as GameObject;
            if (prefab == null)
                return;

            GameObject temp = Object.Instantiate(prefab);
            temp.SetActive(false);

            try
            {
                var laser = temp.GetComponent<HUDLaserGuidedState>();
                if (laser == null)
                    laser = temp.GetComponentInChildren<HUDLaserGuidedState>(true);
                if (laser == null)
                    return;

                var outer = GetField(typeof(HUDLaserGuidedState), "outerCircle")?.GetValue(laser) as Image;
                if (outer == null)
                    return;

                _sprite = outer.sprite;
                _material = outer.material;
                _imageType = outer.type;
                _preserveAspect = outer.preserveAspect;

                RectTransform rt = outer.rectTransform;
                _sizeDelta = rt.sizeDelta;
                _anchorMin = rt.anchorMin;
                _anchorMax = rt.anchorMax;
                _pivot = rt.pivot;
                _loaded = _sprite != null;
            }
            finally
            {
                Object.Destroy(temp);
            }
        }

        internal static void ApplyTo(Image image)
        {
            EnsureLoaded();
            if (!_loaded || image == null)
                return;

            image.sprite = _sprite;
            image.material = _material;
            image.type = _imageType;
            image.preserveAspect = _preserveAspect;
            image.raycastTarget = false;

            RectTransform rt = image.rectTransform;
            rt.sizeDelta = _sizeDelta;
            rt.anchorMin = _anchorMin;
            rt.anchorMax = _anchorMax;
            rt.pivot = _pivot;
        }

        private static FieldInfo GetField(System.Type type, string name) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
