#if !NOLoader_DEV
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.GpuRender
{
    public static class GpuRenderHooks
    {
        public static void CombatHudLateUpdatePostfix(object __instance)
        {
            GpuHudCapture.CaptureFrame();
        }

        public static void RadarChaffLaunchChaffPostfix(object __instance)
        {
            if (__instance is not Component comp)
                return;

            FxInstancingRegistry.Register(comp.transform, 1);
        }

        public static bool HudUnitMarkerUpdatePositionPrefixSkip(object __instance)
        {
            return !GpuHudPassState.ShouldSkipCpuMarkerUpdate();
        }
    }

    internal static class GpuHudPassState
    {
        /// <summary>GPU instanced draw does not replicate marker sprites yet — never skip CPU path.</summary>
        internal static bool ShouldSkipCpuMarkerUpdate() => false;
    }
}
#endif
