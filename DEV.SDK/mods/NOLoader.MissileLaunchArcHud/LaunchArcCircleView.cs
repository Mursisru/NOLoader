using UnityEngine;
using NOLoader.HudCommon;
using UnityEngine.UI;

namespace NOLoader.MissileLaunchArcHud
{
    internal sealed class LaunchArcCircleView
    {
        private readonly GameObject _root;
        private readonly Image _image;
        private readonly RectTransform _rect;
        private readonly Transform _canvasRoot;

        internal LaunchArcCircleView(Transform flightHudCanvas, Transform hudCenterAnchor)
        {
            _canvasRoot = flightHudCanvas;
            _root = new GameObject("MissileLaunchArcHud_OuterCircle");
            _root.transform.SetParent(flightHudCanvas, false);

            _image = _root.AddComponent<Image>();
            _image.raycastTarget = false;
            _image.type = Image.Type.Simple;
            _image.material = null;
            _image.sprite = LaunchArcRingSprite.Get(fullCircle: true);

            _rect = _image.rectTransform;
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            float refPx = LaserOuterCircleTemplate.GetReferenceDiameterPx();
            _rect.sizeDelta = new Vector2(refPx, refPx);

            SyncAnchor(hudCenterAnchor);
        }

        internal bool NeedsRebuild(Transform flightHudCanvas)
        {
            return _canvasRoot != flightHudCanvas;
        }

        internal void SetVisible(bool visible)
        {
            if (_image != null)
                _image.enabled = visible;
        }

        internal void Apply(LaunchArcGameApi.LaunchArcSnapshot snap, Transform hudCenterAnchor)
        {
            SyncAnchor(hudCenterAnchor);
            float refPx = LaserOuterCircleTemplate.GetReferenceDiameterPx();
            float diameter = HudScreenScale.Px(refPx);
            _rect.sizeDelta = new Vector2(diameter, diameter);
            _root.transform.localScale = Vector3.one * snap.CircleScale;

            bool useFullRing = !MissileLaunchArcHudConfigCache.PartialRing
                || snap.CircleScale < MissileLaunchArcHudConfigCache.FullRingBelowScale;

            if (!MissileLaunchArcHudConfigCache.PartialRing)
            {
                LaserOuterCircleTemplate.ApplyTo(_image);
                if (_image.sprite == null)
                    _image.sprite = LaunchArcRingSprite.Get(fullCircle: true);
            }
            else
            {
                _image.material = null;
                _image.sprite = LaunchArcRingSprite.Get(useFullRing);
            }

            ApplyArcColor(snap);

            if (MissileLaunchArcHudConfigCache.DrawBehindHud)
                _root.transform.SetAsFirstSibling();
        }

        private void ApplyArcColor(LaunchArcGameApi.LaunchArcSnapshot snap)
        {
            LaunchArcHudPalette.ApplyRingStyle(
                _image,
                LaunchArcHudPalette.GetArcRingColor(snap),
                LaunchArcHudPalette.GetArcRingMaterial());
        }

        internal void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        private void SyncAnchor(Transform hudCenterAnchor)
        {
            if (hudCenterAnchor == null)
                return;

            _rect.position = hudCenterAnchor.position;
            _rect.localRotation = Quaternion.identity;
        }
    }
}
