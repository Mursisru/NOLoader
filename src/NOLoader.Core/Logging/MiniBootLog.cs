using System;
using System.IO;
using System.Threading;
using NOLoader.Core.Mods;
using NOLoader.Core.Runtime;

namespace NOLoader.Core.Logging
{
    /// <summary>RDYTU.mini — single startup line to noloader_boot.log (independent of ring_log).</summary>
    internal static class MiniBootLog
    {
        private static int _written;

        internal static void TryWrite(string loaderRoot)
        {
            if (!RuntimeConfig.RdytuMiniEnabled)
                return;

            if (Interlocked.CompareExchange(ref _written, 1, 0) != 0)
                return;

            int total = 0;
            int loaded = 0;
            int failed = 0;
            foreach (LoadedMod mod in ModLifecycleManager.AllMods)
            {
                total++;
                if (mod.Loaded)
                    loaded++;
                if (mod.Failed)
                    failed++;
            }

            string line = "[NOLoader] " + AppVersion.Display
                + " mods=" + total
                + " loaded=" + loaded
                + " failed=" + failed
                + " optimizer=" + (RuntimeConfig.ModOptimizerEnabled ? "1" : "0")
                + " reflection=" + (RuntimeConfig.ModReflectionCacheEnabled ? "1" : "0")
                + " scene=" + (RuntimeConfig.ModSceneLocatorEnabled ? "1" : "0");

            string logDir = Path.Combine(loaderRoot, "logs");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, "noloader_boot.log"), line + Environment.NewLine);
        }
    }
}
