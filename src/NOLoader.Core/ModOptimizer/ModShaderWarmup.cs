#if !NOLoader_DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NOLoader.API.Manifest;
using NOLoader.Core.Logging;
using NOLoader.Core.Mods;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.ModOptimizer
{
    internal static class ModShaderWarmup
    {
        private static bool _ran;

        internal static void RunOnMissionReady()
        {
            if (_ran || !ModOptimizerBootstrap.IsShaderWarmupActive)
                return;

            _ran = true;
            if (!UnityMainThread.IsMainThread)
                return;

            var entries = new List<(string ModRoot, ModWarmupSpec Spec)>();
            foreach (LoadedMod mod in ModLifecycleManager.AllMods)
            {
                if (!mod.Loaded || mod.Manifest.Warmup == null)
                    continue;

                entries.Add((mod.Manifest.FolderPath, mod.Manifest.Warmup));
            }

            if (entries.Count == 0)
            {
                RingBufferLog.WriteAscii("[ModOpt] warmup skipped (no manifest entries)");
                return;
            }

            long start = Stopwatch.GetTimestamp();
            int shaderCount = 0;
            int materialCount = 0;
            double budgetMs = RuntimeConfig.ModShaderWarmupBudgetMs;

            foreach ((string modRoot, ModWarmupSpec spec) in entries)
            {
                foreach (string shaderName in spec.Shaders)
                {
                    if (WarmShader(shaderName))
                        shaderCount++;
                    if (ElapsedMs(start) >= budgetMs)
                        break;
                }

                foreach (string materialPath in spec.Materials)
                {
                    if (WarmMaterialPath(modRoot, materialPath))
                        materialCount++;
                    if (ElapsedMs(start) >= budgetMs)
                        break;
                }

                if (ElapsedMs(start) >= budgetMs)
                    break;
            }

            RingBufferLog.WriteAscii("[ModOpt] warmup materials=" + materialCount
                + " shaders=" + shaderCount
                + " ms=" + ElapsedMs(start).ToString("0.0"));
        }

        private static bool WarmShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
                return false;

            Shader? shader = Shader.Find(shaderName);
            if (shader == null)
                return false;

            var mat = new Material(shader);
            mat.SetPass(0);
            UnityEngine.Object.Destroy(mat);
            return true;
        }

        private static bool WarmMaterialPath(string modRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return false;

            string full = System.IO.Path.Combine(modRoot, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(full))
                return false;

            string stem = System.IO.Path.GetFileNameWithoutExtension(full);
            return WarmShader(stem) || WarmShader("Standard");
        }

        private static double ElapsedMs(long startTicks)
        {
            long delta = Stopwatch.GetTimestamp() - startTicks;
            return delta * 1000.0 / Stopwatch.Frequency;
        }
    }
}
#endif
