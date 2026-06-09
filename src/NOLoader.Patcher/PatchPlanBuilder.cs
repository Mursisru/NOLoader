using System;
using System.Collections.Generic;
using System.Linq;
using NOLoader.API;
using NOLoader.API.Manifest;

namespace NOLoader.Patcher
{
    public static class PatchPlanBuilder
    {
        public static List<PatchEntry> Build(
            IReadOnlyList<ModManifest> manifests,
            LoadStage? maxStageInclusive = null,
            LoadStage? minStageInclusive = null)
        {
            var entries = new List<PatchEntry>();
            foreach (ModManifest manifest in manifests.Where(m => m.Valid))
            {
                if (maxStageInclusive.HasValue && manifest.LoadStage > maxStageInclusive.Value)
                    continue;
                if (minStageInclusive.HasValue && manifest.LoadStage < minStageInclusive.Value)
                    continue;
                foreach (PatchDescriptor patch in manifest.Patches)
                {
                    if (string.IsNullOrEmpty(patch.ExpectedSignatureHash))
                        throw new InvalidOperationException(
                            "PatchPlanBuilder: missing expectedSignatureHash for " + manifest.Id + " -> " + patch.Target);

                    entries.Add(new PatchEntry
                    {
                        ModId = manifest.Id,
                        ModFolder = manifest.FolderPath,
                        Descriptor = patch
                    });
                }
            }
            return entries;
        }
    }

    public sealed class PatchEntry
    {
        public string ModId = string.Empty;
        public string ModFolder = string.Empty;
        public string? InjectAssembly;
        public PatchDescriptor Descriptor = null!;
    }
}
