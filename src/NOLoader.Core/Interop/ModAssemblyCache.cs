using System;
using System.Collections.Generic;
using System.IO;

namespace NOLoader.Core.Interop
{
    /// <summary>Index mod/core DLL paths once at bootstrap — no directory scan per AssemblyResolve.</summary>
    public static class ModAssemblyCache
    {
        private static readonly Dictionary<string, string> BySimpleName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _built;

        public static int EntryCount => BySimpleName.Count;

        public static void Build(string loaderRoot, string gameRoot)
        {
            if (_built)
                return;

            _built = true;
            IndexFolder(Path.Combine(loaderRoot, "core"));
            IndexFolder(Path.Combine(loaderRoot, "mods"));
            IndexFolder(Path.Combine(gameRoot, "NuclearOption_Data", "Managed"));
        }

        public static bool TryGetPath(string assemblySimpleName, out string path)
        {
            return BySimpleName.TryGetValue(assemblySimpleName, out path!);
        }

        private static void IndexFolder(string folder)
        {
            if (!Directory.Exists(folder))
                return;

            foreach (string file in Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!BySimpleName.ContainsKey(name))
                    BySimpleName[name] = file;
            }
        }
    }
}
