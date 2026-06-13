#if !NOLoader_DEV
using System;
using System.Collections.Generic;
using System.IO;
using NOLoader.Core.Logging;

namespace NOLoader.Core.GpuRender
{
    internal static class BootConfigHelper
    {
        private static readonly string[] GfxJobLines =
        {
            "gfx-enable-gfx-jobs=1",
            "gfx-enable-native-gfx-jobs=1"
        };

        internal static bool Apply(string gameRoot, bool enableGfxJobs)
        {
            if (!enableGfxJobs || string.IsNullOrEmpty(gameRoot))
                return false;

            string dataDir = Path.Combine(gameRoot, "NuclearOption_Data");
            string bootPath = Path.Combine(dataDir, "boot.config");
            if (!Directory.Exists(dataDir))
                return false;

            string backupPath = bootPath + ".noloader.bak";
            if (File.Exists(bootPath) && !File.Exists(backupPath))
                File.Copy(bootPath, backupPath, overwrite: false);

            var lines = new List<string>();
            if (File.Exists(bootPath))
            {
                foreach (string raw in File.ReadAllLines(bootPath))
                    lines.Add(raw);
            }

            bool changed = false;
            for (int i = 0; i < GfxJobLines.Length; i++)
            {
                string key = GfxJobLines[i].Split('=')[0];
                if (ContainsKey(lines, key))
                    continue;

                lines.Add(GfxJobLines[i]);
                changed = true;
            }

            if (!changed)
                return false;

            File.WriteAllLines(bootPath, lines);
            RingBufferLog.WriteAscii("[GpuRender] boot.config updated gfx-jobs=1 native=1");
            return true;
        }

        private static bool ContainsKey(List<string> lines, string key)
        {
            string prefix = key + "=";
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
#endif
