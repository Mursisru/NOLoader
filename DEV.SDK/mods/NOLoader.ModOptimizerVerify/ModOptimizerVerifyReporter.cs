using System;
using System.IO;
using System.Reflection;
using NOLoader.API;
using UnityEngine;

namespace NOLoader.ModOptimizerVerify
{
    internal static class ModOptimizerVerifyReporter
    {
        internal static void LogOnLoad(ref NOModContext ctx)
        {
            if (ModOptimizerVerifyState.OnLoadLogged)
                return;

            ModOptimizerVerifyState.OnLoadLogged = true;
            ModOptimizerVerifyState.TargetSpawnCount = ResolveSpawnCount(ctx.ModRoot);
            ModOptimizerVerifyLogger.Phase("OnLoad");
            ModOptimizerVerifyLogger.Info("mod=" + ctx.ModId + " DEV5O1 field test (NOModOptimizer)");
            ModOptimizerVerifyLogger.Pass("manifest", "tick interfaces only (no magic Update)");
            ModOptimizerVerifyLogger.Info("covers: reflectionBake warmup sceneLocator Find redirect collision");
            ModOptimizerVerifyLogger.Info("spawn_count=" + ModOptimizerVerifyState.TargetSpawnCount + " (lite=3, full=30 via mod.ini)");
            ModOptimizerVerifyLogger.Info("ini: mod_optimizer=1; full matrix: deploy -EnableCollisionLayers -FullProbe");
            ModOptimizerVerifyLogger.Info("parse: scripts\\RDYTU\\parse-modoptimizer-ringlog.ps1");
        }

        internal static void RunSpawnProbe(ref NOModContext ctx)
        {
            if (ModOptimizerVerifyState.SpawnedCount > 0)
                return;

            int count = ModOptimizerVerifyState.TargetSpawnCount;
            for (int i = 0; i < count; i++)
            {
                string name = "ModOptProxy_" + i.ToString("D2");
                var go = new GameObject(name);
                UnityEngine.Object.DontDestroyOnLoad(go);
                NOModRuntime.Scene.Register(name, go);
                NOModRuntime.Collision.RegisterProjectile(go, ModCollisionProfile.Projectile);
            }

            ModOptimizerVerifyState.SpawnedCount = count;
            ModOptimizerVerifyLogger.Pass("spawn", "proxies=" + count);
        }

        internal static void RunReflectionProbe(Assembly modAsm)
        {
            if (ModOptimizerVerifyState.ReflectionDelegateOk)
                return;

            const string typeName = "NOLoader.ModOptimizerVerify.ModOptimizerVerifyMod";
            if (NOModRuntime.Reflection.TryGetDelegate<Action>(modAsm, typeName, "ProbePing", out Action? del) && del != null)
            {
                del();
                ModOptimizerVerifyState.ReflectionDelegateOk = ModOptimizerVerifyState.ReflectionPingCount > 0;
                if (ModOptimizerVerifyState.ReflectionDelegateOk)
                    ModOptimizerVerifyLogger.Pass("reflection_cache", "ping=" + ModOptimizerVerifyState.ReflectionPingCount);
                else
                    ModOptimizerVerifyLogger.Warn("reflection_cache", "delegate ok but ping=0");
            }
            else
            {
                ModOptimizerVerifyLogger.Warn("reflection_cache", "TryGetDelegate failed — check reflectionBake + mod_reflection_cache=1");
            }
        }

        internal static void RunSceneLocatorProbe()
        {
            if (ModOptimizerVerifyState.SceneLocatorOk)
                return;

            if (NOModRuntime.Scene.TryGet("ModOptProxy_00", out object goObj) && goObj is GameObject go && go != null)
            {
                ModOptimizerVerifyState.SceneLocatorOk = true;
                ModOptimizerVerifyLogger.Pass("scene_locator", "hit=ModOptProxy_00");
            }
        }

        internal static void RunFindRedirectProbe()
        {
            if (ModOptimizerVerifyState.FindRedirectOk)
                return;

            GameObject found = GameObject.Find("ModOptProxy_00");
            if (found == null)
                return;

            if (NOModRuntime.Scene.TryGet("ModOptProxy_00", out object cachedObj)
                && cachedObj is GameObject cached
                && ReferenceEquals(found, cached))
            {
                ModOptimizerVerifyState.FindRedirectOk = true;
                ModOptimizerVerifyLogger.Pass("find_redirect", "cached=ModOptProxy_00");
            }
            else
            {
                ModOptimizerVerifyLogger.Warn("find_redirect", "Find returned object but not scene cache match");
            }
        }

        internal static void RunCollisionLayerProbe()
        {
            if (ModOptimizerVerifyState.CollisionLayerOk || ModOptimizerVerifyState.CollisionLayerWarned)
                return;

            if (!NOModRuntime.Scene.TryGet("ModOptProxy_00", out object goObj) || goObj is not GameObject go)
                return;

            int layer = go.layer;
            if (layer != 0)
            {
                ModOptimizerVerifyState.CollisionLayerOk = true;
                ModOptimizerVerifyLogger.Pass("collision_layers", "layer=" + layer);
                return;
            }

            ModOptimizerVerifyState.CollisionLayerWarned = true;
            ModOptimizerVerifyLogger.Warn("collision_layers", "layer=0 — redeploy with -EnableCollisionLayers");
        }

        internal static void RunAllProbes(Assembly modAsm)
        {
            RunReflectionProbe(modAsm);
            RunSceneLocatorProbe();
            RunFindRedirectProbe();
            RunCollisionLayerProbe();
        }

        internal static void TryLogOverallPass()
        {
            if (ModOptimizerVerifyState.PassLogged)
                return;

            int target = ModOptimizerVerifyState.TargetSpawnCount;
            if (!ModOptimizerVerifyState.ReflectionDelegateOk
                || !ModOptimizerVerifyState.SceneLocatorOk
                || !ModOptimizerVerifyState.FindRedirectOk
                || ModOptimizerVerifyState.SpawnedCount < target)
                return;

            ModOptimizerVerifyState.PassLogged = true;
            string collisionNote = ModOptimizerVerifyState.CollisionLayerOk ? "collision=1" : "collision=opt-in";
            ModOptimizerVerifyLogger.Pass("mod_optimizer", "spawn=" + target + " reflection scene find " + collisionNote);
        }

        internal static void LogPeriodic()
        {
            int elapsedSec = (Environment.TickCount - ModOptimizerVerifyState.SessionStartMs) / 1000;
            ModOptimizerVerifyLogger.Info("elapsed=" + elapsedSec + "s spawned=" + ModOptimizerVerifyState.SpawnedCount
                + " reflectionPing=" + ModOptimizerVerifyState.ReflectionPingCount
                + " find=" + ModOptimizerVerifyState.FindRedirectOk
                + " collisionLayer=" + ModOptimizerVerifyState.CollisionLayerOk);
        }

        internal static void LogSummary()
        {
            ModOptimizerVerifyLogger.Phase("Unload");
            ModOptimizerVerifyLogger.Info("spawned=" + ModOptimizerVerifyState.SpawnedCount
                + " reflection=" + ModOptimizerVerifyState.ReflectionDelegateOk
                + " scene=" + ModOptimizerVerifyState.SceneLocatorOk
                + " find=" + ModOptimizerVerifyState.FindRedirectOk
                + " collisionLayer=" + ModOptimizerVerifyState.CollisionLayerOk);
        }

        private static int ResolveSpawnCount(string modRoot)
        {
            try
            {
                string path = Path.Combine(modRoot, "mod.ini");
                if (!File.Exists(path))
                    return ModOptimizerVerifyState.DefaultSpawnCount;

                bool inVerifySection = false;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal))
                        continue;

                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        inVerifySection = string.Equals(line, "[verify]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!inVerifySection && !line.StartsWith("spawn_count=", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (line.StartsWith("spawn_count=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring("spawn_count=".Length).Trim();
                        if (int.TryParse(value, out int count) && count > 0 && count <= ModOptimizerVerifyState.FullSpawnCount)
                            return count;
                    }
                }
            }
            catch
            {
            }

            return ModOptimizerVerifyState.DefaultSpawnCount;
        }
    }
}
