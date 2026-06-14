using System.Collections.Generic;
using System.Diagnostics;
using NOLoader.API;
using NOLoader.Core.EngineTweaker;
using NOLoader.Core.Mods;
using NOLoader.Core.Runtime.Balance;
using UnityEngine;

namespace NOLoader.Core.Runtime.Perf
{
    internal static class ModTickScheduler
    {
        private static readonly List<LoadedModEntry> FastMods = new List<LoadedModEntry>();
        private static readonly List<LoadedModEntry> NormalMods = new List<LoadedModEntry>();
        private static readonly List<LoadedModEntry> SlowMods = new List<LoadedModEntry>();
        private static float _nextSlowTime;
        private static float _nextTweakerLogTime;
        private static int _normalFrame;

        public static bool HasTickMods =>
            FastMods.Count > 0 || NormalMods.Count > 0 || SlowMods.Count > 0;

        public static void Register(LoadedModEntry entry)
        {
            if (entry.Fast != null && !FastMods.Contains(entry))
                FastMods.Add(entry);
            if ((entry.Normal != null || entry.Background != null) && !NormalMods.Contains(entry))
                NormalMods.Add(entry);
            if (entry.Slow != null && !SlowMods.Contains(entry))
                SlowMods.Add(entry);
        }

        public static void Unregister(LoadedModEntry entry)
        {
            FastMods.Remove(entry);
            NormalMods.Remove(entry);
            SlowMods.Remove(entry);
        }

        public static LoadedModEntry CreateEntry(LoadedMod mod)
        {
            var entry = new LoadedModEntry { Mod = mod };
            if (mod.Instance is INOModTickFast fast)
                entry.Fast = fast;
            if (mod.Instance is INOModTickNormal normal)
                entry.Normal = normal;
            if (mod.Instance is INOModTickSlow slow)
                entry.Slow = slow;
            if (mod.Instance is INOModBackgroundWork background)
                entry.Background = background;
            return entry;
        }

        public static void TickFast(float dt)
        {
            for (int i = 0; i < FastMods.Count; i++)
                InvokeFast(FastMods[i], dt);
        }

        public static void TickNormal()
        {
            int stride = RuntimeConfig.NormalTickStride;
            if (stride < 1)
                stride = 1;

            _normalFrame++;
            if ((_normalFrame % stride) != 0)
                return;

            float dt = Time.deltaTime * stride;
            for (int i = 0; i < NormalMods.Count; i++)
            {
                LoadedModEntry entry = NormalMods[i];
                int effective = stride << entry.DemoteLevel;
                if ((_normalFrame % effective) != 0)
                    continue;
                InvokeNormal(entry, dt * (1 << entry.DemoteLevel));
            }
        }

        public static void TickSlow()
        {
            float interval = RuntimeConfig.SlowTickIntervalSec;
            if (interval <= 0f)
                interval = 1f;

            float now = Time.unscaledTime;
            if (now < _nextSlowTime)
                return;

            _nextSlowTime = now + interval;
            if (now >= _nextTweakerLogTime)
            {
                _nextTweakerLogTime = now + 30f;
                if (!RuntimeConfig.RdytuMiniEnabled)
                    NOEngineTweakerBootstrap.LogPeriodicStats();
            }

            for (int i = 0; i < SlowMods.Count; i++)
            {
                LoadedModEntry entry = SlowMods[i];
                float scaled = interval * (1 << entry.DemoteLevel);
                InvokeSlow(entry, scaled);
            }
        }

        private static void InvokeFast(LoadedModEntry entry, float dt)
        {
            if (entry.Fast == null)
                return;

            long start = Stopwatch.GetTimestamp();
            try
            {
                var ctx = BuildContext(entry.Mod);
                entry.Fast.OnFastUpdate(ref ctx, dt);
            }
            catch (System.Exception ex)
            {
                ModLifecycleManager.FlagModForMissionBlock(Label(entry.Mod), ex);
            }

            ModExecutionBudget.Instance.Record(entry.Mod.Manifest.IdHash, ElapsedMs(start));
        }

        private static void InvokeNormal(LoadedModEntry entry, float dt)
        {
            if (entry.Normal == null && entry.Background == null)
                return;

#if !NOLoader_DEV
            if (entry.Background != null && Runtime.RuntimeConfig.CoreBalancerEnabled)
            {
                InvokeNormalBackground(entry, dt);
                return;
            }
#endif

            if (entry.Normal == null)
                return;

            long start = Stopwatch.GetTimestamp();
            try
            {
                var ctx = BuildContext(entry.Mod);
                entry.Normal.OnNormalUpdate(ref ctx, dt);
            }
            catch (System.Exception ex)
            {
                ModLifecycleManager.FlagModForMissionBlock(Label(entry.Mod), ex);
            }

            ModExecutionBudget.Instance.Record(entry.Mod.Manifest.IdHash, ElapsedMs(start));
        }

#if !NOLoader_DEV
        private static void InvokeNormalBackground(LoadedModEntry entry, float dt)
        {
            long start = Stopwatch.GetTimestamp();
            var ctx = BuildContext(entry.Mod);

            if (entry.HasPendingApply)
            {
                try
                {
                    ThreadGuard.AssertMainThread();
                    entry.Background!.OnApplyResults(ref ctx, in entry.PendingOutput);
                }
                catch (System.Exception ex)
                {
                    ModLifecycleManager.FlagModForMissionBlock(Label(entry.Mod), ex);
                }

                entry.HasPendingApply = false;
            }

            ModWorkInput input = default;
            try
            {
                ThreadGuard.AssertMainThread();
                entry.Background!.OnCaptureInputs(ref ctx, ref input);
                input.FrameId = Time.frameCount;
            }
            catch (System.Exception ex)
            {
                ModLifecycleManager.FlagModForMissionBlock(Label(entry.Mod), ex);
                ModExecutionBudget.Instance.Record(entry.Mod.Manifest.IdHash, ElapsedMs(start));
                return;
            }

            entry.PendingInput = input;
            LoadedModEntry capturedEntry = entry;
            NOMulticoreScheduler.Instance.RunCompute(
                () =>
                {
                    ModWorkOutput output = default;
                    output.FrameId = capturedEntry.PendingInput.FrameId;
                    capturedEntry.Background!.OnCompute(in capturedEntry.PendingInput, ref output);
                    capturedEntry.PendingOutput = output;
                },
                () =>
                {
                    capturedEntry.HasPendingApply = true;
                });

            ModExecutionBudget.Instance.Record(entry.Mod.Manifest.IdHash, ElapsedMs(start));
        }
#endif

        private static void InvokeSlow(LoadedModEntry entry, float dt)
        {
            if (entry.Slow == null)
                return;

            long start = Stopwatch.GetTimestamp();
            try
            {
                var ctx = BuildContext(entry.Mod);
                entry.Slow.OnSlowUpdate(ref ctx, dt);
            }
            catch (System.Exception ex)
            {
                ModLifecycleManager.FlagModForMissionBlock(Label(entry.Mod), ex);
            }

            ModExecutionBudget.Instance.Record(entry.Mod.Manifest.IdHash, ElapsedMs(start));
        }

        private static NOModContext BuildContext(LoadedMod mod)
        {
            return new NOModContext
            {
                GameRoot = ModLifecycleManager.GameRootPath,
                ModRoot = mod.Manifest.FolderPath,
                ModId = mod.Manifest.Id,
                ModIdHash = mod.Manifest.IdHash,
                ModVersion = mod.Manifest.Version,
                Stage = mod.Manifest.LoadStage,
                Services = NOModPerfBootstrap.CreateServices(requestWorld: false)
            };
        }

        private static string Label(LoadedMod mod)
        {
            return !string.IsNullOrEmpty(mod.Manifest.Id)
                ? mod.Manifest.Id
                : mod.Manifest.IdHash.ToString("X8");
        }

        private static double ElapsedMs(long start)
        {
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        internal static bool TryGetEntryByHash(int modIdHash, out LoadedModEntry? entry)
        {
            entry = FindEntry(NormalMods, modIdHash);
            if (entry != null)
                return true;
            entry = FindEntry(FastMods, modIdHash);
            if (entry != null)
                return true;
            entry = FindEntry(SlowMods, modIdHash);
            return entry != null;
        }

        private static LoadedModEntry? FindEntry(List<LoadedModEntry> list, int modIdHash)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Mod.Manifest.IdHash == modIdHash)
                    return list[i];
            }

            return null;
        }

        internal static void SetDemoteLevel(int modIdHash, int level)
        {
            ApplyDemote(FastMods, modIdHash, level);
            ApplyDemote(NormalMods, modIdHash, level);
            ApplyDemote(SlowMods, modIdHash, level);
        }

        private static void ApplyDemote(List<LoadedModEntry> list, int modIdHash, int level)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Mod.Manifest.IdHash == modIdHash)
                    list[i].DemoteLevel = level;
            }
        }
    }
}
