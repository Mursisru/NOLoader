using NOLoader.ModConfig;
using UnityEngine;

namespace NOLoader.MissileHoldCam
{
    internal static class MissileHoldCamConfigCache
    {
        internal static bool Enabled;
        internal static KeyCode HoldKey;
        internal static float PostExplosionHoldSeconds;

        internal static void Load(ModIniConfig cfg)
        {
            Enabled = cfg.GetBool("General", "Enabled", true);
            HoldKey = cfg.GetKeyCode("General", "HoldKey", KeyCode.V);
            PostExplosionHoldSeconds = cfg.GetFloat("General", "PostExplosionHoldSeconds", 1f);
        }

        internal static void Refresh()
        {
        }
    }
}
