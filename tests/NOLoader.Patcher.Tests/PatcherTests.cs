using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NOLoader.API.Manifest;
using NOLoader.Patcher;
using Xunit;

namespace NOLoader.Patcher.Tests
{
    public class SignatureHashTests
    {
        [Fact]
        public void ComputeMethodSignatureHash_IsDeterministic()
        {
            var asm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("TestAsm", new Version(1, 0)),
                "TestMod",
                ModuleKind.Dll);
            var type = new TypeDefinition("NS", "TestType",
                TypeAttributes.Public | TypeAttributes.Class,
                asm.MainModule.ImportReference(typeof(object)));
            asm.MainModule.Types.Add(type);
            var method = new MethodDefinition("Run",
                MethodAttributes.Public | MethodAttributes.Static,
                asm.MainModule.TypeSystem.Void);
            type.Methods.Add(method);

            string h1 = AssemblyPatcher.ComputeMethodSignatureHash(method);
            string h2 = AssemblyPatcher.ComputeMethodSignatureHash(method);
            Assert.Equal(h1, h2);
        }
    }

    public class PrefixSkipIlTests
    {
        [Fact]
        public void PrefixSkip_BoolReturn_PatchesWithoutRollback()
        {
            var targetAsm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("PrefixSkipTarget", new Version(1, 0)),
                "TargetModule",
                ModuleKind.Dll);

            var targetType = new TypeDefinition("T", "Target",
                TypeAttributes.Public | TypeAttributes.Class,
                targetAsm.MainModule.ImportReference(typeof(object)));
            targetAsm.MainModule.Types.Add(targetType);

            var targetMethod = new MethodDefinition("Gate",
                MethodAttributes.Public | MethodAttributes.Static,
                targetAsm.MainModule.TypeSystem.Boolean);
            targetType.Methods.Add(targetMethod);
            targetMethod.Body = new MethodBody(targetMethod);
            var il = targetMethod.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Ret));

            var injectAsm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("PrefixSkipInject", new Version(1, 0)),
                "InjectModule",
                ModuleKind.Dll);

            var injectType = new TypeDefinition("T", "Inject",
                TypeAttributes.Public | TypeAttributes.Class,
                injectAsm.MainModule.ImportReference(typeof(object)));
            injectAsm.MainModule.Types.Add(injectType);

            var injectMethod = new MethodDefinition("Prefix",
                MethodAttributes.Public | MethodAttributes.Static,
                injectAsm.MainModule.TypeSystem.Boolean);
            injectType.Methods.Add(injectMethod);
            injectMethod.Body = new MethodBody(injectMethod);
            var injectIl = injectMethod.Body.GetILProcessor();
            injectIl.Append(injectIl.Create(OpCodes.Ldc_I4_0));
            injectIl.Append(injectIl.Create(OpCodes.Ret));

            string tempDir = Path.Combine(Path.GetTempPath(), "PrefixSkip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string injectPath = Path.Combine(tempDir, "Inject.dll");
            injectAsm.Write(injectPath);

            using var ms = new MemoryStream();
            targetAsm.Write(ms);
            byte[] original = ms.ToArray();

            try
            {
                var entry = new PatchEntry
                {
                    ModId = "test",
                    ModFolder = tempDir,
                    InjectAssembly = "Inject.dll",
                    Descriptor = new PatchDescriptor
                    {
                        Target = "Target::Gate",
                        Inject = "Inject::Prefix",
                        Method = "PrefixSkip"
                    }
                };

                PatchSignatureResolver.PopulateMissingCoreHashes(original, tempDir, new List<PatchEntry> { entry });
                var result = AssemblyPatcher.ApplyPatches(original, new List<PatchEntry> { entry }, tempDir);
                Assert.False(result.RolledBack);
                Assert.NotNull(result.PatchedBytes);
                Assert.Empty(result.Errors);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }
    }

    public class ThrottleIlTests
    {
        [Fact]
        public void Postfix_WithThrottleEveryN_EmitsPatchThrottleGate()
        {
            var targetAsm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("ThrottleTarget", new Version(1, 0)),
                "TargetModule",
                ModuleKind.Dll);

            var targetType = new TypeDefinition("Game", "Target",
                TypeAttributes.Public | TypeAttributes.Class,
                targetAsm.MainModule.ImportReference(typeof(object)));
            targetAsm.MainModule.Types.Add(targetType);

            var targetMethod = new MethodDefinition("Tick",
                MethodAttributes.Public | MethodAttributes.HideBySig,
                targetAsm.MainModule.TypeSystem.Void);
            targetType.Methods.Add(targetMethod);
            targetMethod.Body = new MethodBody(targetMethod);
            var il = targetMethod.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ret));

            var injectAsm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("ThrottleInject", new Version(1, 0)),
                "InjectModule",
                ModuleKind.Dll);

            var injectType = new TypeDefinition("Mod", "Patches",
                TypeAttributes.Public | TypeAttributes.Class,
                injectAsm.MainModule.ImportReference(typeof(object)));
            injectAsm.MainModule.Types.Add(injectType);

            var injectMethod = new MethodDefinition("TickPostfix",
                MethodAttributes.Public | MethodAttributes.Static,
                injectAsm.MainModule.TypeSystem.Void);
            injectType.Methods.Add(injectMethod);
            injectMethod.Body = new MethodBody(injectMethod);
            var injectIl = injectMethod.Body.GetILProcessor();
            injectIl.Append(injectIl.Create(OpCodes.Ret));

            string tempDir = Path.Combine(Path.GetTempPath(), "ThrottleIl_" + Guid.NewGuid().ToString("N"));
            string gameRoot = tempDir;
            string noloaderCore = Path.Combine(tempDir, "NOLoader", "core");
            Directory.CreateDirectory(noloaderCore);

            string repoApi = Path.Combine(FindRepoRoot(), "src", "NOLoader.API", "bin", "RDYTU", "netstandard2.0", "NOLoader.API.dll");
            if (!File.Exists(repoApi))
                repoApi = Path.Combine(FindRepoRoot(), "src", "NOLoader.API", "bin", "Release", "netstandard2.0", "NOLoader.API.dll");
            Assert.True(File.Exists(repoApi), "Build NOLoader.API RDYTU first");
            File.Copy(repoApi, Path.Combine(noloaderCore, "NOLoader.API.dll"), true);
            injectAsm.Write(Path.Combine(tempDir, "Inject.dll"));

            using var ms = new MemoryStream();
            targetAsm.Write(ms);
            byte[] original = ms.ToArray();

            try
            {
                var entry = new PatchEntry
                {
                    ModId = "throttle.test",
                    ModFolder = tempDir,
                    InjectAssembly = "Inject.dll",
                    Descriptor = new PatchDescriptor
                    {
                        Target = "Target::Tick",
                        Inject = "Patches::TickPostfix",
                        Method = "Postfix",
                        ThrottleEveryN = 4
                    }
                };

                PatchSignatureResolver.PopulateMissingCoreHashes(original, gameRoot, new List<PatchEntry> { entry });
                var result = AssemblyPatcher.ApplyPatches(original, new List<PatchEntry> { entry }, gameRoot);
                Assert.False(result.RolledBack, string.Join("; ", result.Errors));
                Assert.NotNull(result.PatchedBytes);
                Assert.Empty(result.Errors);

                string text = Encoding.ASCII.GetString(result.PatchedBytes!);
                Assert.Contains("PatchThrottleGate", text);
                Assert.Contains("ShouldRun", text);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }

        private static string FindRepoRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "RDYTU", "NOLoader.RDYTU.sln")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
            }

            throw new InvalidOperationException("Repo root not found");
        }
    }

    public class GameAssemblyPatchIntegrationTests
    {
        private static string GetGameRoot()
        {
            return Environment.GetEnvironmentVariable("NOLOADER_GAME_ROOT")
                ?? @"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option";
        }

#if NOLOADER_FULL_TESTS
        [Fact]
        public void ApplyCorePatches_ToVanillaGameAssembly_Succeeds()
        {
            string gameRoot = GetGameRoot();
            string managed = Path.Combine(gameRoot, "NuclearOption_Data", "Managed");
            string vanilla = Path.Combine(managed, "Assembly-CSharp.dll.noloader.bak");
            if (!File.Exists(vanilla))
                vanilla = Path.Combine(managed, "Assembly-CSharp.dll");

            if (!File.Exists(vanilla))
            {
                return;
            }

            string repoRoot = FindRepoRoot();
            string coreDir = Path.Combine(repoRoot, "src", "NOLoader.Core", "bin", "DEV_SDK", "net48");
            string registryDir = Path.Combine(repoRoot, "src", "NOLoader.Registry", "bin", "DEV_SDK", "net48");
            if (!File.Exists(Path.Combine(coreDir, "NOLoader.Core.dll")))
                return;

            string loaderRoot = Path.Combine(Path.GetTempPath(), "NOLoaderPatchTest_" + Guid.NewGuid().ToString("N"));
            string coreDeploy = Path.Combine(loaderRoot, "core");
            Directory.CreateDirectory(coreDeploy);
            File.Copy(Path.Combine(coreDir, "NOLoader.Core.dll"), Path.Combine(coreDeploy, "NOLoader.Core.dll"), true);
            File.Copy(Path.Combine(registryDir, "NOLoader.Registry.dll"), Path.Combine(coreDeploy, "NOLoader.Registry.dll"), true);

            byte[] bytes = File.ReadAllBytes(vanilla);
            var plan = CoreBootstrapPatches.CreateGameAssemblyPlan(loaderRoot);

            try
            {
                var result = AssemblyPatcher.ApplyPatches(bytes, plan, gameRoot);
                Assert.False(result.RolledBack);
                Assert.NotNull(result.PatchedBytes);
                Assert.Empty(result.Errors);
            }
            finally
            {
                try { Directory.Delete(loaderRoot, true); } catch { /* ignore */ }
            }
        }
#endif

        [Fact]
        public void PrefixSkip_InstanceValueTypeArg_BoxesBeforeCall()
        {
            var targetAsm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("PrefixSkipInstTarget", new Version(1, 0)),
                "TargetModule",
                ModuleKind.Dll);

            var mapKeyType = new TypeDefinition("NuclearOption.SceneLoading", "MapKey",
                TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                targetAsm.MainModule.ImportReference(typeof(ValueType)));
            targetAsm.MainModule.Types.Add(mapKeyType);

            var targetType = new TypeDefinition("NuclearOption.SceneLoading", "MapLoader",
                TypeAttributes.Public | TypeAttributes.Class,
                targetAsm.MainModule.ImportReference(typeof(object)));
            targetAsm.MainModule.Types.Add(targetType);

            var targetMethod = new MethodDefinition("CanLoad",
                MethodAttributes.Public | MethodAttributes.HideBySig,
                targetAsm.MainModule.TypeSystem.Boolean);
            targetMethod.Parameters.Add(new ParameterDefinition("key", ParameterAttributes.None, mapKeyType));
            targetType.Methods.Add(targetMethod);
            targetMethod.Body = new MethodBody(targetMethod);
            var il = targetMethod.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Ret));

            var injectAsm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("PrefixSkipInstInject", new Version(1, 0)),
                "InjectModule",
                ModuleKind.Dll);

            var injectType = new TypeDefinition("NOLoader.Core.Gates", "MissionGateHooks",
                TypeAttributes.Public | TypeAttributes.Class,
                injectAsm.MainModule.ImportReference(typeof(object)));
            injectAsm.MainModule.Types.Add(injectType);

            var objectType = injectAsm.MainModule.ImportReference(typeof(object));
            var injectMethod = new MethodDefinition("CanLoadPrefixSkip",
                MethodAttributes.Public | MethodAttributes.Static,
                injectAsm.MainModule.TypeSystem.Boolean);
            injectMethod.Parameters.Add(new ParameterDefinition("mapLoader", ParameterAttributes.None, objectType));
            injectMethod.Parameters.Add(new ParameterDefinition("mapKey", ParameterAttributes.None, objectType));
            injectType.Methods.Add(injectMethod);
            injectMethod.Body = new MethodBody(injectMethod);
            var injectIl = injectMethod.Body.GetILProcessor();
            injectIl.Append(injectIl.Create(OpCodes.Ldc_I4_1));
            injectIl.Append(injectIl.Create(OpCodes.Ret));

            string tempDir = Path.Combine(Path.GetTempPath(), "PrefixSkipInst_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string injectPath = Path.Combine(tempDir, "Inject.dll");
            injectAsm.Write(injectPath);

            using var ms = new MemoryStream();
            targetAsm.Write(ms);
            byte[] original = ms.ToArray();

            try
            {
                var entry = new PatchEntry
                {
                    ModId = "test",
                    ModFolder = tempDir,
                    InjectAssembly = "Inject.dll",
                    Descriptor = new PatchDescriptor
                    {
                        Target = "NuclearOption.SceneLoading.MapLoader::CanLoad",
                        Inject = "NOLoader.Core.Gates.MissionGateHooks::CanLoadPrefixSkip",
                        Method = "PrefixSkip"
                    }
                };

                PatchSignatureResolver.PopulateMissingCoreHashes(original, tempDir, new List<PatchEntry> { entry });
                var result = AssemblyPatcher.ApplyPatches(original, new List<PatchEntry> { entry }, tempDir);
                Assert.False(result.RolledBack);
                Assert.NotNull(result.PatchedBytes);
                Assert.Empty(result.Errors);

                using var patched = AssemblyDefinition.ReadAssembly(new MemoryStream(result.PatchedBytes!));
                MethodDefinition? patchedMethod = patched.MainModule.Types
                    .First(t => t.Name == "MapLoader")
                    .Methods.First(m => m.Name == "CanLoad");
                Assert.Contains(patchedMethod.Body.Instructions, i => i.OpCode == OpCodes.Box);
                Assert.Contains(patchedMethod.Body.Instructions, i => i.OpCode == OpCodes.Brtrue_S);
                Assert.True(patchedMethod.Body.Instructions[0].OpCode != OpCodes.Ret, "method must not start with ret");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void Postfix_ObjectInstance_OnInstanceMethodWithNoArgs_Succeeds()
        {
            var targetAsm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("ObjInstTarget", new Version(1, 0)),
                "TargetModule",
                ModuleKind.Dll);

            var targetType = new TypeDefinition("Game", "Panel",
                TypeAttributes.Public | TypeAttributes.Class,
                targetAsm.MainModule.ImportReference(typeof(object)));
            targetAsm.MainModule.Types.Add(targetType);

            var targetMethod = new MethodDefinition("Refresh",
                MethodAttributes.Public | MethodAttributes.HideBySig,
                targetAsm.MainModule.TypeSystem.Void);
            targetType.Methods.Add(targetMethod);
            targetMethod.Body = new MethodBody(targetMethod);
            var il = targetMethod.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ret));

            var injectAsm = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("ObjInstInject", new Version(1, 0)),
                "InjectModule",
                ModuleKind.Dll);

            var injectType = new TypeDefinition("Mod", "Patches",
                TypeAttributes.Public | TypeAttributes.Class,
                injectAsm.MainModule.ImportReference(typeof(object)));
            injectAsm.MainModule.Types.Add(injectType);

            var objectType = injectAsm.MainModule.ImportReference(typeof(object));
            var injectMethod = new MethodDefinition("RefreshPostfix",
                MethodAttributes.Public | MethodAttributes.Static,
                injectAsm.MainModule.TypeSystem.Void);
            injectMethod.Parameters.Add(new ParameterDefinition("__instance", ParameterAttributes.None, objectType));
            injectType.Methods.Add(injectMethod);
            injectMethod.Body = new MethodBody(injectMethod);
            var injectIl = injectMethod.Body.GetILProcessor();
            injectIl.Append(injectIl.Create(OpCodes.Ret));

            string tempDir = Path.Combine(Path.GetTempPath(), "ObjInst_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            injectAsm.Write(Path.Combine(tempDir, "Inject.dll"));

            using var ms = new MemoryStream();
            targetAsm.Write(ms);
            byte[] original = ms.ToArray();

            try
            {
                var entry = new PatchEntry
                {
                    ModId = "test",
                    ModFolder = tempDir,
                    InjectAssembly = "Inject.dll",
                    Descriptor = new PatchDescriptor
                    {
                        Target = "Panel::Refresh",
                        Inject = "Patches::RefreshPostfix",
                        Method = "Postfix"
                    }
                };

                PatchSignatureResolver.PopulateMissingCoreHashes(original, tempDir, new List<PatchEntry> { entry });
                var result = AssemblyPatcher.ApplyPatches(original, new List<PatchEntry> { entry }, tempDir);
                Assert.False(result.RolledBack, string.Join("; ", result.Errors));
                Assert.NotNull(result.PatchedBytes);
                Assert.Empty(result.Errors);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void ApplyCanLoadGatePatch_ToVanillaGameAssembly_Succeeds()
        {
            string gameRoot = GetGameRoot();
            string managed = Path.Combine(gameRoot, "NuclearOption_Data", "Managed");
            string vanilla = Path.Combine(managed, "Assembly-CSharp.dll.noloader.bak");
            if (!File.Exists(vanilla))
                vanilla = Path.Combine(managed, "Assembly-CSharp.dll");

            if (!File.Exists(vanilla))
                return;

            string repoRoot = FindRepoRoot();
            string coreDir = Path.Combine(repoRoot, "src", "NOLoader.Core", "bin", "DEV_SDK", "net48");
            if (!File.Exists(Path.Combine(coreDir, "NOLoader.Core.dll")))
                return;

            string loaderRoot = Path.Combine(Path.GetTempPath(), "NOLoaderCanLoadPatch_" + Guid.NewGuid().ToString("N"));
            string coreDeploy = Path.Combine(loaderRoot, "core");
            Directory.CreateDirectory(coreDeploy);
            File.Copy(Path.Combine(coreDir, "NOLoader.Core.dll"), Path.Combine(coreDeploy, "NOLoader.Core.dll"), true);

            byte[] bytes = File.ReadAllBytes(vanilla);
            var plan = new List<PatchEntry>
            {
                Patch(coreDeploy,
                    "NuclearOption.SceneLoading.MapLoader::CanLoad",
                    "NOLoader.Core.Gates.MissionGateHooks::CanLoadPrefixSkip",
                    "PrefixSkip",
                    "NOLoader.Core.dll")
            };

            PatchSignatureResolver.PopulateMissingCoreHashes(bytes, gameRoot, plan);

            try
            {
                var result = AssemblyPatcher.ApplyPatches(bytes, plan, gameRoot);
                Assert.Empty(result.Errors);
                Assert.False(result.RolledBack, string.Join("; ", result.Errors));
                Assert.NotNull(result.PatchedBytes);
            }
            finally
            {
                try { Directory.Delete(loaderRoot, true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void ApplyUnityScenePatch_ToVanillaUnityModule_Succeeds()
        {
            string gameRoot = GetGameRoot();
            string unityPath = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", "UnityEngine.CoreModule.dll.noloader.bak");
            if (!File.Exists(unityPath))
                unityPath = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", "UnityEngine.CoreModule.dll");

            if (!File.Exists(unityPath))
                return;

            string repoRoot = FindRepoRoot();
            string coreDir = Path.Combine(repoRoot, "src", "NOLoader.Core", "bin", "DEV_SDK", "net48");
            if (!File.Exists(Path.Combine(coreDir, "NOLoader.Core.dll")))
                return;

            string loaderRoot = Path.Combine(Path.GetTempPath(), "NOLoaderUnityPatchTest_" + Guid.NewGuid().ToString("N"));
            string coreDeploy = Path.Combine(loaderRoot, "core");
            Directory.CreateDirectory(coreDeploy);
            File.Copy(Path.Combine(coreDir, "NOLoader.Core.dll"), Path.Combine(coreDeploy, "NOLoader.Core.dll"), true);

            byte[] bytes = File.ReadAllBytes(unityPath);
            var plan = new List<PatchEntry>
            {
                Patch(coreDeploy,
                    "UnityEngine.SceneManagement.SceneManager::LoadSceneAsync",
                    "NOLoader.Core.Gates.MissionGateHooks::SceneLoadPrefixSkip",
                    "PrefixSkip",
                    "NOLoader.Core.dll")
            };

            PatchSignatureResolver.PopulateMissingCoreHashes(bytes, gameRoot, plan);

            try
            {
                var result = AssemblyPatcher.ApplyPatches(bytes, plan, gameRoot, bytes);
                Assert.Empty(result.Errors);
                Assert.False(result.RolledBack, string.Join("; ", result.Errors));
                Assert.NotNull(result.PatchedBytes);
            }
            finally
            {
                try { Directory.Delete(loaderRoot, true); } catch { /* ignore */ }
            }
        }

        private static PatchEntry Patch(string coreDir, string target, string inject, string method, string injectDll)
        {
            return new PatchEntry
            {
                ModId = "test",
                ModFolder = coreDir,
                InjectAssembly = injectDll,
                Descriptor = new PatchDescriptor { Target = target, Inject = inject, Method = method }
            };
        }

        private static string FindRepoRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "DEV.SDK", "NOLoader.DEV_SDK.sln")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
            }

            throw new InvalidOperationException("Repo root not found");
        }
    }
}
