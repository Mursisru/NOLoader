using System.Collections.Generic;
using System.Linq;
using NOLoader.API;
using NOLoader.API.Manifest;

namespace NOLoader.Core.Gates
{
    /// <summary>Gate L1 — manifest validation before menu.</summary>
    public static class ManifestGateL1
    {
        public static List<ModManifest> Validate(IReadOnlyList<ModManifest> manifests, out List<string> errors)
        {
            errors = new List<string>();
            var valid = new List<ModManifest>();
            var ids = new HashSet<string>();
            var guids = new HashSet<string>();

            foreach (ModManifest m in manifests)
            {
                if (!m.Valid)
                {
                    errors.Add($"Skip invalid manifest in {m.FolderPath}: {m.Error}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(m.Id) && m.IdHash == 0)
                {
                    errors.Add($"Missing id/idHash in {m.FolderPath}");
                    continue;
                }

                if (m.IdHash == 0 && !string.IsNullOrWhiteSpace(m.Id))
                    m.IdHash = StringHash.Murmur32(m.Id);

                if (string.IsNullOrWhiteSpace(m.Assembly) || string.IsNullOrWhiteSpace(m.EntryType))
                {
                    errors.Add($"Missing assembly/entryType for {m.IdHash:X8}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(m.Guid))
                {
                    if (!IsValidGuidFormat(m.Guid))
                    {
                        errors.Add($"Invalid guid format for {m.Id}: {m.Guid}");
                        continue;
                    }

                    string guidKey = NormalizeGuid(m.Guid);
                    if (!guids.Add(guidKey))
                    {
                        errors.Add($"Duplicate mod guid: {m.Guid}");
                        continue;
                    }
                }

                string dedupeKey = !string.IsNullOrWhiteSpace(m.Id) ? m.Id : m.IdHash.ToString("X8");
                if (!ids.Add(dedupeKey))
                {
                    errors.Add($"Duplicate mod id: {dedupeKey}");
                    continue;
                }

                if (HasCycle(manifests, dedupeKey))
                {
                    errors.Add($"Dependency cycle involving {dedupeKey}");
                    continue;
                }

                valid.Add(m);
            }

            return valid;
        }

        private static bool IsValidGuidFormat(string guid)
        {
            string normalized = NormalizeGuid(guid);
            if (normalized.Length != 32)
                return false;

            foreach (char c in normalized)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            }

            return true;
        }

        private static string NormalizeGuid(string guid)
            => guid.Replace("-", string.Empty).Replace("{", string.Empty).Replace("}", string.Empty).Trim().ToLowerInvariant();

        private static bool HasCycle(IReadOnlyList<ModManifest> manifests, string startKey)
        {
            var byKey = new Dictionary<string, ModManifest>();
            foreach (ModManifest m in manifests.Where(x => x.Valid))
            {
                string key = !string.IsNullOrWhiteSpace(m.Id) ? m.Id : m.IdHash.ToString("X8");
                byKey[key] = m;
            }

            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();

            bool Dfs(string key)
            {
                if (visited.Contains(key)) return false;
                if (!visiting.Add(key)) return true;
                if (!byKey.TryGetValue(key, out ModManifest? m)) { visiting.Remove(key); return false; }
                foreach (string dep in m.Dependencies)
                {
                    if (Dfs(dep)) return true;
                }
                visiting.Remove(key);
                visited.Add(key);
                return false;
            }

            return Dfs(startKey);
        }
    }
}
