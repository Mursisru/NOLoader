using System;
using System.IO;
using System.Text;

namespace NOLoader.Core.Patching
{
    internal static class AssemblyMarkerScan
    {
        public static bool Contains(string gameRoot, string moduleFile, string marker)
        {
            if (string.IsNullOrEmpty(marker))
                return false;

            string path = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", moduleFile);
            if (!File.Exists(path))
                return false;

            string text = Encoding.ASCII.GetString(File.ReadAllBytes(path));
            return text.IndexOf(marker, StringComparison.Ordinal) >= 0;
        }
    }
}
