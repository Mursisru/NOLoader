using NOLoader.ModConfig;

namespace NOLoader.AutoFlare
{
    internal static class AutoFlareConfigCache
    {
        internal static bool Enabled;
        internal static byte LtcIndex;
        internal static string IrSeekerKeyword = "IR";
        internal static float HitRadius;

        internal static void Load(ModIniConfig cfg)
        {
            Enabled = cfg.GetBool("General", "Enabled", true);
            LtcIndex = cfg.GetByte("General", "LtcIndex", 0);
            IrSeekerKeyword = cfg.GetString("General", "IrSeekerKeyword", "IR");
            HitRadius = cfg.GetFloat("Physics", "HitRadius", 260f);
        }
    }
}
