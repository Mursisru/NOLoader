using System.Collections.Generic;
using System.Linq;
using NOLoader.API.Manifest;
using NOLoader.Core.Gates;

namespace NOLoader.Core.Manifest
{
    public static class ModManifestPipeline
    {
        public static List<ModManifest> ReadValidated(string modsRoot, out List<string> errors, string? gameRoot = null)
        {
            var manifests = ManifestReader.ReadAll(modsRoot);
            manifests = ManifestGateL1.Validate(manifests, out List<string> l1Errors);
            manifests = PatchGateL2.FilterValid(manifests, out List<string> l2Errors, gameRoot);
            errors = l1Errors.Concat(l2Errors).ToList();
            return manifests;
        }
    }
}
