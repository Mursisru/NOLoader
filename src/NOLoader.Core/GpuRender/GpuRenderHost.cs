#if !NOLoader_DEV
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.GpuRender
{
    internal static class GpuRenderHost
    {
        private static GameObject? _host;

        internal static void EnsureInstalled()
        {
            if (_host != null)
                return;

            UnityMainThread.Post(CreateHost);
        }

        private static void CreateHost()
        {
            if (_host != null)
                return;

            _host = new GameObject("NOLoader.GpuRenderHost");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<GpuRenderHostBehaviour>();
        }
    }

    internal sealed class GpuRenderHostBehaviour : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (!RuntimeConfig.GpuRenderEnabled)
                return;

            if (RuntimeConfig.GpuFxInstancingEnabled)
                FxInstancingRegistry.TickAndDraw();

            GpuRenderMetrics.TryLogPeriodic();
        }
    }
}
#endif
