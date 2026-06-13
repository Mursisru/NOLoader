using System.Collections.Generic;

namespace NOLoader.API.Manifest
{
    public sealed class ModWarmupSpec
    {
        public List<string> Materials = new List<string>();
        public List<string> Shaders = new List<string>();
        public List<string> Prefabs = new List<string>();
    }

    public sealed class ReflectionBakeEntry
    {
        public string Type = string.Empty;
        public string Method = string.Empty;
    }
}
