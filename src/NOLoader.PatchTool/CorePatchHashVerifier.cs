using System;
using System.Collections.Generic;
using System.IO;
using NOLoader.Core.Patching;
using NOLoader.Core.Runtime;
using NOLoader.Patcher;

namespace NOLoader.PatchTool
{
    internal static class CorePatchHashVerifier
    {
        private static readonly (string Module, Func<string, List<PatchEntry>> PlanFactory)[] Modules =
        {
            ("Assembly-CSharp.dll", CoreBootstrapPatches.CreateGameAssemblyPlan),
            ("UnityEngine.CoreModule.dll", CoreBootstrapPatches.CreateUnityCorePlan),
            ("UnityEngine.PhysicsModule.dll", CoreBootstrapPatches.CreateUnityPhysicsPlan)
        };

        public static int Verify(string gameRoot)
        {
            RuntimeConfig.Load(gameRoot);
            string loaderRoot = Path.Combine(gameRoot, "NOLoader");
            int errors = 0;

            foreach ((string module, Func<string, List<PatchEntry>> planFactory) in Modules)
            {
                byte[]? bytes = LoadVanillaModuleBytes(gameRoot, module);
                if (bytes == null)
                {
                    Console.Error.WriteLine("Cannot load vanilla bytes for " + module + " (.noloader.bak missing?)");
                    errors++;
                    continue;
                }

                List<PatchEntry> plan = planFactory(loaderRoot);
                PatchSignatureResolver.PopulateMissingCoreHashes(bytes, gameRoot, plan);
                foreach (PatchEntry entry in plan)
                {
                    string? computed = entry.Descriptor.ExpectedSignatureHash;
                    if (string.IsNullOrEmpty(computed))
                    {
                        Console.Error.WriteLine("Failed to compute hash for " + entry.ModId);
                        errors++;
                        continue;
                    }

                    string? baked = CoreBootstrapPatchHashes.TryGet(entry.ModId);
                    if (string.IsNullOrEmpty(baked))
                    {
                        Console.Error.WriteLine("No baked hash for " + entry.ModId + " — run bake-core-patch-hashes.ps1");
                        errors++;
                        continue;
                    }

                    if (!string.Equals(baked, computed, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Error.WriteLine(
                            "Hash mismatch " + entry.ModId + ": baked=" + baked + " game=" + computed);
                        errors++;
                    }
                    else
                    {
                        Console.WriteLine("[ OK ] " + entry.ModId);
                    }
                }
            }

            if (errors == 0)
                Console.WriteLine("All " + CountBakedEntries() + " baked core hashes match vanilla game modules.");
            return errors == 0 ? 0 : 1;
        }

        private static int CountBakedEntries()
        {
            int count = 0;
            foreach ((string _, Func<string, List<PatchEntry>> planFactory) in Modules)
            {
                count += planFactory("").Count;
            }

            return count;
        }

        private static byte[]? LoadVanillaModuleBytes(string gameRoot, string moduleFile)
        {
            string managed = Path.Combine(gameRoot, "NuclearOption_Data", "Managed");
            string bakPath = Path.Combine(managed, moduleFile + ".noloader.bak");
            if (File.Exists(bakPath))
                return File.ReadAllBytes(bakPath);

            string dllPath = Path.Combine(managed, moduleFile);
            if (!File.Exists(dllPath))
                return null;

            Console.Error.WriteLine("Warning: using live " + moduleFile + " — prefer .noloader.bak for verify");
            return File.ReadAllBytes(dllPath);
        }
    }
}
