using System;
using System.Collections.Generic;
using NOLoader.Core.Logging;
using UnityEngine;

namespace NOLoader.Core.Runtime.Balance
{
    internal static class MainThreadMetrics
    {
        private const int WindowSize = 128;
        private static readonly double[] _samples = new double[WindowSize];
        private static int _index;
        private static int _count;
        private static float _nextLogTime = -1f;

        internal static void RecordFrameMs(double ms)
        {
            _samples[_index] = ms;
            _index = (_index + 1) % WindowSize;
            if (_count < WindowSize)
                _count++;
        }

        internal static void TryLogPeriodic()
        {
            if (_count < 16)
                return;

            float now = Time.unscaledTime;
            if (_nextLogTime >= 0f && now < _nextLogTime)
                return;

            _nextLogTime = now + 30f;

            var copy = new double[_count];
            int start = _count < WindowSize ? 0 : _index;
            for (int i = 0; i < _count; i++)
                copy[i] = _samples[(start + i) % WindowSize];

            Array.Sort(copy);
            int p95Index = Math.Min(copy.Length - 1, (int)Math.Ceiling(copy.Length * 0.95) - 1);
            double p95 = copy[p95Index];
            double avg = 0;
            for (int i = 0; i < copy.Length; i++)
                avg += copy[i];
            avg /= copy.Length;

            RingBufferLog.WriteAscii("[CoreBalancer] main_ms avg="
                + avg.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " p95=" + p95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
