using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class EngineCullingState
    {
        private static long _skippedAnim;
        private static long _offscreenSkipped;
        private static long _audioSkipped;
        private static long _rendererCullCount;

        internal static long SkippedAnim => _skippedAnim;
        internal static long OffscreenSkipped => _offscreenSkipped;
        internal static long AudioSkipped => _audioSkipped;
        internal static long RendererCullCount => _rendererCullCount;

        internal static void ResetStats()
        {
            _skippedAnim = 0;
            _offscreenSkipped = 0;
            _audioSkipped = 0;
            _rendererCullCount = 0;
        }

        internal static void RecordAudioSkip()
        {
            _audioSkipped++;
        }

        /// <summary>Read-only visual gate — never writes Unit.displayDetail (gameplay field).</summary>
        internal static bool ShouldSkipVisualUpdate(object unit)
        {
            if (TrackIrStabilityGuard.ShouldProtectVanillaCameraFrame())
                return false;

            if (!RuntimeConfig.CullingGroundWheelsEnabled && !RuntimeConfig.CullingPilotAnimEnabled)
                return false;

            if (GameplayMechanicsGuard.IsProtectedSimUnit(unit))
                return false;

            float detail = EngineTweakerGameAccess.ReadDisplayDetail(unit);
            if (detail < RuntimeConfig.DisplayDetailMin)
                return false;

            if (RuntimeConfig.CullingOffscreenOnlyEnabled)
            {
                if (!EngineTweakerGameAccess.IsVisibleToMainCamera(unit))
                {
                    _offscreenSkipped++;
                    _skippedAnim++;
                    return true;
                }

                if (ShouldSkipByLodOrDistance(unit))
                {
                    _skippedAnim++;
                    return true;
                }

                return false;
            }

            if (ShouldSkipByLodOrDistance(unit))
            {
                _skippedAnim++;
                return true;
            }

            return false;
        }

        /// <summary>Disable ground vehicle renderers when compound CPU skip would apply.</summary>
        internal static bool ShouldCullGroundRenderer(object unit)
        {
            if (TrackIrStabilityGuard.ShouldProtectVanillaCameraFrame())
                return false;

            if (!RuntimeConfig.CullingGroundRendererEnabled || !RuntimeConfig.CullingGroundWheelsEnabled)
                return false;

            if (GameplayMechanicsGuard.IsProtectedSimUnit(unit))
                return false;

            float detail = EngineTweakerGameAccess.ReadDisplayDetail(unit);
            if (detail < RuntimeConfig.DisplayDetailMin)
                return false;

            if (!RuntimeConfig.CullingOffscreenOnlyEnabled)
                return ShouldSkipByLodOrDistance(unit);

            if (!EngineTweakerGameAccess.IsVisibleToMainCamera(unit))
            {
                _rendererCullCount++;
                return true;
            }

            if (ShouldSkipByLodOrDistance(unit))
            {
                _rendererCullCount++;
                return true;
            }

            return false;
        }

        /// <summary>Skip full GroundVehicle::Update path for off-screen or far on-screen client vehicles.</summary>
        internal static bool ShouldSkipOffScreenGroundAudio(object unit)
        {
            if (TrackIrStabilityGuard.ShouldProtectVanillaCameraFrame())
                return false;

            if (!RuntimeConfig.CullingGroundWheelsEnabled || !RuntimeConfig.CullingOffscreenOnlyEnabled)
                return false;

            if (GameplayMechanicsGuard.IsProtectedSimUnit(unit))
                return false;

            float detail = EngineTweakerGameAccess.ReadDisplayDetail(unit);
            if (detail < RuntimeConfig.DisplayDetailMin)
                return false;

            if (!EngineTweakerGameAccess.IsVisibleToMainCamera(unit))
                return true;

            return IsBeyondOnScreenMaxDistance(unit);
        }

        private static bool ShouldSkipByLodOrDistance(object unit)
        {
            if (IsBeyondOnScreenMaxDistance(unit))
                return true;

            if (IsBeyondCullDistance(unit))
                return true;

            float detail = EngineTweakerGameAccess.ReadDisplayDetail(unit);
            return detail < RuntimeConfig.DisplayDetailMin;
        }

        private static bool IsBeyondOnScreenMaxDistance(object unit)
        {
            float maxM = RuntimeConfig.CullingOnScreenMaxM;
            if (maxM <= 0f)
                return false;

            if (!EngineTweakerGameAccess.TryReadDistanceToMainCamera(unit, out float distance))
                return false;

            return distance > maxM;
        }

        private static bool IsBeyondCullDistance(object unit)
        {
            if (!EngineTweakerGameAccess.TryReadDistanceToMainCamera(unit, out float distance))
                return false;

            return distance > RuntimeConfig.CullDistanceM;
        }
    }
}
