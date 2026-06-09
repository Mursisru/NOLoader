using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NOLoader.API.Manifest;

namespace NOLoader.Patcher
{
    public sealed class PatchResult
    {
        public byte[]? PatchedBytes;
        public List<string> Errors = new List<string>();
        public bool RolledBack;
    }

    public static class AssemblyPatcher
    {
        public const bool RequireSignatureHashes = true;

        private static byte[]? _originalSnapshot;
        private static Assembly? _injectedGameAssembly;

        public static Assembly? InjectedGameAssembly => _injectedGameAssembly;

        public static byte[]? LoadGameAssemblyBytes(string gameRoot)
            => LoadManagedModuleBytes(gameRoot, "Assembly-CSharp.dll", ref _originalSnapshot);

        public static byte[]? LoadLiveGameAssemblyBytes(string gameRoot)
            => LoadLiveManagedModuleBytes(gameRoot, "Assembly-CSharp.dll", ref _originalSnapshot);

        public static byte[]? LoadManagedModuleBytes(string gameRoot, string moduleFileName, ref byte[]? snapshotField)
        {
            string path = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", moduleFileName);
            string backup = path + ".noloader.bak";
            if (File.Exists(backup))
                path = backup;
            if (!File.Exists(path)) return null;
            snapshotField = File.ReadAllBytes(path);
            return (byte[])snapshotField.Clone();
        }

        public static byte[]? LoadLiveManagedModuleBytes(string gameRoot, string moduleFileName, ref byte[]? snapshotField)
        {
            string path = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", moduleFileName);
            if (!File.Exists(path))
                return null;
            snapshotField = File.ReadAllBytes(path);
            return (byte[])snapshotField.Clone();
        }

        public static void WriteManagedModuleBytes(string gameRoot, string moduleFileName, byte[] patchedBytes)
        {
            string gameAsmPath = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", moduleFileName);
            string backupPath = gameAsmPath + ".noloader.bak";
            if (!File.Exists(backupPath))
            {
                File.Copy(gameAsmPath, backupPath);
            }

            File.WriteAllBytes(gameAsmPath, patchedBytes);
        }

        /// <summary>Restore live module from .noloader.bak (RDYTU: strip global physics hooks).</summary>
        public static bool RestoreManagedModuleFromBackup(string gameRoot, string moduleFileName)
        {
            string gameAsmPath = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", moduleFileName);
            string backupPath = gameAsmPath + ".noloader.bak";
            if (!File.Exists(backupPath))
                return false;

            File.Copy(backupPath, gameAsmPath, overwrite: true);
            return true;
        }

        public static byte[]? LoadGameAssemblyBytesLegacy(string gameRoot)
        {
            string path = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", "Assembly-CSharp.dll");
            string backup = path + ".noloader.bak";
            if (File.Exists(backup))
                path = backup;
            if (!File.Exists(path)) return null;
            _originalSnapshot = File.ReadAllBytes(path);
            return (byte[])_originalSnapshot.Clone();
        }

        private static byte[]? _unitySnapshot;
        private static byte[]? _physicsSnapshot;

        public static byte[]? LoadUnityCoreModuleBytes(string gameRoot)
            => LoadManagedModuleBytes(gameRoot, "UnityEngine.CoreModule.dll", ref _unitySnapshot);

        public static byte[]? LoadUnityPhysicsModuleBytes(string gameRoot)
            => LoadManagedModuleBytes(gameRoot, "UnityEngine.PhysicsModule.dll", ref _physicsSnapshot);

        public static PatchResult ApplyPatches(byte[] assemblyBytes, IReadOnlyList<PatchEntry> plan, string gameRoot, byte[]? rollbackSnapshot = null)
        {
            var result = new PatchResult();
            byte[]? rollback = rollbackSnapshot ?? _originalSnapshot;
            if (plan.Count == 0)
            {
                result.PatchedBytes = assemblyBytes;
                return result;
            }

            try
            {
                using var ms = new MemoryStream(assemblyBytes);
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.Combine(gameRoot, "NuclearOption_Data", "Managed"));
                var readerParams = new ReaderParameters { AssemblyResolver = resolver };
                var asm = AssemblyDefinition.ReadAssembly(ms, readerParams);

                foreach (PatchEntry entry in plan)
                {
                    if (!TryApplySinglePatch(asm, entry, gameRoot, result.Errors))
                    {
                        result.RolledBack = true;
                        result.Errors.Add($"Patch failed for mod {entry.ModId}, rolling back all patches.");
                        result.PatchedBytes = rollback;
                        return result;
                    }
                }

                using var outMs = new MemoryStream();
                asm.Write(outMs);
                result.PatchedBytes = outMs.ToArray();
            }
            catch (Exception ex)
            {
                result.RolledBack = true;
                result.Errors.Add("Assembly patch exception: " + ex.Message);
                result.PatchedBytes = rollback;
            }

            return result;
        }

        private static bool TryApplySinglePatch(AssemblyDefinition gameAsm, PatchEntry entry, string gameRoot, List<string> errors)
        {
            ParseTarget(entry.Descriptor.Target, out string typeName, out string methodName);
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
            {
                errors.Add($"Invalid target: {entry.Descriptor.Target}");
                return false;
            }

            typeName = typeName.Replace('.', '/');

            TypeDefinition? targetType = FindTypeDefinition(gameAsm, typeName);
            if (targetType == null)
            {
                errors.Add($"Target type not found: {typeName}");
                return false;
            }

            ParseInject(entry.Descriptor.Inject, out string injectTypeName, out string injectMethodName);
            string injectFile = entry.InjectAssembly ?? FindModAssembly(entry.ModFolder, entry.ModId);
            string modAsmPath = Path.Combine(entry.ModFolder, injectFile);
            if (!File.Exists(modAsmPath))
            {
                errors.Add($"Mod assembly not found for patch: {modAsmPath}");
                return false;
            }

            var modAsm = AssemblyDefinition.ReadAssembly(modAsmPath);
            TypeDefinition? injectType = modAsm.MainModule.Types.FirstOrDefault(t => t.FullName == injectTypeName || t.Name == injectTypeName);
            if (injectType == null)
            {
                errors.Add($"Inject type not found: {injectTypeName}");
                return false;
            }

            MethodDefinition? injectMethod = injectType.Methods.FirstOrDefault(m => m.Name == injectMethodName && m.IsStatic);
            if (injectMethod == null)
            {
                errors.Add($"Inject method not found: {injectTypeName}::{injectMethodName}");
                return false;
            }

            MethodDefinition? targetMethod = FindTargetMethod(targetType, methodName, entry.Descriptor, injectMethod);
            if (targetMethod == null)
            {
                var candidates = targetType.Methods.Where(m => m.Name == methodName && m.HasBody).ToList();
                if (candidates.Count > 0 && !string.IsNullOrEmpty(entry.Descriptor.ExpectedSignatureHash))
                {
                    string candidateHash = ComputeMethodSignatureHash(candidates[0]);
                    errors.Add($"Signature mismatch for {typeName}::{methodName} expected={entry.Descriptor.ExpectedSignatureHash} actual={candidateHash}");
                }
                else
                {
                    errors.Add($"Target method not found: {typeName}::{methodName}");
                }

                return false;
            }

            if (string.IsNullOrEmpty(entry.Descriptor.ExpectedSignatureHash))
            {
                errors.Add($"Missing expectedSignatureHash for {entry.Descriptor.Target}");
                return false;
            }

            string actual = ComputeMethodSignatureHash(targetMethod);
            if (!string.Equals(actual, entry.Descriptor.ExpectedSignatureHash, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Signature mismatch for {typeName}::{methodName} expected={entry.Descriptor.ExpectedSignatureHash} actual={actual}");
                return false;
            }

            if (!gameAsm.MainModule.AssemblyReferences.Any(r => r.Name == modAsm.Name.Name))
            {
                gameAsm.MainModule.AssemblyReferences.Add(new AssemblyNameReference(modAsm.Name.Name, modAsm.Name.Version));
            }

            var injectRef = gameAsm.MainModule.ImportReference(injectMethod);
            ILProcessor il = targetMethod.Body.GetILProcessor();
            var call = il.Create(OpCodes.Call, injectRef);

            if (string.Equals(entry.Descriptor.Method, "Prefix", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Descriptor.Method, "PrefixSkip", StringComparison.OrdinalIgnoreCase))
            {
                bool skipOriginal = string.Equals(entry.Descriptor.Method, "PrefixSkip", StringComparison.OrdinalIgnoreCase)
                    && injectMethod.ReturnType.FullName == "System.Boolean";
                InsertPrefixCall(il, targetMethod, injectMethod, call, skipOriginal);
            }
            else
            {
                var retInstructions = targetMethod.Body.Instructions
                    .Where(i => i.OpCode == OpCodes.Ret)
                    .ToList();

                if (retInstructions.Count == 0)
                {
                    InsertInjectCall(il, targetMethod, injectMethod, null, call);
                }
                else
                {
                    foreach (Instruction ret in retInstructions)
                    {
                        var retCall = il.Create(OpCodes.Call, injectRef);
                        InsertInjectCall(il, targetMethod, injectMethod, ret, retCall);
                    }
                }
            }

            return true;
        }

        private static TypeDefinition? FindTypeDefinition(AssemblyDefinition gameAsm, string typeName)
        {
            string[] candidates =
            {
                typeName,
                typeName.Replace('.', '/'),
                typeName.Replace('/', '.')
            };

            foreach (TypeDefinition type in gameAsm.MainModule.Types)
            {
                foreach (string candidate in candidates)
                {
                    TypeDefinition? match = FindTypeDefinitionRecursive(type, candidate);
                    if (match != null)
                        return match;
                }
            }

            return null;
        }

        private static TypeDefinition? FindTypeDefinitionRecursive(TypeDefinition type, string typeName)
        {
            if (TypeNamesEqual(type.FullName, typeName) || string.Equals(type.Name, typeName, StringComparison.Ordinal))
                return type;

            foreach (TypeDefinition nested in type.NestedTypes)
            {
                TypeDefinition? match = FindTypeDefinitionRecursive(nested, typeName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static bool TypeNamesEqual(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal)
                || string.Equals(left.Replace('.', '/'), right.Replace('.', '/'), StringComparison.Ordinal)
                || string.Equals(left.Replace('/', '.'), right.Replace('/', '.'), StringComparison.Ordinal);
        }

        private static MethodDefinition? FindTargetMethod(
            TypeDefinition targetType,
            string methodName,
            PatchDescriptor descriptor,
            MethodDefinition? injectMethod)
        {
            var candidates = targetType.Methods.Where(m => m.Name == methodName && m.HasBody).ToList();
            if (candidates.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(descriptor.ExpectedSignatureHash))
            {
                foreach (MethodDefinition candidate in candidates)
                {
                    if (string.Equals(
                            ComputeMethodSignatureHash(candidate),
                            descriptor.ExpectedSignatureHash,
                            StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }

                return null;
            }

            if (injectMethod != null)
            {
                var argMatches = candidates
                    .Where(m => m.Parameters.Count + (m.IsStatic ? 0 : 1) == injectMethod.Parameters.Count)
                    .ToList();

                if (argMatches.Count > 1)
                {
                    MethodDefinition? instanceNoArgs = argMatches.FirstOrDefault(m => !m.IsStatic && m.Parameters.Count == 0);
                    if (instanceNoArgs != null)
                        return instanceNoArgs;

                    MethodDefinition? staticMatch = argMatches.FirstOrDefault(m => m.IsStatic);
                    if (staticMatch != null)
                        return staticMatch;
                }

                if (argMatches.Count == 1)
                    return argMatches[0];
            }

            if (candidates.Count == 1)
                return candidates[0];

            MethodDefinition? stringParam = candidates.FirstOrDefault(m =>
                m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String");
            if (stringParam != null)
                return stringParam;

            MethodDefinition? instanceFallback = candidates.FirstOrDefault(m => !m.IsStatic && m.Parameters.Count == 0);
            return instanceFallback ?? candidates[0];
        }

        private static void InsertPrefixCall(
            ILProcessor il,
            MethodDefinition targetMethod,
            MethodDefinition injectMethod,
            Instruction call,
            bool skipOriginal)
        {
            var loads = CreateInjectArgumentLoads(il, targetMethod, injectMethod);
            Instruction entry = targetMethod.Body.Instructions[0];

            var prefix = new List<Instruction>();
            prefix.AddRange(loads);
            prefix.Add(call);

            if (skipOriginal)
            {
                prefix.Add(il.Create(OpCodes.Brtrue_S, entry));
                prefix.AddRange(CreateDefaultReturnInstructions(il, targetMethod));
                prefix.Add(il.Create(OpCodes.Ret));
            }

            foreach (Instruction ins in prefix)
                il.InsertBefore(entry, ins);
        }

        private static List<Instruction> CreateDefaultReturnInstructions(ILProcessor il, MethodDefinition targetMethod)
        {
            var list = new List<Instruction>();
            TypeReference retType = targetMethod.ReturnType;
            if (retType.FullName == "System.Void")
                return list;

            if (retType.FullName == "System.Boolean")
            {
                list.Add(il.Create(OpCodes.Ldc_I4_0));
                return list;
            }

            if (retType.IsValueType)
            {
                var temp = new VariableDefinition(retType);
                targetMethod.Body.Variables.Add(temp);
                list.Add(il.Create(OpCodes.Ldloca_S, temp));
                list.Add(il.Create(OpCodes.Initobj, retType));
                list.Add(il.Create(OpCodes.Ldloc_S, temp));
                return list;
            }

            list.Add(il.Create(OpCodes.Ldnull));
            return list;
        }

        private static void InsertInjectCall(
            ILProcessor il,
            MethodDefinition targetMethod,
            MethodDefinition injectMethod,
            Instruction? insertBefore,
            Instruction call)
        {
            var loads = CreateInjectArgumentLoads(il, targetMethod, injectMethod);
            if (insertBefore != null)
            {
                foreach (Instruction load in loads)
                    il.InsertBefore(insertBefore, load);
                il.InsertBefore(insertBefore, call);
                return;
            }

            foreach (Instruction load in loads)
                il.Append(load);
            il.Append(call);
        }

        private static List<Instruction> CreateInjectArgumentLoads(
            ILProcessor il,
            MethodDefinition targetMethod,
            MethodDefinition injectMethod)
        {
            var loads = new List<Instruction>();
            if (!injectMethod.IsStatic || injectMethod.Parameters.Count == 0)
                return loads;

            int expectedArgs = targetMethod.Parameters.Count + (targetMethod.IsStatic ? 0 : 1);
            if (injectMethod.Parameters.Count != expectedArgs)
                return loads;

            for (int i = 0; i < injectMethod.Parameters.Count; i++)
            {
                loads.Add(CreateLdarg(il, i));
                TypeReference injectParam = injectMethod.Parameters[i].ParameterType;
                if (injectParam.FullName != "System.Object")
                    continue;

                TypeReference sourceType = GetInjectArgumentSourceType(targetMethod, i);
                if (sourceType.IsValueType)
                    loads.Add(il.Create(OpCodes.Box, sourceType));
            }

            return loads;
        }

        private static TypeReference GetInjectArgumentSourceType(MethodDefinition targetMethod, int injectArgIndex)
        {
            if (!targetMethod.IsStatic && injectArgIndex == 0)
                return targetMethod.DeclaringType;

            int paramIndex = injectArgIndex - (targetMethod.IsStatic ? 0 : 1);
            return targetMethod.Parameters[paramIndex].ParameterType;
        }

        private static Instruction CreateLdarg(ILProcessor il, int index)
        {
            return index switch
            {
                0 => il.Create(OpCodes.Ldarg_0),
                1 => il.Create(OpCodes.Ldarg_1),
                2 => il.Create(OpCodes.Ldarg_2),
                3 => il.Create(OpCodes.Ldarg_3),
                _ => il.Create(OpCodes.Ldarg_S, (byte)index)
            };
        }

        private static string FindModAssembly(string modFolder, string modId)
        {
            string manifestPath = Path.Combine(modFolder, "mod.json");
            if (File.Exists(manifestPath))
            {
                string json = File.ReadAllText(manifestPath);
                const string key = "\"assembly\"";
                int idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    int start = json.IndexOf('"', idx + key.Length);
                    if (start >= 0)
                    {
                        int end = json.IndexOf('"', start + 1);
                        if (end > start)
                            return json.Substring(start + 1, end - start - 1);
                    }
                }
            }

            return Directory.GetFiles(modFolder, "*.dll")
                .Select(Path.GetFileName)
                .FirstOrDefault(f => f != null && !f.StartsWith("NOLoader.API", StringComparison.OrdinalIgnoreCase))
                ?? string.Empty;
        }

        private static void ParseTarget(string target, out string typeName, out string methodName)
        {
            typeName = string.Empty;
            methodName = string.Empty;
            int sep = target.IndexOf("::", StringComparison.Ordinal);
            if (sep < 0) return;
            typeName = target.Substring(0, sep);
            methodName = target.Substring(sep + 2);
        }

        private static void ParseInject(string inject, out string typeName, out string methodName)
        {
            ParseTarget(inject, out typeName, out methodName);
        }

        public static string ComputeMethodSignatureHash(MethodDefinition method)
        {
            var sb = new StringBuilder();
            sb.Append(method.DeclaringType.FullName).Append("::").Append(method.Name).Append("(");
            sb.Append(string.Join(",", method.Parameters.Select(p => p.ParameterType.FullName)));
            sb.Append(")");
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
        }

        public static bool TryComputePatchSignatureHash(
            byte[] assemblyBytes,
            string gameRoot,
            PatchEntry entry,
            out string hash,
            out string error)
        {
            hash = string.Empty;
            error = string.Empty;

            try
            {
                using var ms = new MemoryStream(assemblyBytes);
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.Combine(gameRoot, "NuclearOption_Data", "Managed"));
                var readerParams = new ReaderParameters { AssemblyResolver = resolver };
                var asm = AssemblyDefinition.ReadAssembly(ms, readerParams);

                ParseTarget(entry.Descriptor.Target, out string typeName, out string methodName);
                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
                {
                    error = "Invalid target: " + entry.Descriptor.Target;
                    return false;
                }

                typeName = typeName.Replace('.', '/');
                TypeDefinition? targetType = FindTypeDefinition(asm, typeName);
                if (targetType == null)
                {
                    error = "Target type not found: " + typeName;
                    return false;
                }

                ParseInject(entry.Descriptor.Inject, out string injectTypeName, out string injectMethodName);
                string injectFile = entry.InjectAssembly ?? FindModAssembly(entry.ModFolder, entry.ModId);
                string modAsmPath = Path.Combine(entry.ModFolder, injectFile);
                if (!File.Exists(modAsmPath))
                {
                    error = "Mod assembly not found for patch: " + modAsmPath;
                    return false;
                }

                var modAsm = AssemblyDefinition.ReadAssembly(modAsmPath);
                TypeDefinition? injectType = modAsm.MainModule.Types.FirstOrDefault(
                    t => t.FullName == injectTypeName || t.Name == injectTypeName);
                if (injectType == null)
                {
                    error = "Inject type not found: " + injectTypeName;
                    return false;
                }

                MethodDefinition? injectMethod = injectType.Methods.FirstOrDefault(m => m.Name == injectMethodName && m.IsStatic);
                if (injectMethod == null)
                {
                    error = "Inject method not found: " + injectTypeName + "::" + injectMethodName;
                    return false;
                }

                MethodDefinition? targetMethod = FindTargetMethod(targetType, methodName, entry.Descriptor, injectMethod);
                if (targetMethod == null)
                {
                    error = "Target method not found: " + typeName + "::" + methodName;
                    return false;
                }

                hash = ComputeMethodSignatureHash(targetMethod);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryComputeTargetSignatureHash(
            byte[] assemblyBytes,
            string gameRoot,
            PatchDescriptor descriptor,
            out string hash,
            out string error)
        {
            hash = string.Empty;
            error = string.Empty;

            try
            {
                using var ms = new MemoryStream(assemblyBytes);
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.Combine(gameRoot, "NuclearOption_Data", "Managed"));
                var readerParams = new ReaderParameters { AssemblyResolver = resolver };
                var asm = AssemblyDefinition.ReadAssembly(ms, readerParams);

                ParseTarget(descriptor.Target, out string typeName, out string methodName);
                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
                {
                    error = "Invalid target: " + descriptor.Target;
                    return false;
                }

                typeName = typeName.Replace('.', '/');
                TypeDefinition? targetType = FindTypeDefinition(asm, typeName);
                if (targetType == null)
                {
                    error = "Target type not found: " + typeName;
                    return false;
                }

                MethodDefinition? targetMethod = FindTargetMethod(targetType, methodName, descriptor, null);
                if (targetMethod == null)
                {
                    error = "Target method not found: " + typeName + "::" + methodName;
                    return false;
                }

                hash = ComputeMethodSignatureHash(targetMethod);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void InjectPatchedAssembly(byte[] patchedBytes)
        {
            _injectedGameAssembly = Assembly.Load(patchedBytes);
        }
    }
}
