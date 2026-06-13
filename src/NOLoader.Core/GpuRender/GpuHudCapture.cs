#if !NOLoader_DEV
using System.Collections.Generic;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.GpuRender
{
    internal static class GpuHudCapture
    {
        private static readonly List<Vector3> Positions = new List<Vector3>(256);
        private static readonly List<Color> Colors = new List<Color>(256);
        private static int _lastCount;

        internal static int LastMarkerCount => _lastCount;

        internal static void CaptureFrame()
        {
            if (!RuntimeConfig.GpuHudPassEnabled)
                return;

            GpuRenderGameAccess.EnsureInitialized();
            _lastCount = GpuRenderGameAccess.CaptureHudMarkers(Positions, Colors);
            GpuHudPass.UploadMarkers(Positions, Colors);
        }

        internal static IReadOnlyList<Vector3> PositionsSnapshot => Positions;
    }
}
#endif
