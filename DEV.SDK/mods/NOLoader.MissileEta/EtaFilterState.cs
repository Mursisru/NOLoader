using System;
using NOLoader.HudCommon;
using System.Collections.Generic;
using UnityEngine;

namespace NOLoader.MissileEta
{
    internal sealed class EtaFilterState
    {
        private readonly float[] _rawRing;
        private int _rawCount;
        private int _rawIndex;

        internal float Displayed;
        internal float SmoothedClosure;
        internal float LastValidRaw;
        internal float LastValidTime;
        internal float LastShownText;
        internal bool HasDisplayed;

        internal EtaFilterState(int medianWindow)
        {
            int n = Mathf.Clamp(medianWindow, 1, 9);
            _rawRing = new float[n];
        }

        internal float Update(
            float? rawSample,
            float closure,
            float dist,
            float dt,
            float now,
            MissileEtaFilterSettings settings)
        {
            if (rawSample.HasValue && IsFinite(rawSample.Value))
            {
                PushRaw(rawSample.Value);
                float median = MedianRaw();
                LastValidRaw = median;
                LastValidTime = now;

                if (closure > 0f)
                {
                    float t = Mathf.Clamp01(dt * settings.ClosureSmoothHz);
                    SmoothedClosure = SmoothedClosure <= 0f
                        ? closure
                        : Mathf.Lerp(SmoothedClosure, closure, t);
                }

                float blend = Mathf.Clamp01(settings.BlendLosWeight);
                float target = blend > 0.001f
                    ? Mathf.Lerp(median, dist / Mathf.Max(SmoothedClosure, settings.MinClosureMps), blend)
                    : median;
                target = Mathf.Clamp(target, 0f, settings.MaxEtaSeconds);
                ApplyAsymmetricSmooth(target, dt, settings);
            }
            else if (HasDisplayed && now - LastValidTime < settings.HoldInvalidSeconds)
            {
                // Hold last displayed value during brief invalid samples.
            }
            else
            {
                HasDisplayed = false;
                Displayed = 0f;
                LastShownText = 0f;
            }

            return Displayed;
        }

        internal void Reset()
        {
            _rawCount = 0;
            _rawIndex = 0;
            Displayed = 0f;
            SmoothedClosure = 0f;
            LastValidRaw = 0f;
            LastValidTime = 0f;
            LastShownText = 0f;
            HasDisplayed = false;
        }

        internal bool TryGetDisplayText(int decimalPlaces, float quantizeStep, out string text)
        {
            text = null;
            if (!HasDisplayed)
                return false;

            float q = Mathf.Max(0.05f, quantizeStep);
            float quantized = Mathf.Round(Displayed / q) * q;
            if (Mathf.Abs(quantized - LastShownText) < q * 0.5f && LastShownText > 0f)
                quantized = LastShownText;

            LastShownText = quantized;
            text = decimalPlaces <= 0
                ? quantized.ToString("F0")
                : quantized.ToString("F1");
            return true;
        }

        private void ApplyAsymmetricSmooth(float target, float dt, MissileEtaFilterSettings settings)
        {
            if (!HasDisplayed)
            {
                Displayed = target;
                HasDisplayed = true;
                return;
            }

            float delta = target - Displayed;
            float maxStep = delta < 0f
                ? settings.MaxDecreasePerSec * dt
                : settings.MaxIncreasePerSec * dt;
            Displayed = Mathf.MoveTowards(Displayed, target, Mathf.Max(0.001f, maxStep));
            HasDisplayed = true;
        }

        private void PushRaw(float value)
        {
            _rawRing[_rawIndex] = value;
            _rawIndex = (_rawIndex + 1) % _rawRing.Length;
            if (_rawCount < _rawRing.Length)
                _rawCount++;
        }

        private float MedianRaw()
        {
            if (_rawCount == 0)
                return LastValidRaw;

            var scratch = new float[_rawCount];
            int start = (_rawIndex - _rawCount + _rawRing.Length) % _rawRing.Length;
            for (int i = 0; i < _rawCount; i++)
                scratch[i] = _rawRing[(start + i) % _rawRing.Length];
            Array.Sort(scratch);
            return scratch[_rawCount / 2];
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f;
        }
    }

    internal struct MissileEtaFilterSettings
    {
        internal float HoldInvalidSeconds;
        internal int RawMedianWindow;
        internal float ClosureSmoothHz;
        internal float MaxDecreasePerSec;
        internal float MaxIncreasePerSec;
        internal float MinClosureMps;
        internal float DisplayQuantizeStep;
        internal float BlendLosWeight;
        internal float MaxEtaSeconds;
    }

    internal sealed class EtaFilterRegistry
    {
        private readonly Dictionary<int, EtaFilterState> _eta = new Dictionary<int, EtaFilterState>();
        private readonly Dictionary<int, EtaFilterState> _arh = new Dictionary<int, EtaFilterState>();

        internal EtaFilterState GetEta(int id, int medianWindow)
        {
            if (!_eta.TryGetValue(id, out EtaFilterState state))
            {
                state = new EtaFilterState(medianWindow);
                _eta[id] = state;
            }
            return state;
        }

        internal EtaFilterState GetArh(int id, int medianWindow)
        {
            if (!_arh.TryGetValue(id, out EtaFilterState state))
            {
                state = new EtaFilterState(medianWindow);
                _arh[id] = state;
            }
            return state;
        }

        internal void Remove(int id)
        {
            _eta.Remove(id);
            _arh.Remove(id);
        }

        internal void Prune(HashSet<int> alive)
        {
            PruneDict(_eta, alive);
            PruneDict(_arh, alive);
        }

        private static void PruneDict(Dictionary<int, EtaFilterState> dict, HashSet<int> alive)
        {
            var remove = new List<int>();
            foreach (int key in dict.Keys)
            {
                if (!alive.Contains(key))
                    remove.Add(key);
            }
            for (int i = 0; i < remove.Count; i++)
                dict.Remove(remove[i]);
        }
    }
}
