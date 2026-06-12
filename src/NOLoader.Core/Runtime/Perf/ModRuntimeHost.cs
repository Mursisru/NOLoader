using UnityEngine;

namespace NOLoader.Core.Runtime.Perf
{
    internal static class ModRuntimeHost
    {
        private static GameObject? _host;

        public static void EnsureInstalled()
        {
            if (_host != null)
                return;

            UnityMainThread.Post(CreateHost);
        }

        private static void CreateHost()
        {
            if (_host != null)
                return;

            _host = new GameObject("NOLoader.ModRuntimeHost");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<ModRuntimeHostBehaviour>();
        }
    }

    internal sealed class ModRuntimeHostBehaviour : MonoBehaviour
    {
        private void Update()
        {
            if (!ModTickScheduler.HasTickMods && !WorldSnapshotService.Instance.IsActive)
                return;

            WorldSnapshotService.Instance.Tick();
            ModTickScheduler.TickNormal();
            ModTickScheduler.TickSlow();
        }

        private void LateUpdate()
        {
            if (!ModTickScheduler.HasTickMods)
                return;

            ModTickScheduler.TickFast(Time.deltaTime);
            ModExecutionBudget.Instance.EndFrame();
        }
    }
}
