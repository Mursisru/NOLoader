using System.Collections.Generic;
using NOLoader.API;

namespace NOLoader.API.Manifest
{
    public sealed class ModManifest
    {
        public string Id = string.Empty;
        /// <summary>Mod GUID — unique across all mods (Unity-style asset id).</summary>
        public string Guid = string.Empty;
        public string Version = string.Empty;
        public string Name = string.Empty;
        public string Assembly = string.Empty;
        public string EntryType = string.Empty;
        public LoadStage LoadStage = LoadStage.PreMenu;
        public List<string> Dependencies = new List<string>();
        public List<PatchDescriptor> Patches = new List<PatchDescriptor>();
        public string FolderPath = string.Empty;
        public bool Valid = true;
        public string? Error;
        /// <summary>Murmur32 of mod id — primary runtime identifier in RDYTU.</summary>
        public int IdHash;
        /// <summary>True when manifest uses idHash without plain id (RDYTU hash-only).</summary>
        public bool HashOnlyId;
        /// <summary>Optional shader/material warmup entries (ModOptimizer).</summary>
        public ModWarmupSpec? Warmup;
        /// <summary>Optional reflection bake list (ModOptimizer).</summary>
        public List<ReflectionBakeEntry> ReflectionBake = new List<ReflectionBakeEntry>();
    }

    public sealed class PatchDescriptor
    {
        public string Target = string.Empty;
        public string Inject = string.Empty;
        public string Method = "Postfix";
        public string? ExpectedSignatureHash;
        /// <summary>RDYTU: run inject every N target invocations (1 = every call).</summary>
        public int ThrottleEveryN = 1;
        /// <summary>Murmur32 of target descriptor (RDYTU hash-only manifest).</summary>
        public int TargetHash;
        /// <summary>Murmur32 of inject descriptor (RDYTU hash-only manifest).</summary>
        public int InjectHash;
    }
}
