using System;
using System.Collections.Generic;
using NOLoader.API;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using NOLoader.Core.Runtime.Balance;
using UnityEngine;

namespace NOLoader.Core.Runtime.Perf
{
    internal sealed class ModExecutionBudget : IModExecutionBudgetView
    {
        public static readonly ModExecutionBudget Instance = new ModExecutionBudget();

        private readonly Dictionary<int, double> _emaMs = new Dictionary<int, double>();
        private readonly Dictionary<int, int> _demoteLevel = new Dictionary<int, int>();
        private double _frameMs;
        private float _stableSince = -1f;
        private const double EmaAlpha = 0.15;

        public int GetDemoteLevel(int modIdHash)
        {
            return _demoteLevel.TryGetValue(modIdHash, out int level) ? level : 0;
        }

        public void Record(int modIdHash, double elapsedMs)
        {
            _frameMs += elapsedMs;
            if (!_emaMs.TryGetValue(modIdHash, out double prev))
                prev = elapsedMs;
            _emaMs[modIdHash] = prev + EmaAlpha * (elapsedMs - prev);
        }

        public void EndFrame()
        {
            if (!ModTickScheduler.HasTickMods)
                return;

            double frameMs = _frameMs;
            MainThreadMetrics.RecordFrameMs(frameMs);
            MainThreadMetrics.TryLogPeriodic();

            double budget = RuntimeConfig.ModBudgetMs;
            if (budget <= 0)
            {
                _frameMs = 0;
                return;
            }

            if (_frameMs <= budget)
            {
                if (_stableSince < 0f)
                    _stableSince = Time.unscaledTime;
                else if (Time.unscaledTime - _stableSince >= 60f)
                    RelaxDemotions();
            }
            else
            {
                _stableSince = -1f;
#if !NOLoader_DEV
                if (!TryOffloadInsteadOfDemote())
                    DemoteHeaviest();
#else
                DemoteHeaviest();
#endif
            }

            _frameMs = 0;
        }

#if !NOLoader_DEV
        private bool TryOffloadInsteadOfDemote()
        {
            if (!RuntimeConfig.CoreBalancerEnabled)
                return false;

            int bestHash = 0;
            double bestMs = 0;
            foreach (KeyValuePair<int, double> kv in _emaMs)
            {
                if (kv.Value > bestMs)
                {
                    bestMs = kv.Value;
                    bestHash = kv.Key;
                }
            }

            if (bestHash == 0)
                return false;

            if (!ModTickScheduler.TryGetEntryByHash(bestHash, out LoadedModEntry? entry) || entry == null)
                return false;

            if (entry.Background == null || entry.OffloadActive)
                return false;

            entry.OffloadActive = true;
            RingBufferLog.WriteAscii("[CoreBalancer] offload mod=" + bestHash.ToString("X8")
                + " queue=" + NOMulticoreScheduler.Instance.QueueDepth);
            return true;
        }
#endif

        private void DemoteHeaviest()
        {
            int bestHash = 0;
            double bestMs = 0;
            foreach (KeyValuePair<int, double> kv in _emaMs)
            {
                if (kv.Value > bestMs)
                {
                    bestMs = kv.Value;
                    bestHash = kv.Key;
                }
            }

            if (bestHash == 0)
                return;

            int level = _demoteLevel.TryGetValue(bestHash, out int cur) ? cur : 0;
            if (level >= 2)
                return;

            level++;
            _demoteLevel[bestHash] = level;
            ModTickScheduler.SetDemoteLevel(bestHash, level);
            RingBufferLog.WriteAscii("[Perf] demote mod=" + bestHash.ToString("X8") + " level=" + level);
        }

        private void RelaxDemotions()
        {
            if (_demoteLevel.Count == 0)
                return;

            var keys = new List<int>(_demoteLevel.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int hash = keys[i];
                int level = _demoteLevel[hash];
                if (level <= 0)
                    continue;

                level--;
                if (level <= 0)
                    _demoteLevel.Remove(hash);
                else
                    _demoteLevel[hash] = level;

                ModTickScheduler.SetDemoteLevel(hash, Math.Max(0, level));
            }
        }

        internal void ApplyAnalyzerDemotion(int modIdHash, int level)
        {
            if (level <= 0)
                return;

            _demoteLevel[modIdHash] = level;
            ModTickScheduler.SetDemoteLevel(modIdHash, level);
        }
    }
}
