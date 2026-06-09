using System;
using NOLoader.API.Manifest;
using NOLoader.Patcher;

namespace NOLoader.PatchTool
{
    internal static class ModPatchHashTool
    {
        public static int Compute(string gameRoot, string moduleFile, string target, string inject, string method)
        {
            byte[]? snapshot = null;
            byte[]? bytes = AssemblyPatcher.LoadManagedModuleBytes(gameRoot, moduleFile, ref snapshot);
            if (bytes == null)
            {
                Console.Error.WriteLine("Missing module: " + moduleFile);
                return 1;
            }

            var descriptor = new PatchDescriptor
            {
                Target = target,
                Inject = inject,
                Method = method
            };

            if (!AssemblyPatcher.TryComputeTargetSignatureHash(bytes, gameRoot, descriptor, out string hash, out string error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            Console.WriteLine(hash);
            return 0;
        }
    }
}
