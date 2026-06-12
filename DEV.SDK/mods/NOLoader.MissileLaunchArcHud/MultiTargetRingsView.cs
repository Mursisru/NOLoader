using System.Collections.Generic;
using NOLoader.HudCommon;
using UnityEngine;
using UnityEngine.UI;

namespace NOLoader.MissileLaunchArcHud
{
    /// <summary>One calm HUD ring per in-arc target when multiple targets are locked.</summary>
    internal sealed class MultiTargetRingsView
    {
        private readonly GameObject _root;
        private readonly Transform _canvasRoot;
        private readonly List<Image> _rings = new List<Image>();
        private readonly List<RectTransform> _rects = new List<RectTransform>();

        internal MultiTargetRingsView(Transform flightHudCanvas)
        {
            Transform parent = HudScreenPlacement.ResolveMarkersParent(flightHudCanvas);
            _canvasRoot = parent;

            _root = new GameObject("MissileLaunchArcHud_MultiTarget");
            var rootRect = _root.AddComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;
            rootRect.localScale = Vector3.one;
            _root.SetActive(false);
        }

        internal bool NeedsRebuild(Transform flightHudCanvas)
        {
            return _canvasRoot != HudScreenPlacement.ResolveMarkersParent(flightHudCanvas);
        }

        internal void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }

        internal void Apply(
            LaunchArcGameApi.LaunchArcSnapshot snap,
            IReadOnlyList<LaunchArcGameApi.TargetRingPlacement> placements)
        {
            float ringPx = MissileLaunchArcHudConfigCache.NezInnerRingSizePx;
            Color ringColor = LaunchArcHudPalette.GetCalmArcRingColor(snap);
            Material ringMaterial = LaunchArcHudPalette.GetArcRingMaterial();

            int count = placements?.Count ?? 0;
            EnsureRingCount(count);

            if (count == 0)
            {
                HideAll();
                return;
            }

            _root.transform.SetAsLastSibling();

            for (int i = 0; i < count; i++)
            {
                Image img = _rings[i];
                RectTransform rect = _rects[i];
                SetRingSize(rect, ringPx);
                PlaceRing(rect, placements[i].ScreenPosition);

                bool show = ringColor.a > 0.001f;
                img.enabled = show;
                if (show)
                    LaunchArcHudPalette.ApplyRingStyle(img, ringColor, ringMaterial);
            }

            for (int i = count; i < _rings.Count; i++)
                _rings[i].enabled = false;
        }

        internal void HideAll()
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                if (_rings[i] != null)
                    _rings[i].enabled = false;
            }
        }

        internal void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);
            _rings.Clear();
            _rects.Clear();
        }

        private void EnsureRingCount(int count)
        {
            while (_rings.Count < count)
            {
                int index = _rings.Count;
                Image img = CreateRing($"MultiTarget_{index}", out RectTransform rect);
                _rings.Add(img);
                _rects.Add(rect);
            }
        }

        private Image CreateRing(string name, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_root.transform, false);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            img.material = null;
            img.sprite = NezTargetRingSprite.GetThinInner();
            img.preserveAspect = true;
            img.enabled = false;

            rect = img.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            return img;
        }

        private static void PlaceRing(RectTransform rect, Vector3 canvasWorldPosition)
        {
            rect.position = canvasWorldPosition;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static void SetRingSize(RectTransform rect, float referenceDiameterPx)
        {
            float d = HudScreenScale.Px(Mathf.Max(4f, referenceDiameterPx));
            rect.sizeDelta = new Vector2(d, d);
        }
    }
}
