using UnityEngine;
using NOLoader.HudCommon;
using UnityEngine.UI;

namespace NOLoader.MissileLaunchArcHud
{
    internal sealed class NezTargetMarkersView
    {
        private readonly GameObject _root;
        private readonly Image _approachRing;
        private readonly Image _innerRing;
        private readonly Image _outerRing;
        private readonly RectTransform _approachRect;
        private readonly RectTransform _innerRect;
        private readonly RectTransform _outerRect;
        private readonly Transform _canvasRoot;

        internal NezTargetMarkersView(Transform flightHudCanvas)
        {
            Transform parent = HudScreenPlacement.ResolveMarkersParent(flightHudCanvas);
            _canvasRoot = parent;

            _root = new GameObject("MissileLaunchArcHud_NEZ_Target");
            var rootRect = _root.AddComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;
            rootRect.localScale = Vector3.one;

            _approachRing = CreateRing("NEZ_Approach", NezTargetRingSprite.GetThinInner(), out _approachRect);
            _innerRing = CreateRing("NEZ_Inner", NezTargetRingSprite.GetThinInner(), out _innerRect);
            _outerRing = CreateRing("NEZ_Outer", NezTargetRingSprite.GetThinOuter(), out _outerRect);

            ApplyRingSizes();
            _root.SetActive(false);
            HideAll();
        }

        internal bool NeedsRebuild(Transform flightHudCanvas)
        {
            Transform want = HudScreenPlacement.ResolveMarkersParent(flightHudCanvas);
            return _canvasRoot != want;
        }

        internal void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }

        internal void Apply(
            LaunchArcGameApi.LaunchArcSnapshot snap,
            Vector3 hudCenterScreen,
            Vector3 liveTargetPosition,
            bool hasLiveTarget,
            bool insideMainArc)
        {
            ApplyRingSizes();

            if (!hasLiveTarget
                || !insideMainArc
                || snap.NezPhase == LaunchArcNezPhase.None
                || snap.NezPhase == LaunchArcNezPhase.Calm)
            {
                HideAll();
                return;
            }

            _root.transform.SetAsLastSibling();
            PlaceRing(_approachRect, liveTargetPosition);
            PlaceRing(_innerRect, liveTargetPosition);
            PlaceRing(_outerRect, liveTargetPosition);

            Color ringColor = LaunchArcHudPalette.GetTargetRingColor(snap);
            Material ringMaterial = LaunchArcHudPalette.GetTargetRingMaterial();

            if (snap.NezPhase == LaunchArcNezPhase.Approaching)
            {
                _approachRing.enabled = ringColor.a > 0.001f;
                LaunchArcHudPalette.ApplyRingStyle(_approachRing, ringColor, ringMaterial);
                _innerRing.enabled = false;
                _outerRing.enabled = false;
                return;
            }

            if (snap.NezPhase == LaunchArcNezPhase.InsideNez)
            {
                _approachRing.enabled = false;
                bool show = ringColor.a > 0.001f;
                _innerRing.enabled = show;
                _outerRing.enabled = show;
                LaunchArcHudPalette.ApplyRingStyle(_innerRing, ringColor, ringMaterial);
                LaunchArcHudPalette.ApplyRingStyle(_outerRing, ringColor, ringMaterial);
            }
        }

        internal void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        private Image CreateRing(string name, Sprite sprite, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_root.transform, false);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            img.material = null;
            img.sprite = sprite;
            img.preserveAspect = true;

            rect = img.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            img.enabled = false;
            return img;
        }

        private void ApplyRingSizes()
        {
            float innerPx = MissileLaunchArcHudConfigCache.NezInnerRingSizePx;
            SetRingSize(_approachRect, innerPx);
            SetRingSize(_innerRect, innerPx);
            float gap = Mathf.Max(1.5f, MissileLaunchArcHudConfigCache.NezInnerOuterGapPx);
            SetRingSize(_outerRect, innerPx + gap);
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

        private void HideAll()
        {
            if (_approachRing != null)
                _approachRing.enabled = false;
            if (_innerRing != null)
                _innerRing.enabled = false;
            if (_outerRing != null)
                _outerRing.enabled = false;
        }
    }
}
