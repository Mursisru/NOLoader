using System;
using System.IO;
using NOLoader.ModConfig;

namespace NOLoader.NVGConfig
{
    internal static class NVGConfigCache
    {
        private const string ModFolderName = "NVGConfig";

        private static string _modRoot = string.Empty;
        private static DateTime _lastWriteUtc = DateTime.MinValue;

        internal static NVGMode SelectedMode = NVGMode.GreenPhosphor;
        internal static float CustomSaturation;
        internal static float CustomContrast;
        internal static float CustomColorR = 1f;
        internal static float CustomColorG = 1f;
        internal static float CustomColorB = 1f;

        internal static void Init(string modRoot)
        {
            _modRoot = modRoot;
            _lastWriteUtc = DateTime.MinValue;
            Refresh(force: true);
        }

        internal static void EnsureInitialized()
        {
            if (!string.IsNullOrEmpty(_modRoot))
                return;

            string fromAssembly = ResolveModRootFromAssembly();
            if (fromAssembly.Length > 0)
            {
                Init(fromAssembly);
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string candidate = Path.Combine(baseDir, "NOLoader", "mods", ModFolderName);
            if (File.Exists(Path.Combine(candidate, "mod_config.ini")))
                Init(candidate);
        }

        internal static void Refresh(bool force = false)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(_modRoot))
                return;

            string path = Path.Combine(_modRoot, "mod_config.ini");
            if (!File.Exists(path))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (!force && writeUtc <= _lastWriteUtc)
                return;

            _lastWriteUtc = writeUtc;
            Load(ModIniConfig.Load(_modRoot));
        }

        internal static void Load(ModIniConfig cfg)
        {
            string modeRaw = cfg.GetString("Settings", "FilterMode", nameof(NVGMode.GreenPhosphor));
            int hash = modeRaw.IndexOf('#');
            if (hash >= 0)
                modeRaw = modeRaw.Substring(0, hash).Trim();

            NVGMode previous = SelectedMode;
            if (!Enum.TryParse(modeRaw, true, out NVGMode mode))
                mode = NVGMode.GreenPhosphor;

            SelectedMode = mode;
            if (mode != previous)
                NightVisionColorLogic.NotifyConfigChanged();

            CustomSaturation = cfg.GetFloat("Custom Filter", "Saturation", 0f);
            CustomContrast = cfg.GetFloat("Custom Filter", "Contrast", 0f);
            CustomColorR = cfg.GetFloat("Custom Filter", "ColourFilterR", 1f);
            CustomColorG = cfg.GetFloat("Custom Filter", "ColourFilterG", 1f);
            CustomColorB = cfg.GetFloat("Custom Filter", "ColourFilterB", 1f);
        }

        private static string ResolveModRootFromAssembly()
        {
            string? location = typeof(NVGConfigCache).Assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                string? dir = Path.GetDirectoryName(location);
                return dir ?? string.Empty;
            }

            string? codeBase = typeof(NVGConfigCache).Assembly.CodeBase;
            if (string.IsNullOrEmpty(codeBase))
                return string.Empty;

            try
            {
                var uri = new Uri(codeBase);
                return Path.GetDirectoryName(uri.LocalPath) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
