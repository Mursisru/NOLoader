#if NOLoader_DEV
using NOLoader.Core.Runtime;
using NOLoader.Telemetry;
using UnityEngine;

namespace NOLoader.Core
{
    internal static class TelemetryHost
    {
        private static GameObject? _host;

        public static void Install()
        {
            if (_host != null)
                return;

            UnityMainThread.Post(CreateHost);
        }

        private static void CreateHost()
        {
            if (_host != null)
                return;

            _host = new GameObject("NOLoader.TelemetryHost");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<TelemetryUpdateBehaviour>();
        }
    }

    internal sealed class TelemetryUpdateBehaviour : MonoBehaviour
    {
        private int _stride;

        private void Awake()
        {
            _stride = RuntimeConfig.TelemetryCaptureStride;
            if (_stride < 1)
                _stride = 1;
        }

        private void Update()
        {
            if ((Time.frameCount % _stride) != 0)
                return;

            TelemetryService.CaptureOnMainThread();
        }
    }
}
#endif
