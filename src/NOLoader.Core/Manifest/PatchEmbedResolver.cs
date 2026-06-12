using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NOLoader.API;
using NOLoader.API.Manifest;

namespace NOLoader.Core.Manifest
{
    /// <summary>One-time read of patch.bake.json — resolves hash-only patch entries for Cecil apply.</summary>
    public static class PatchEmbedResolver
    {
        private const string BakeFileName = "patch.bake.json";

        public static void Resolve(ModManifest manifest, string modFolder)
        {
            if (manifest.Patches.Count == 0)
                return;

            bool needsBake = false;
            foreach (PatchDescriptor patch in manifest.Patches)
            {
                if (string.IsNullOrEmpty(patch.Target) || string.IsNullOrEmpty(patch.Inject))
                    needsBake = true;
            }

            if (!needsBake)
                return;

            string bakePath = Path.Combine(modFolder, BakeFileName);
            if (!File.Exists(bakePath))
            {
                manifest.Valid = false;
                manifest.Error = "Hash-only patches require " + BakeFileName;
                return;
            }

            var baked = ParseBakeFile(File.ReadAllText(bakePath, Encoding.UTF8));
            foreach (PatchDescriptor patch in manifest.Patches)
            {
                if (!string.IsNullOrEmpty(patch.Target) && !string.IsNullOrEmpty(patch.Inject))
                    continue;

                if (!TryFindBake(baked, patch, out PatchDescriptor? source))
                {
                    manifest.Valid = false;
                    manifest.Error = "patch.bake.json missing entry for targetHash "
                        + patch.TargetHash.ToString("X8");
                    return;
                }

                patch.Target = source!.Target;
                patch.Inject = source.Inject;
                if (string.IsNullOrEmpty(patch.Method) || patch.Method == "Postfix")
                    patch.Method = source.Method;
                if (string.IsNullOrEmpty(patch.ExpectedSignatureHash))
                    patch.ExpectedSignatureHash = source.ExpectedSignatureHash;
            }
        }

        private static bool TryFindBake(List<PatchDescriptor> baked, PatchDescriptor patch, out PatchDescriptor? match)
        {
            foreach (PatchDescriptor entry in baked)
            {
                int targetHash = entry.TargetHash != 0
                    ? entry.TargetHash
                    : (string.IsNullOrEmpty(entry.Target) ? 0 : StringHash.Murmur32(entry.Target));
                int injectHash = entry.InjectHash != 0
                    ? entry.InjectHash
                    : (string.IsNullOrEmpty(entry.Inject) ? 0 : StringHash.Murmur32(entry.Inject));

                if (patch.TargetHash != 0 && targetHash == patch.TargetHash)
                {
                    match = entry;
                    return true;
                }

                if (patch.InjectHash != 0 && injectHash == patch.InjectHash)
                {
                    match = entry;
                    return true;
                }
            }

            match = null;
            return false;
        }

        private static List<PatchDescriptor> ParseBakeFile(string json)
        {
            var list = new List<PatchDescriptor>();
            int arrStart = json.IndexOf('[');
            if (arrStart < 0)
                return list;

            int depth = 0;
            int objStart = -1;
            for (int i = arrStart; i < json.Length; i++)
            {
                if (json[i] == '{')
                {
                    if (depth == 0)
                        objStart = i;
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
                            ExpectedSignatureHash = ReadString(obj, "expectedSignatureHash"),
                            ThrottleEveryN = ReadInt(obj, "throttleEveryN", 1),
                            TargetHash = ReadHexHash(obj, "targetHash"),
                            InjectHash = ReadHexHash(obj, "injectHash")
                        });
                        objStart = -1;
                    }
                }
                else if (json[i] == ']' && depth == 0)
                {
                    break;
                }
            }

            return list;
        }

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
            if (idx < 0)
                return defaultValue;
            int colon = json.IndexOf(':', idx);
            if (colon < 0)
                return defaultValue;
            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;
            if (end <= start)
                return defaultValue;
            return int.TryParse(json.Substring(start, end - start), out int value) ? value : defaultValue;
        }

        private static string ReadString(string json, string key, string defaultValue = "")
        {
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0)
                return defaultValue;
            int colon = json.IndexOf(':', idx);
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0)
                return defaultValue;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0)
                return defaultValue;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }
    }
}

