using NOLoader.ModConfig;

namespace NOLoader.RepeatTakeoffMusic
{
    internal static class RepeatTakeoffMusicConfigCache
    {
        internal static bool Enabled;

        internal static void Load(ModIniConfig cfg)
        {
            Enabled = cfg.GetBool("General", "Enabled", true);
        }

        internal static void Refresh()
        {
        }
    }
}
