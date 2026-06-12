using System.Collections.Generic;
using NOLoader.HudCommon;
using UnityEngine;
using UnityEngine.UI;

namespace NOLoader.MissileEta
{
    internal sealed class OffScreenArrowView
    {
        private readonly Transform _parent;
        private readonly List<ArrowEntry> _entries = new List<ArrowEntry>();
        private readonly Dictionary<int, Vector2> _smoothedDir = new Dictionary<int, Vector2>();

        private sealed class ArrowEntry
        {
            internal GameObject Root;
            internal PrismPointerGraphic Prism;
            internal Image TextBackdrop;
            internal Text EtaText;
            internal Transform PrismTransform;
            internal Transform TextTransform;
            internal RectTransform PrismRect;
        }

        internal OffScreenArrowView(Transform parent)
        {
            _parent = parent;
        }

        internal bool NeedsRebuild(Transform parent) => _parent != parent;

        internal void Apply(IReadOnlyList<MissileLabelSlot> slots)
        {
            int arrowCount = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Mode == MissileLabelMode.IncomingOffScreen
                    || slots[i].Mode == MissileLabelMode.OwnOffScreen)
                    arrowCount++;
            }

            EnsureCount(arrowCount);

            int arrowIndex = 0;
            Font font = HudFontHelper.ResolveHudFont();
            for (int i = 0; i < slots.Count; i++)
            {
                MissileLabelSlot slot = slots[i];
                if (slot.Mode != MissileLabelMode.IncomingOffScreen
                    && slot.Mode != MissileLabelMode.OwnOffScreen)
                    continue;

                ArrowEntry e = _entries[arrowIndex++];
                e.Root.SetActive(true);
                e.Root.transform.SetAsLastSibling();

                int id = slot.Missile != null ? slot.Missile.GetInstanceID() : arrowIndex;
                Vector2 dir = SmoothDirection(id, slot.AngleDeg);

                bool incoming = slot.Mode == MissileLabelMode.IncomingOffScreen;
                Color accent = incoming
                    ? MissileEtaPalette.GetIncomingArrowColor()
                    : MissileEtaPalette.GetOwnArrowColor();

                float len = HudScreenScale.Px(MissileEtaConfigCache.ArrowLengthPx);
                float baseW = HudScreenScale.Px(14f);
                e.Prism.SetGeometry(len, baseW, baseW * 0.2f, 0.35f, 0.45f, accent);

                float fontPx = incoming
                    ? MissileEtaConfigCache.IncomingFontSizePx
                    : MissileEtaConfigCache.OwnFontSizePx;
                int fontSize = HudLabelBackdrop.ScaleFontPx(fontPx);

                e.EtaText.font = font;
                e.EtaText.fontSize = fontSize;
                e.EtaText.color = HudLabelBackdrop.TextOnAccent(accent);
                e.EtaText.text = slot.EtaText ?? string.Empty;
                HudLabelBackdrop.FitBackdropToText(e.TextBackdrop, e.EtaText, accent);

                OffScreenArrowLayout.Place(
                    slot.ScreenPosition,
                    dir,
                    len,
                    e.PrismTransform,
                    e.PrismRect,
                    e.EtaText,
                    e.TextBackdrop,
                    e.TextTransform);
            }

            for (int i = arrowIndex; i < _entries.Count; i++)
                _entries[i].Root.SetActive(false);
        }

        internal void HideAll()
        {
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].Root.SetActive(false);
        }

        internal void PruneSmoothed(HashSet<int> alive)
        {
            var remove = new List<int>();
            foreach (int key in _smoothedDir.Keys)
            {
                if (!alive.Contains(key))
                    remove.Add(key);
            }
            for (int i = 0; i < remove.Count; i++)
                _smoothedDir.Remove(remove[i]);
        }

        internal void Dispose()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Root != null)
                    Object.Destroy(_entries[i].Root);
            }
            _entries.Clear();
            _smoothedDir.Clear();
        }

        private Vector2 SmoothDirection(int id, float angleDeg)
        {
            Vector2 raw = new Vector2(
                Mathf.Cos(angleDeg * Mathf.Deg2Rad),
                Mathf.Sin(angleDeg * Mathf.Deg2Rad));

            if (!_smoothedDir.TryGetValue(id, out Vector2 smoothed) || smoothed.sqrMagnitude < 0.0001f)
            {
                _smoothedDir[id] = raw;
                return raw;
            }

            float smooth = Mathf.Clamp01(MissileEtaConfigCache.ArrowPositionSmoothing);
            Vector2 lerped = Vector2.Lerp(smoothed, raw, 1f - smooth);
            float maxStep = HudScreenScale.Px(MissileEtaConfigCache.ArrowMaxScreenStepPx) * Time.deltaTime;
            _smoothedDir[id] = Vector2.MoveTowards(smoothed, lerped, maxStep);
            return _smoothedDir[id].normalized;
        }

        private void EnsureCount(int count)
        {
            while (_entries.Count < count)
            {
                int idx = _entries.Count;
                var root = new GameObject($"MissileEta_Arrow_{idx}");
                root.transform.SetParent(_parent, false);

                var prismGo = new GameObject("Prism");
                prismGo.transform.SetParent(root.transform, false);
                var prism = prismGo.AddComponent<PrismPointerGraphic>();
                prism.raycastTarget = false;
                var prismRect = prism.rectTransform;
                prismRect.anchorMin = Vector2.zero;
                prismRect.anchorMax = Vector2.zero;
                prismRect.pivot = new Vector2(0f, 0.5f);

                Image textBg = HudLabelBackdrop.CreateBackdrop(root.transform, "EtaBg");
                var textGo = new GameObject("Eta");
                textGo.transform.SetParent(root.transform, false);
                var text = textGo.AddComponent<Text>();
                text.font = HudFontHelper.ResolveHudFont();
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                var textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.zero;
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.sizeDelta = Vector2.zero;

                _entries.Add(new ArrowEntry
                {
                    Root = root,
                    Prism = prism,
                    TextBackdrop = textBg,
                    EtaText = text,
                    PrismTransform = prismGo.transform,
                    TextTransform = textGo.transform,
                    PrismRect = prismRect,
                });
            }
        }
    }
}
