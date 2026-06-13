using NOLoader.Core.Mods;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOLoader.Core.Interop
{
    /// <summary>Event-driven mission stage — zero Update() polling, no FindObjectsOfTypeAll.</summary>
    internal static class MissionStageObserver
    {
        private static bool _installed;

        public static void Install()
        {
            if (_installed)
                return;

            _installed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EngineTweaker.NOEngineTweakerBootstrap.OnSceneChanged();
            if (!ModLifecycleManager.IsMainMenuReady || ModLifecycleManager.IsMissionReady)
                return;

            if (IsMenuOrSystemScene(scene.path))
                return;

            ModLifecycleManager.NotifyMissionReady();
        }

        private static bool IsMenuOrSystemScene(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            return path.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("MultiplayerMenu", System.StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("MissionsMenu", System.StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("Encyclopedia", System.StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("MissionEditor", System.StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("empty", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
