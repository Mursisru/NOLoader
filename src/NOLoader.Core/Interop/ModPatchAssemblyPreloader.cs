using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NOLoader.API.Manifest;
using NOLoader.Core.Logging;

namespace NOLoader.Core.Interop
{
    /// <summary>Load mod/core DLLs before Encyclopedia preload — IL inject runs during MainMenu.StartAsync.</summary>
    public static class ModPatchAssemblyPreloader
    {
        private static readonly HashSet<string> Loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Reset() => Loaded.Clear();

        public static void EnsureLoaded(IReadOnlyList<ModManifest> manifests, string loaderRoot)
        {
            PreloadCore(loaderRoot, "NOLoader.Core.dll");
            PreloadCore(loaderRoot, "NOLoader.Registry.dll");
            PreloadCore(loaderRoot, "NOLoader.API.dll");
            PreloadCore(loaderRoot, "NOLoader.Patcher.dll");

            foreach (ModManifest manifest in manifests)
            {
                if (!manifest.Valid || manifest.Patches.Count == 0)
                    continue;
                if (!Directory.Exists(manifest.FolderPath))
                    continue;

                foreach (string dllPath in Directory.GetFiles(manifest.FolderPath, "*.dll"))
                    Preload(Path.GetFileNameWithoutExtension(dllPath), dllPath);
            }
        }

        private static void PreloadCore(string loaderRoot, string fileName)
        {
            string path = Path.Combine(loaderRoot, "core", fileName);
            if (File.Exists(path))
                Preload(Path.GetFileNameWithoutExtension(fileName), path);
        }

        private static void Preload(string simpleName, string path)
        {
            if (IsLoadedInDomain(simpleName))
                return;

            Loaded.Add(simpleName);
            try
            {
                Assembly.LoadFrom(path);
                RingBufferLog.WriteAscii("[NOLoader] Preloaded patch assembly: " + simpleName);
            }
            catch (Exception ex)
            {
                Loaded.Remove(simpleName);
                RingBufferLog.WriteAscii("[NOLoader] WARN patch assembly preload failed: " + simpleName + " — " + ex.Message);
            }
        }

        private static bool IsLoadedInDomain(string simpleName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
