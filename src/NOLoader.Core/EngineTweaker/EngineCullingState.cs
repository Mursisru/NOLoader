using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class EngineCullingState
    {
        private static long _skippedAnim;

        internal static long SkippedAnim => _skippedAnim;

        internal static void ResetStats()
        {
            _skippedAnim = 0;
        }

        /// <summary>Read-only LOD gate — never writes Unit.displayDetail (gameplay field).</summary>
        internal static bool ShouldSkipVisualUpdate(object unit)
        {
            if (!Runtime.RuntimeConfig.CullingOptimizerEnabled)
                return false;

            if (GameplayMechanicsGuard.IsProtectedSimUnit(unit))
                return false;

            float detail = EngineTweakerGameAccess.ReadDisplayDetail(unit);
            if (detail >= Runtime.RuntimeConfig.DisplayDetailMin)
                return false;

            _skippedAnim++;
            return true;
        }
    }
}
