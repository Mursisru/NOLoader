using System;
using System.IO;
using System.Text;

namespace NOLoader.Patcher
{
    /// <summary>Detect NOLoader IL in managed modules and keep an immutable vanilla snapshot for uninstall.</summary>
    public static class ManagedModuleGuard
    {
        public const string VanillaBackupExtension = ".noloader.vanilla.bak";
        public const string LegacyBackupExtension = ".noloader.bak";

        private static readonly string[] Markers =
        {
            "NOLoader.Core",
            "NOLoader.Registry",
            "OnMainMenuReady",
            "MissionGateHooks",
            "NOLoader.RealWeaponNames",
            "NOLoader.AutoFlare",
            "NOLoader.HudCommon",
            "NOLoader.RepeatTakeoffMusic",
        };

        public static string GetLivePath(string gameRoot, string moduleFile)
            => Path.Combine(gameRoot, "NuclearOption_Data", "Managed", moduleFile);

        public static string GetVanillaBackupPath(string gameRoot, string moduleFile)
            => GetLivePath(gameRoot, moduleFile) + VanillaBackupExtension;

        public static bool ContainsNOLoaderMarkers(string filePath)
        {
            if (!File.Exists(filePath))
                return false;
            return ContainsNOLoaderMarkers(File.ReadAllBytes(filePath));
        }

        public static bool ContainsNOLoaderMarkers(byte[] bytes)
        {
            if (bytes.Length == 0)
                return false;

            string text = Encoding.ASCII.GetString(bytes);
            foreach (string marker in Markers)
            {
                if (text.IndexOf(marker, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        public static bool IsValidVanillaSnapshot(string filePath)
            => File.Exists(filePath) && !ContainsNOLoaderMarkers(filePath);

        /// <summary>Remove backup files that contain NOLoader IL — they cannot be used for restore or patch source.</summary>
        public static bool TryPurgeInvalidVanillaBackup(string gameRoot, string moduleFile)
        {
            string vanilla = GetVanillaBackupPath(gameRoot, moduleFile);
            if (!File.Exists(vanilla) || IsValidVanillaSnapshot(vanilla))
                return false;

            File.Delete(vanilla);
            return true;
        }

        /// <summary>Save live DLL once before first patch — only when still vanilla.</summary>
        public static void EnsureVanillaBackup(string gameRoot, string moduleFile)
        {
            string live = GetLivePath(gameRoot, moduleFile);
            string vanilla = GetVanillaBackupPath(gameRoot, moduleFile);
            if (!File.Exists(live))
                return;

            if (File.Exists(vanilla) && !IsValidVanillaSnapshot(vanilla))
                File.Delete(vanilla);

            if (File.Exists(vanilla))
                return;

            byte[] bytes = File.ReadAllBytes(live);
            if (ContainsNOLoaderMarkers(bytes))
                return;

            if (string.Equals(moduleFile, "Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase)
                && !ManagedAssemblyCompatibility.TryValidateCSharpMirage(gameRoot, bytes, out _))
                return;

            File.WriteAllBytes(vanilla, bytes);
        }

        public static bool TryRestoreVanilla(string gameRoot, string moduleFile)
        {
            string live = GetLivePath(gameRoot, moduleFile);
            string vanilla = GetVanillaBackupPath(gameRoot, moduleFile);
            if (File.Exists(vanilla))
            {
                if (!IsValidVanillaSnapshot(vanilla))
                {
                    File.Delete(vanilla);
                }
                else
                {
                    File.Copy(vanilla, live, overwrite: true);
                    return true;
                }
            }

            string legacy = live + LegacyBackupExtension;
            if (File.Exists(legacy) && !ContainsNOLoaderMarkers(legacy))
            {
                File.Copy(legacy, live, overwrite: true);
                return true;
            }

            return false;
        }

        public static bool IsVanilla(string gameRoot, string moduleFile)
        {
            string live = GetLivePath(gameRoot, moduleFile);
            return File.Exists(live) && !ContainsNOLoaderMarkers(live);
        }
    }
}
