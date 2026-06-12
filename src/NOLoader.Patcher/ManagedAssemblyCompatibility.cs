using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace NOLoader.Patcher
{
    /// <summary>Preflight checks before Cecil patch — stale Assembly-CSharp after game updates breaks Unity serialization.</summary>
    public static class ManagedAssemblyCompatibility
    {
        public static bool TryValidateForPatch(string gameRoot, out string error)
            => TryValidateCSharpMirage(gameRoot, null, out error);

        public static bool TryValidateCSharpMirage(string gameRoot, byte[]? csharpBytes, out string error)
        {
            error = string.Empty;
            string managed = Path.Combine(gameRoot, "NuclearOption_Data", "Managed");
            string miragePath = Path.Combine(managed, "Mirage.dll");
            if (!File.Exists(miragePath))
            {
                error = "Missing Mirage.dll — verify Nuclear Option game files in Steam.";
                return false;
            }

            Version? mirageDllVersion = ReadAssemblyFileVersion(miragePath);
            Version? csharpMirageRef = csharpBytes == null
                ? ReadAssemblyReferenceVersion(Path.Combine(managed, "Assembly-CSharp.dll"), "Mirage")
                : ReadAssemblyReferenceVersion(csharpBytes, "Mirage");

            if (mirageDllVersion == null || csharpMirageRef == null)
            {
                error = "Could not read Mirage versions from managed assemblies.";
                return false;
            }

            if (!VersionsMatch(mirageDllVersion, csharpMirageRef))
            {
                error =
                    "Assembly-CSharp.dll references Mirage " + csharpMirageRef +
                    " but game has Mirage " + mirageDllVersion +
                    ". Run scripts\\uninstall-noloader.ps1 (Steam verify), then redeploy.";
                return false;
            }

            if (csharpBytes == null)
            {
                string csharpPath = Path.Combine(managed, "Assembly-CSharp.dll");
                if (!File.Exists(csharpPath))
                {
                    error = "Missing Assembly-CSharp.dll — verify Nuclear Option game files in Steam.";
                    return false;
                }

                string vanillaPath = csharpPath + ManagedModuleGuard.VanillaBackupExtension;
                if (File.Exists(vanillaPath) && ManagedModuleGuard.ContainsNOLoaderMarkers(vanillaPath))
                {
                    error =
                        "Corrupt NOLoader backup (" + Path.GetFileName(vanillaPath) +
                        " contains IL patches). Run uninstall-noloader.ps1 or delete *.noloader.vanilla.bak and Steam-verify.";
                    return false;
                }
            }

            return true;
        }

        public static Version? ReadAssemblyFileVersion(string path)
        {
            try
            {
                return System.Reflection.AssemblyName.GetAssemblyName(path).Version;
            }
            catch
            {
                return null;
            }
        }

        public static Version? ReadAssemblyReferenceVersion(string modulePath, string referenceName)
        {
            try
            {
                return ReadAssemblyReferenceVersion(File.ReadAllBytes(modulePath), referenceName);
            }
            catch
            {
                return null;
            }
        }

        public static Version? ReadAssemblyReferenceVersion(byte[] moduleBytes, string referenceName)
        {
            try
            {
                using var stream = new MemoryStream(moduleBytes, writable: false);
                using var module = ModuleDefinition.ReadModule(stream);
                AssemblyNameReference? reference = module.AssemblyReferences
                    .FirstOrDefault(r => string.Equals(r.Name, referenceName, StringComparison.OrdinalIgnoreCase));
                return reference?.Version;
            }
            catch
            {
                return null;
            }
        }

        private static bool VersionsMatch(Version left, Version right)
            => left.Major == right.Major
               && left.Minor == right.Minor
               && left.Build == right.Build;
    }
}
