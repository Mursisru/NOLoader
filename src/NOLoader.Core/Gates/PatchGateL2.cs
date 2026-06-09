using System.Collections.Generic;
using System.Linq;
using NOLoader.API.Manifest;
using NOLoader.Patcher;

namespace NOLoader.Core.Gates
{
    /// <summary>Gate L2 — IL patch signature enforcement before Cecil apply.</summary>
    public static class PatchGateL2
    {
        public static List<string> ValidateModPatches(IReadOnlyList<ModManifest> manifests)
        {
            var errors = new List<string>();
            foreach (ModManifest manifest in manifests)
            {
                if (!manifest.Valid || manifest.Patches.Count == 0)
                    continue;

                string modLabel = ModLabel(manifest);

                foreach (PatchDescriptor patch in manifest.Patches)
                {
                    if (string.IsNullOrEmpty(patch.ExpectedSignatureHash))
                    {
                        errors.Add(
                            "Mod " + modLabel + ": patch " + patch.Target + " missing expectedSignatureHash");
                    }
                }
            }

            return errors;
        }

        /// <summary>Exclude mods with invalid patch metadata or signature hash mismatch (Gate L2 hard fail).</summary>
        public static List<ModManifest> FilterValid(
            IReadOnlyList<ModManifest> manifests,
            out List<string> errors,
            string? gameRoot = null)
        {
            errors = new List<string>();
            var valid = new List<ModManifest>();
            byte[]? gameAssembly = null;

            if (!string.IsNullOrEmpty(gameRoot))
            {
                string root = gameRoot!;
                gameAssembly = AssemblyPatcher.LoadLiveGameAssemblyBytes(root)
                    ?? AssemblyPatcher.LoadGameAssemblyBytes(root);
            }

            foreach (ModManifest manifest in manifests)
            {
                if (manifest.Patches.Count == 0)
                {
                    valid.Add(manifest);
                    continue;
                }

                bool rejected = false;
                string modLabel = ModLabel(manifest);

                foreach (PatchDescriptor patch in manifest.Patches)
                {
                    if (string.IsNullOrEmpty(patch.ExpectedSignatureHash))
                    {
                        errors.Add(
                            "Mod " + modLabel + " rejected: patch " + patch.Target + " missing expectedSignatureHash");
                        rejected = true;
                        break;
                    }

                    if (gameAssembly != null && !string.IsNullOrEmpty(gameRoot))
                    {
                        string root = gameRoot!;
                        if (!AssemblyPatcher.TryComputeTargetSignatureHash(
                                gameAssembly,
                                root,
                                patch,
                                out string actual,
                                out string computeError))
                        {
                            errors.Add("Mod " + modLabel + " rejected: " + computeError);
                            rejected = true;
                            break;
                        }

                        if (!string.Equals(actual, patch.ExpectedSignatureHash, System.StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(
                                "Mod " + modLabel + " rejected: Signature mismatch for " + patch.Target
                                + " expected=" + patch.ExpectedSignatureHash + " actual=" + actual);
                            rejected = true;
                            break;
                        }
                    }
                }

                if (!rejected)
                    valid.Add(manifest);
            }

            return valid;
        }

        private static string ModLabel(ModManifest manifest)
            => !string.IsNullOrEmpty(manifest.Id) ? manifest.Id : manifest.IdHash.ToString("X8");
    }
}
