using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;

namespace NOLoader.Core.EngineTweaker
{
    /// <summary>Prevents EngineTweaker from breaking core sim (thrust, vehicles) via displayDetail or culling.</summary>
    internal static class GameplayMechanicsGuard
    {
        private const float LocalSimDisplayDetail = 1000f;
        private static int _correctedDetail;

        internal static bool IsProtectedSimUnit(object unit)
        {
            if (EngineTweakerGameAccess.IsLocalPlayerUnit(unit))
                return true;

            if (EngineTweakerGameAccess.IsFollowingUnit(unit))
                return true;

            return false;
        }

        /// <summary>Ensure local aircraft never keeps displayDetail &lt; 1 (blocks audio/FX paths in game code).</summary>
        internal static void PinLocalPlayerSimState()
        {
            if (!RuntimeConfig.EngineTweakerEnabled)
                return;

            if (!EngineTweakerGameAccess.TryGetLocalPlayerUnit(out object? unit) || unit == null)
                return;

            float detail = EngineTweakerGameAccess.ReadDisplayDetail(unit);
            if (detail >= 1f)
                return;

            EngineTweakerGameAccess.WriteDisplayDetail(unit, LocalSimDisplayDetail);
            _correctedDetail++;
            if (_correctedDetail <= 3 || _correctedDetail % 30 == 0)
            {
                RingBufferLog.WriteAscii("[EngineTweaker] pinned displayDetail for local aircraft (was "
                    + detail.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
            }
        }
    }
}
