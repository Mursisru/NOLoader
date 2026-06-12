using System.Collections.Generic;
using System;
using System.Reflection;
using NOLoader.API.Manifest;
using NOLoader.Core.Interop;
using NOLoader.Core.Logging;

namespace NOLoader.Core.Patching
{
    /// <summary>Preload mod patch assemblies before Assembly-CSharp JIT binds inject calls.</summary>
    internal static class ModIlAssemblyLoadHook
    {
        private static bool _registered;
        private static IReadOnlyList<ModManifest>? _manifests;
        private static string _loaderRoot = string.Empty;

        public static void Register(IReadOnlyList<ModManifest> manifests, string loaderRoot)
        {
            if (_registered)
            {
                _manifests = manifests;
                _loaderRoot = loaderRoot;
                return;
            }

            _registered = true;
            _manifests = manifests;
            _loaderRoot = loaderRoot;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        public static void PreloadPatchAssemblies(IReadOnlyList<ModManifest> manifests, string loaderRoot)
        {
            _manifests = manifests;
            _loaderRoot = loaderRoot;
            ModPatchAssemblyPreloader.EnsureLoaded(manifests, loaderRoot);
        }

        private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            if (!string.Equals(args.LoadedAssembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
                return;

            if (_manifests == null || string.IsNullOrEmpty(_loaderRoot))
                return;

            ModPatchAssemblyPreloader.EnsureLoaded(_manifests, _loaderRoot);
            RingBufferLog.WriteAscii("[NOLoader] Mod IL preload on Assembly-CSharp load");
        }
    }
}
