using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NOLoader.API;
using NOLoader.API.Manifest;
using NOLoader.Core.Manifest;
using NOLoader.Core.Patching;
using NOLoader.Core.Runtime;
using NOLoader.Patcher;

namespace NOLoader.PatchTool
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  NOLoader.PatchTool <GameRoot>");
                Console.Error.WriteLine("  NOLoader.PatchTool bake-hashes <GameRoot> <OutputCsPath>");
                Console.Error.WriteLine("  NOLoader.PatchTool verify-hashes <GameRoot>");
                Console.Error.WriteLine("  NOLoader.PatchTool restore-vanilla <GameRoot>");
                Console.Error.WriteLine("  NOLoader.PatchTool hash-patch <GameRoot> <ModuleFile> <Target> <Inject> <Method>");
                return 2;
            }

            if (string.Equals(args[0], "hash-patch", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 6)
                {
                    Console.Error.WriteLine(
                        "Usage: NOLoader.PatchTool hash-patch <GameRoot> <ModuleFile> <Target> <Inject> <Method>");
                    return 2;
                }

                return ModPatchHashTool.Compute(
                    args[1].Trim('"'),
                    args[2].Trim('"'),
                    args[3].Trim('"'),
                    args[4].Trim('"'),
                    args[5].Trim('"'));
            }

            if (string.Equals(args[0], "bake-hashes", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    Console.Error.WriteLine("Usage: NOLoader.PatchTool bake-hashes <GameRoot> <OutputCsPath>");
                    return 2;
                }

                return CorePatchHashBaker.Bake(args[1].Trim('"'), args[2].Trim('"'));
            }

            if (string.Equals(args[0], "verify-hashes", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: NOLoader.PatchTool verify-hashes <GameRoot>");
                    return 2;
                }

                return CorePatchHashVerifier.Verify(args[1].Trim('"'));
            }

            if (string.Equals(args[0], "restore-vanilla", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: NOLoader.PatchTool restore-vanilla <GameRoot>");
                    return 2;
                }

                return RestoreVanilla(args[1].Trim('"'));
            }

            string gameRoot = args[0].Trim('"');
            RuntimeConfig.Load(gameRoot);
            string loaderRoot = Path.Combine(gameRoot, "NOLoader");

            // Rebase Assembly-CSharp on immutable vanilla before applying the current plan.
            // Prevents stale DEV / mod-test IL (Motor::Thrust, PerfTest, MechanicsTest) from accumulating.
            if (ManagedModuleGuard.TryRestoreVanilla(gameRoot, "Assembly-CSharp.dll"))
                Console.WriteLine("Rebased Assembly-CSharp.dll on vanilla snapshot before patch plan");

            foreach (string module in new[] { "Assembly-CSharp.dll", "UnityEngine.CoreModule.dll", "UnityEngine.PhysicsModule.dll" })
            {
                if (ManagedModuleGuard.TryPurgeInvalidVanillaBackup(gameRoot, module))
                    Console.WriteLine("Removed corrupt vanilla backup: " + module + ManagedModuleGuard.VanillaBackupExtension);
            }

            if (!ManagedAssemblyCompatibility.TryValidateForPatch(gameRoot, out string compatError))
            {
                Console.Error.WriteLine("[preflight] " + compatError);
                return 1;
            }

            int errors = 0;

            errors += ApplyModule(gameRoot, loaderRoot, "Assembly-CSharp.dll", CoreBootstrapPatches.CreateGameAssemblyPlan(loaderRoot));
            errors += ApplyModule(gameRoot, loaderRoot, "UnityEngine.CoreModule.dll", CoreBootstrapPatches.CreateUnityCorePlan(loaderRoot));

            var physicsPlan = CoreBootstrapPatches.CreateUnityPhysicsPlan(loaderRoot);
            if (physicsPlan.Count == 0)
            {
                if (AssemblyPatcher.RestoreManagedModuleFromBackup(gameRoot, "UnityEngine.PhysicsModule.dll"))
                {
                    Console.WriteLine("Restored UnityEngine.PhysicsModule.dll (physics_catch_unity=0)");
                    ClearPhysicsPatchState(loaderRoot);
                }
            }
            else
            {
                errors += ApplyModule(gameRoot, loaderRoot, "UnityEngine.PhysicsModule.dll", physicsPlan);
            }

            var uiPlan = CoreBootstrapPatches.CreateUnityUiPlan(loaderRoot);
            if (uiPlan.Count == 0)
            {
                if (AssemblyPatcher.RestoreManagedModuleFromBackup(gameRoot, "UnityEngine.UI.dll"))
                {
                    Console.WriteLine("Restored UnityEngine.UI.dll (canvas_limiter=0)");
                    ClearUiPatchState(loaderRoot);
                }
            }
            else
            {
                errors += ApplyModule(gameRoot, loaderRoot, "UnityEngine.UI.dll", uiPlan);
            }

            errors += ApplyModAssemblyPatches(gameRoot, loaderRoot);

            return errors == 0 ? 0 : 1;
        }

        private static int ApplyModAssemblyPatches(string gameRoot, string loaderRoot)
        {
            string modsRoot = Path.Combine(loaderRoot, "mods");
            var manifests = ModManifestPipeline.ReadValidated(modsRoot, out List<string> gateErrors, gameRoot);
            foreach (string err in gateErrors)
                Console.Error.WriteLine("[mod-manifest] " + err);

            var fullPlan = PatchPlanBuilder.Build(manifests, LoadStage.Mission);
            if (fullPlan.Count == 0)
            {
                Console.WriteLine("No mod IL patches to pre-apply");
                return 0;
            }

            var pending = new List<PatchEntry>();
            foreach (PatchEntry entry in fullPlan)
            {
                string marker = PatchStateCache.ExtractMarker(entry.Descriptor.Inject);
                if (string.IsNullOrEmpty(marker))
                {
                    pending.Add(entry);
                    continue;
                }

                if (PatchStateCache.TryIsPatched(gameRoot, "Assembly-CSharp.dll", marker))
                    continue;

                pending.Add(entry);
            }

            if (pending.Count == 0)
            {
                Console.WriteLine("Mod IL already pre-applied (" + fullPlan.Count + " patch(es))");
                return 0;
            }

            byte[]? snapshotField = null;
            byte[]? bytes = AssemblyPatcher.LoadManagedModuleBytes(gameRoot, "Assembly-CSharp.dll", ref snapshotField);
            if (bytes == null)
            {
                Console.Error.WriteLine("Missing module for mod pre-patch: Assembly-CSharp.dll");
                return 1;
            }

            byte[]? rollback = (byte[])bytes.Clone();
            var result = AssemblyPatcher.ApplyPatches(bytes, pending, gameRoot, rollback);
            foreach (string err in result.Errors)
                Console.Error.WriteLine("[Assembly-CSharp.dll mod] " + err);

            if (result.RolledBack || result.PatchedBytes == null)
                return 1;

            AssemblyPatcher.WriteManagedModuleBytes(gameRoot, "Assembly-CSharp.dll", result.PatchedBytes);
            var markers = pending
                .Select(entry => PatchStateCache.ExtractMarker(entry.Descriptor.Inject))
                .Where(marker => !string.IsNullOrEmpty(marker))
                .ToList();
            PatchStateCache.Append(loaderRoot, "Assembly-CSharp.dll", markers);
            Console.WriteLine("Pre-applied " + pending.Count + " mod IL patch(es) to Assembly-CSharp.dll");
            return 0;
        }

        private static void ClearPhysicsPatchState(string loaderRoot)
        {
            PatchStateCache.Record(loaderRoot, "UnityEngine.PhysicsModule.dll", Array.Empty<string>());
        }

        private static void ClearUiPatchState(string loaderRoot)
        {
            PatchStateCache.Record(loaderRoot, "UnityEngine.UI.dll", Array.Empty<string>());
        }

        private static int ApplyModule(string gameRoot, string loaderRoot, string moduleFile, List<PatchEntry> plan)
        {
            if (plan.Count == 0)
                return 0;

            byte[]? snapshotField = null;
            byte[]? bytes = AssemblyPatcher.LoadManagedModuleBytes(gameRoot, moduleFile, ref snapshotField);
            if (bytes == null)
            {
                Console.Error.WriteLine("Missing module: " + moduleFile);
                return 1;
            }

            if (string.Equals(moduleFile, "Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase)
                && !ManagedAssemblyCompatibility.TryValidateCSharpMirage(gameRoot, bytes, out string mirageError))
            {
                Console.Error.WriteLine("[" + moduleFile + "] " + mirageError);
                return 1;
            }

            byte[]? rollback = (byte[])bytes.Clone();
            PatchSignatureResolver.PopulateMissingCoreHashes(bytes, gameRoot, plan);
            var result = AssemblyPatcher.ApplyPatches(bytes, plan, gameRoot, rollback);
            foreach (string err in result.Errors)
                Console.Error.WriteLine("[" + moduleFile + "] " + err);

            if (result.RolledBack || result.PatchedBytes == null)
                return 1;

            AssemblyPatcher.WriteManagedModuleBytes(gameRoot, moduleFile, result.PatchedBytes);
            var markers = new List<string>(plan.Count);
            foreach (PatchEntry entry in plan)
            {
                string marker = PatchStateCache.ExtractMarker(entry.Descriptor.Inject);
                if (!string.IsNullOrEmpty(marker))
                    markers.Add(marker);
            }

            PatchStateCache.Record(loaderRoot, moduleFile, markers);
            Console.WriteLine("Patched " + moduleFile);
            return 0;
        }

        private static int RestoreVanilla(string gameRoot)
        {
            string[] modules =
            {
                "Assembly-CSharp.dll",
                "UnityEngine.CoreModule.dll",
                "UnityEngine.PhysicsModule.dll",
                "UnityEngine.UI.dll"
            };

            int missing = 0;
            foreach (string module in modules)
            {
                if (ManagedModuleGuard.TryRestoreVanilla(gameRoot, module))
                {
                    Console.WriteLine("Restored " + module);
                    continue;
                }

                Console.Error.WriteLine("No vanilla snapshot for " + module);
                missing++;
            }

            string loaderRoot = Path.Combine(gameRoot, "NOLoader");
            if (Directory.Exists(loaderRoot))
            {
                string statePath = Path.Combine(loaderRoot, "patch_state.txt");
                if (File.Exists(statePath))
                    File.Delete(statePath);
            }

            return missing == 0 ? 0 : 1;
        }
    }
}
