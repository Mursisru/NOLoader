using System.Collections.Generic;
using NOLoader.HudCommon;
using UnityEngine;
using UnityEngine.UI;

namespace NOLoader.MissileEta
{
    internal sealed class MissileLabelPoolView
    {
        private readonly Transform _parent;
        private readonly List<LabelEntry> _entries = new List<LabelEntry>();

        private Font _font;
        private Material _material;

        private sealed class LabelEntry
        {
            internal GameObject Root;
            internal Image EtaBackdrop;
            internal Image ArhBackdrop;
            internal Text EtaText;
            internal Text ArhText;
            internal Transform EtaTransform;
            internal Transform ArhTransform;
        }

        internal MissileLabelPoolView(Transform parent)
        {
            _parent = parent;
            CaptureHudStyle();
            HudScreenPlacement.LogCanvasChain(parent);
            MissileEtaDiagLog.Info(
                $"LabelPool ctor: parent='{parent.name}' font={(_font != null ? _font.name : "NULL")} material={(_material != null)}");
        }

        internal bool NeedsRebuild(Transform parent) => _parent != parent;

        internal void Apply(IReadOnlyList<MissileLabelSlot> slots)
        {
            int onScreenCount = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Mode == MissileLabelMode.OwnOnScreen
                    || slots[i].Mode == MissileLabelMode.IncomingOnScreen)
                    onScreenCount++;
            }

            EnsureCount(onScreenCount);
            CaptureHudStyle();

            int entryIndex = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                MissileLabelSlot slot = slots[i];
                if (slot.Mode != MissileLabelMode.OwnOnScreen
                    && slot.Mode != MissileLabelMode.IncomingOnScreen)
                    continue;

                LabelEntry e = _entries[entryIndex++];
                e.Root.SetActive(true);
                e.Root.transform.SetAsLastSibling();

                bool incoming = slot.Mode == MissileLabelMode.IncomingOnScreen;
                Color accent = incoming ? MissileEtaPalette.GetIncomingColor() : MissileEtaPalette.GetOwnColor();
                float fontPx = incoming ? MissileEtaConfigCache.IncomingFontSizePx : MissileEtaConfigCache.OwnFontSizePx;
                int fontSize = HudLabelBackdrop.ScaleFontPx(fontPx);
                Color textColor = HudLabelBackdrop.TextOnAccent(accent);
                string etaText = slot.EtaText ?? string.Empty;

                e.EtaText.font = _font;
                e.EtaText.material = !incoming && _material != null ? _material : null;
                e.EtaText.fontSize = fontSize;
                e.EtaText.color = textColor;
                e.EtaText.text = etaText;

                HudLabelBackdrop.FitBackdropToText(e.EtaBackdrop, e.EtaText, accent);
                Vector2 etaPos = slot.ScreenPosition + new Vector2(0f, HudLabelBackdrop.VerticalOffsetPx());
                HudScreenPlacement.PlaceTransform(e.EtaTransform, etaPos);
                HudScreenPlacement.PlaceTransform(e.EtaBackdrop.transform, etaPos);

                if (slot.ShowArh && !string.IsNullOrEmpty(slot.ArhText))
                {
                    int arhSize = Mathf.Max(9, fontSize - 3);
                    Color arhAccent = ResolveArhAccent(slot.ArhPhase, accent);
                    bool arhVisible = arhAccent.a > 0.001f;

                    e.ArhText.enabled = arhVisible;
                    e.ArhBackdrop.enabled = arhVisible;
                    if (arhVisible)
                    {
                        Color arhTextColor = HudLabelBackdrop.TextOnAccent(arhAccent);
                        e.ArhText.font = _font;
                        e.ArhText.fontSize = arhSize;
                        e.ArhText.color = arhTextColor;
                        e.ArhText.text = slot.ArhText;
                        HudLabelBackdrop.FitBackdropToText(e.ArhBackdrop, e.ArhText, arhAccent);
                        Vector2 arhPos = slot.ScreenPosition + new Vector2(0f, -HudScreenScale.Px(14f));
                        HudScreenPlacement.PlaceTransform(e.ArhTransform, arhPos);
                        HudScreenPlacement.PlaceTransform(e.ArhBackdrop.transform, arhPos);
                    }
                }
                else
                {
                    e.ArhText.enabled = false;
                    e.ArhBackdrop.enabled = false;
                }
            }

            for (int i = entryIndex; i < _entries.Count; i++)
                _entries[i].Root.SetActive(false);
        }

        internal void HideAll()
        {
            for (int i = 0; i < _entries.Count; i++)
                _entries[i].Root.SetActive(false);
        }

        internal void Dispose()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Root != null)
                    Object.Destroy(_entries[i].Root);
            }
            _entries.Clear();
        }

        private void EnsureCount(int count)
        {
            while (_entries.Count < count)
            {
                int idx = _entries.Count;
                var root = new GameObject($"MissileEta_Label_{idx}");
                root.transform.SetParent(_parent, false);

                Image etaBg = HudLabelBackdrop.CreateBackdrop(root.transform, "EtaBg");
                var etaGo = new GameObject("Eta");
                etaGo.transform.SetParent(root.transform, false);
                var etaText = etaGo.AddComponent<Text>();
                ConfigureText(etaText);

                Image arhBg = HudLabelBackdrop.CreateBackdrop(root.transform, "ArhBg");
                var arhGo = new GameObject("Arh");
                arhGo.transform.SetParent(root.transform, false);
                var arhText = arhGo.AddComponent<Text>();
                ConfigureText(arhText);

                _entries.Add(new LabelEntry
                {
                    Root = root,
                    EtaBackdrop = etaBg,
                    ArhBackdrop = arhBg,
                    EtaText = etaText,
                    ArhText = arhText,
                    EtaTransform = etaGo.transform,
                    ArhTransform = arhGo.transform,
                });
            }
        }

        private static void ConfigureText(Text text)
        {
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
        }

        private void CaptureHudStyle()
        {
            _material = MissileEtaPalette.GetOwnMaterial();
            _font = HudFontHelper.ResolveHudFont();
        }

        private static Color ResolveArhAccent(ArhDisplayPhase phase, Color ownAccent)
        {
            switch (phase)
            {
                case ArhDisplayPhase.Search:
                    return MissileEtaPalette.GetArhSearchBlinkColor();
                case ArhDisplayPhase.ActiveLock:
                    return MissileEtaPalette.GetArhActiveBlinkColor();
                case ArhDisplayPhase.TargetLost:
                    return MissileEtaPalette.GetArhLostColor();
                case ArhDisplayPhase.NoTarget:
                    return MissileEtaPalette.GetArhNoTargetColor();
                default:
                    return ownAccent;
            }
        }
    }
}
