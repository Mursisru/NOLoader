using System;
using System.Collections;
using System.Collections.Generic;
using NOLoader.Core.Logging;
using NOLoader.Core.Mods;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOLoader.Core.Interop
{
    /// <summary>Event-driven mission stage — zero Update() polling, no FindObjectsOfTypeAll.</summary>
    internal static class MissionStageObserver
    {
        private static bool _installed;
        private static bool _missionReadyScheduled;

        public static void Install()
        {
            if (_installed)
                return;

            _installed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Runtime.RuntimeConfig.RdytuMiniEnabled)
                EngineTweaker.NOEngineTweakerBootstrap.OnSceneChanged();
            if (!ModLifecycleManager.IsMainMenuReady || ModLifecycleManager.IsMissionReady)
                return;

            if (IsMenuOrSystemScene(scene.path))
                return;

            if (_missionReadyScheduled)
                return;

            _missionReadyScheduled = true;
            MissionReadyDeferHost.Schedule(() =>
            {
                _missionReadyScheduled = false;
                if (ModLifecycleManager.IsMissionReady)
                    return;

                ModLifecycleManager.NotifyMissionReady();
            });
        }

        private static bool IsMenuOrSystemScene(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            return path.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("MultiplayerMenu", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("MissionsMenu", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("Encyclopedia", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("MissionEditor", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("empty", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>Defer mission mod load until next frame (scene init must finish first).</summary>
    internal static class MissionReadyDeferHost
    {
        private static MissionReadyDeferBehaviour? _behaviour;

        internal static void Schedule(Action action)
        {
            EnsureHost();
            _behaviour!.Enqueue(action);
        }

        private static void EnsureHost()
        {
            if (_behaviour != null)
                return;

            var host = new GameObject("NOLoader.MissionReadyDefer");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _behaviour = host.AddComponent<MissionReadyDeferBehaviour>();
        }

        private sealed class MissionReadyDeferBehaviour : MonoBehaviour
        {
            private readonly Queue<Action> _pending = new Queue<Action>();
            private bool _flushScheduled;

            internal void Enqueue(Action action)
            {
                _pending.Enqueue(action);
                if (!_flushScheduled)
                {
                    _flushScheduled = true;
                    StartCoroutine(FlushNextFrame());
                }
            }

            private IEnumerator FlushNextFrame()
            {
                yield return null;
                _flushScheduled = false;
                while (_pending.Count > 0)
                {
                    Action action = _pending.Dequeue();
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        RingBufferLog.WriteAscii("[NOLoader][WARN] mission ready defer: " + ex.Message);
                    }
                }
            }
        }
    }
}
