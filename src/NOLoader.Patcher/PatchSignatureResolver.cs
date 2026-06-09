using System.Collections.Generic;
using NOLoader.API.Manifest;

namespace NOLoader.Patcher
{
    /// <summary>Fills expectedSignatureHash for trusted core bootstrap patches at apply time.</summary>
    public static class PatchSignatureResolver
    {
        public static void PopulateMissingCoreHashes(byte[] assemblyBytes, string gameRoot, IList<PatchEntry> plan)
        {
            foreach (PatchEntry entry in plan)
            {
                if (!string.IsNullOrEmpty(entry.Descriptor.ExpectedSignatureHash))
                    continue;

                if (AssemblyPatcher.TryComputePatchSignatureHash(
                        assemblyBytes,
                        gameRoot,
                        entry,
                        out string hash,
                        out _))
                {
                    entry.Descriptor.ExpectedSignatureHash = hash;
                }
            }
        }
    }
}
