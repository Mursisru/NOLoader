using System;
using System.Collections.Generic;
using System.IO;
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

            string gameRoot = args[0].Trim('"');
            RuntimeConfig.Load(gameRoot);
            string loaderRoot = Path.Combine(gameRoot, "NOLoader");
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

            return errors == 0 ? 0 : 1;
        }

        private static void ClearPhysicsPatchState(string loaderRoot)
        {
            PatchStateCache.Record(loaderRoot, "UnityEngine.PhysicsModule.dll", Array.Empty<string>());
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
    }
}
