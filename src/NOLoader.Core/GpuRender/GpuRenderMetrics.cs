#if !NOLoader_DEV
using System.Globalization;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using NOLoader.Core.Runtime.Balance;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOLoader.Core.GpuRender
{
    internal static class GpuRenderMetrics
    {
        private static float _nextLogTime;
        private static int _logCount;

        internal static void TryLogPeriodic()
        {
            if (!RuntimeConfig.GpuMetricsEnabled)
                return;

            float now = Time.unscaledTime;
            if (_logCount > 0 && now < _nextLogTime)
                return;

            _nextLogTime = now + 30f;
            _logCount++;

            string threading = SystemInfo.renderingThreadingMode.ToString();
            RingBufferLog.WriteAscii("[GpuRender] gpu device=" + SystemInfo.graphicsDeviceType
                + " shaderLevel=" + SystemInfo.graphicsShaderLevel
                + " threadingMode=" + threading
                + " gpu=" + SystemInfo.graphicsDeviceName);

            MainThreadMetrics.TryLogPeriodic();
        }

        internal static string FormatSnapshot()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "threading={0} shader={1} device={2}",
                SystemInfo.renderingThreadingMode,
                SystemInfo.graphicsShaderLevel,
                SystemInfo.graphicsDeviceType);
        }
    }
}
#endif
