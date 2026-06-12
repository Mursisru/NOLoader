using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NOLoader.API;
using NOLoader.API.Manifest;

namespace NOLoader.Core.Manifest
{
    public static class ManifestReader
    {
        public static List<ModManifest> ReadAll(string modsRoot)
        {
            var result = new List<ModManifest>();
            if (!Directory.Exists(modsRoot))
                return result;

            foreach (string dir in Directory.GetDirectories(modsRoot))
            {
                string jsonPath = Path.Combine(dir, "mod.json");
                if (!File.Exists(jsonPath))
                    continue;

                try
                {
                    var manifest = Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
                    manifest.FolderPath = dir;
                    PatchEmbedResolver.Resolve(manifest, dir);
                    result.Add(manifest);
                }
                catch (Exception ex)
                {
                    result.Add(new ModManifest
                    {
                        FolderPath = dir,
                        Valid = false,
                        Error = ex.Message
                    });
                }
            }

            return result;
        }

        public static ModManifest Parse(string json)
        {
            var m = new ModManifest();
            m.Id = ReadString(json, "id");
            m.Guid = ReadString(json, "guid");
            m.Version = ReadString(json, "version");
            m.Name = ReadString(json, "name");
            m.Assembly = ReadString(json, "assembly");
            m.EntryType = ReadString(json, "entryType");
            m.LoadStage = ParseStage(ReadString(json, "loadStage"));
            m.Dependencies = ReadStringArray(json, "dependencies");
            m.Patches = ReadPatches(json);

            int idHash = ReadInt(json, "idHash");
            if (idHash != 0)
            {
                m.IdHash = idHash;
                m.HashOnlyId = string.IsNullOrEmpty(m.Id);
            }
            else if (!string.IsNullOrEmpty(m.Id))
            {
                m.IdHash = StringHash.Murmur32(m.Id);
            }

#if !NOLoader_DEV
            if (m.HashOnlyId)
            {
                m.Id = m.IdHash.ToString("X8");
                m.Name = string.Empty;
            }
#else
            if (m.HashOnlyId)
            {
                m.Id = m.IdHash.ToString("X8");
                m.Name = string.Empty;
            }
            else if (StringHashTable.DevAutoRegisterEnabled)
            {
                RegisterManifestStrings(m);
            }
            else
            {
                RegisterDevDecodeKeys(json);
            }
#endif

            return m;
        }

#if NOLoader_DEV
        private static void RegisterManifestStrings(ModManifest m)
        {
            if (!string.IsNullOrEmpty(m.Id))
                StringHashTable.Register(m.Id);
            if (!string.IsNullOrEmpty(m.Name))
                StringHashTable.Register(m.Name);
            if (!string.IsNullOrEmpty(m.Assembly))
                StringHashTable.Register(m.Assembly);
            if (!string.IsNullOrEmpty(m.EntryType))
                StringHashTable.Register(m.EntryType);
            foreach (string dep in m.Dependencies)
                StringHashTable.Register(dep);
            foreach (PatchDescriptor patch in m.Patches)
            {
                if (!string.IsNullOrEmpty(patch.Target))
                    StringHashTable.Register(patch.Target);
                if (!string.IsNullOrEmpty(patch.Inject))
                    StringHashTable.Register(patch.Inject);
            }
        }

        private static void RegisterDevDecodeKeys(string json)
        {
            foreach (string key in ReadStringArray(json, "devDecodeKeys"))
                StringHashTable.Register(key);
        }
#endif

        private static LoadStage ParseStage(string stage)
        {
            if (string.Equals(stage, "MainMenu", StringComparison.OrdinalIgnoreCase))
                return LoadStage.MainMenu;
            if (string.Equals(stage, "Mission", StringComparison.OrdinalIgnoreCase))
                return LoadStage.Mission;
            return LoadStage.PreMenu;
        }

        private static List<string> ReadStringArray(string json, string key)
        {
            var list = new List<string>();
            int keyIdx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyIdx < 0) return list;
            int start = json.IndexOf('[', keyIdx);
            int end = json.IndexOf(']', start);
            if (start < 0 || end < 0) return list;

            string body = json.Substring(start + 1, end - start - 1);
            foreach (string part in body.Split(','))
            {
                string trimmed = part.Trim().Trim('"');
                if (!string.IsNullOrEmpty(trimmed))
                    list.Add(trimmed);
            }
            return list;
        }

        private static List<PatchDescriptor> ReadPatches(string json)
        {
            var list = new List<PatchDescriptor>();
            int patchesIdx = json.IndexOf("\"patches\"", StringComparison.Ordinal);
            if (patchesIdx < 0) return list;

            int arrStart = json.IndexOf('[', patchesIdx);
            if (arrStart < 0) return list;

            int depth = 0;
            int objStart = -1;
            for (int i = arrStart; i < json.Length; i++)
            {
                if (json[i] == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        string obj = json.Substring(objStart, i - objStart + 1);
                        list.Add(new PatchDescriptor
                        {
                            Target = ReadString(obj, "target"),
                            Inject = ReadString(obj, "inject"),
                            Method = ReadString(obj, "method", "Postfix"),
                            ExpectedSignatureHash = NullIfEmpty(ReadString(obj, "expectedSignatureHash")),
                            ThrottleEveryN = ReadInt(obj, "throttleEveryN", 1),
                            TargetHash = ReadHexHash(obj, "targetHash"),
                            InjectHash = ReadHexHash(obj, "injectHash")
                        });
                        objStart = -1;
                    }
                }
                else if (json[i] == ']' && depth == 0)
                    break;
            }

            return list;
        }

        private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        private static int ReadHexHash(string json, string key)
        {
            string hex = ReadString(json, key);
            if (string.IsNullOrEmpty(hex))
                return 0;
            hex = hex.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            if (hex.Length > 8)
                hex = hex.Substring(hex.Length - 8);
            return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int value)
                ? value
                : 0;
        }

        private static int ReadInt(string json, string key, int defaultValue = 0)
        {
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return 0;
            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            if (end <= start) return defaultValue;
            return int.TryParse(json.Substring(start, end - start), out int value) ? value : defaultValue;
        }

        private static string ReadString(string json, string key, string defaultValue = "")
        {
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;
            int colon = json.IndexOf(':', idx);
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return defaultValue;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return defaultValue;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }
    }
}
