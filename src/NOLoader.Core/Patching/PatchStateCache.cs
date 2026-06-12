using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NOLoader.Patcher;

namespace NOLoader.Core.Patching
{
    /// <summary>Deploy-time patch markers — avoids full-DLL scans at bootstrap when PatchTool pre-applied.</summary>
    public static class PatchStateCache
    {
        private const string StateFileName = "patch_state.txt";
        private static Dictionary<string, string>? _entries;

        public static void Record(string loaderRoot, string moduleFile, IEnumerable<string> markers)
        {
            string path = Path.Combine(loaderRoot, StateFileName);
            var map = LoadMap(path);
            map[moduleFile] = string.Join(",", markers);
            WriteMap(path, map);
            _entries = null;
        }

        public static void Append(string loaderRoot, string moduleFile, IEnumerable<string> markers)
        {
            string path = Path.Combine(loaderRoot, StateFileName);
            var map = LoadMap(path);
            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (map.TryGetValue(moduleFile, out string? existing))
            {
                foreach (string part in existing.Split(','))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length > 0)
                        merged.Add(trimmed);
                }
            }

            foreach (string marker in markers)
            {
                if (!string.IsNullOrEmpty(marker))
                    merged.Add(marker);
            }

            map[moduleFile] = string.Join(",", merged);
            WriteMap(path, map);
            _entries = null;
        }

        public static bool TryIsPatched(string gameRoot, string moduleFile, string marker)
        {
            // Never trust patch_state.txt alone — it desyncs after Steam verify or partial restores.
            return AssemblyMarkerScan.Contains(gameRoot, moduleFile, marker);
        }

        private static void EnsureLoaded(string loaderRoot)
        {
            if (_entries != null)
                return;

            string path = Path.Combine(loaderRoot, StateFileName);
            _entries = LoadMap(path);
        }

        private static Dictionary<string, string> LoadMap(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return map;

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            return map;
        }

        private static void WriteMap(string path, Dictionary<string, string> map)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var sb = new StringBuilder();
            sb.AppendLine("# NOLoader deploy patch markers — written by PatchTool");
            foreach (KeyValuePair<string, string> kv in map)
                sb.AppendLine(kv.Key + "=" + kv.Value);
            File.WriteAllText(path, sb.ToString());
        }

        public static string ExtractMarker(string injectMethod)
        {
            if (string.IsNullOrEmpty(injectMethod))
                return string.Empty;

            int dot = injectMethod.LastIndexOf('.');
            return dot >= 0 ? injectMethod.Substring(dot + 1) : injectMethod;
        }
    }
}
