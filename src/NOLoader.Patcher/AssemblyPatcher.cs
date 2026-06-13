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

        public static bool HasAssemblyReference(string gameRoot, string moduleFileName, string assemblyName)
        {
            string live = ManagedModuleGuard.GetLivePath(gameRoot, moduleFileName);
            if (!File.Exists(live))
                return false;

            using var ms = new MemoryStream(File.ReadAllBytes(live), writable: false);
            using var asm = AssemblyDefinition.ReadAssembly(ms);
            foreach (AssemblyNameReference reference in asm.MainModule.AssemblyReferences)
            {
                if (string.Equals(reference.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static byte[]? LoadManagedModuleBytes(string gameRoot, string moduleFileName, ref byte[]? snapshotField)
        {
            string live = ManagedModuleGuard.GetLivePath(gameRoot, moduleFileName);
            if (!File.Exists(live) && !File.Exists(ManagedModuleGuard.GetVanillaBackupPath(gameRoot, moduleFileName)))
                return null;

            string source = ResolvePatchSourcePath(gameRoot, moduleFileName);
            if (!File.Exists(source))
                return null;

            snapshotField = File.ReadAllBytes(source);
            return (byte[])snapshotField.Clone();
        }

        private static string ResolvePatchSourcePath(string gameRoot, string moduleFileName)
        {
            string live = ManagedModuleGuard.GetLivePath(gameRoot, moduleFileName);
            ManagedModuleGuard.TryPurgeInvalidVanillaBackup(gameRoot, moduleFileName);

            if (File.Exists(live) && ManagedModuleGuard.ContainsNOLoaderMarkers(live))
                return live;

            string vanilla = ManagedModuleGuard.GetVanillaBackupPath(gameRoot, moduleFileName);
            if (ManagedModuleGuard.IsValidVanillaSnapshot(vanilla))
                return vanilla;

            if (File.Exists(live) && !ManagedModuleGuard.ContainsNOLoaderMarkers(live))
                return live;

            string legacy = live + ManagedModuleGuard.LegacyBackupExtension;
            if (ManagedModuleGuard.IsValidVanillaSnapshot(legacy))
                return legacy;

            return live;
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
            string gameAsmPath = ManagedModuleGuard.GetLivePath(gameRoot, moduleFileName);
            ManagedModuleGuard.EnsureVanillaBackup(gameRoot, moduleFileName);
            File.WriteAllBytes(gameAsmPath, patchedBytes);
        }

        /// <summary>Restore live module from immutable vanilla snapshot (uninstall / repair).</summary>
        public static bool RestoreManagedModuleFromBackup(string gameRoot, string moduleFileName)
            => ManagedModuleGuard.TryRestoreVanilla(gameRoot, moduleFileName);

        public static byte[]? LoadGameAssemblyBytesLegacy(string gameRoot)
            => LoadManagedModuleBytes(gameRoot, "Assembly-CSharp.dll", ref _originalSnapshot);

        private static byte[]? _unitySnapshot;
        private static byte[]? _physicsSnapshot;
        private static byte[]? _uiSnapshot;

        public static byte[]? LoadUnityCoreModuleBytes(string gameRoot)
            => LoadManagedModuleBytes(gameRoot, "UnityEngine.CoreModule.dll", ref _unitySnapshot);

        public static byte[]? LoadUnityPhysicsModuleBytes(string gameRoot)
            => LoadManagedModuleBytes(gameRoot, "UnityEngine.PhysicsModule.dll", ref _physicsSnapshot);

        public static byte[]? LoadUnityUiModuleBytes(string gameRoot)
            => LoadManagedModuleBytes(gameRoot, "UnityEngine.UI.dll", ref _uiSnapshot);

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
            if (MethodAlreadyContainsInjectCall(targetMethod, injectMethodName))
                return true;

            if (string.Equals(entry.Descriptor.Method, "Redirect", StringComparison.OrdinalIgnoreCase))
                EnsureRedirectBody(targetMethod);

            ILProcessor il = targetMethod.Body.GetILProcessor();
            var call = il.Create(OpCodes.Call, injectRef);
            int throttleEveryN = entry.Descriptor.ThrottleEveryN;
            int patchSlotId = ComputePatchSlotId(entry);

            if (string.Equals(entry.Descriptor.Method, "Redirect", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryValidateRedirectSignature(targetMethod, injectMethod, out string redirectError))
                {
                    errors.Add(redirectError);
                    return false;
                }

                ApplyRedirectMethodBody(il, targetMethod, injectMethod, call);
            }
            else if (string.Equals(entry.Descriptor.Method, "Prefix", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Descriptor.Method, "PrefixSkip", StringComparison.OrdinalIgnoreCase))
            {
                var prefixLoads = CreateInjectArgumentLoads(il, targetMethod, injectMethod);
                if (injectMethod.Parameters.Count > 0 && prefixLoads.Count == 0)
                {
                    errors.Add($"Inject argument mismatch for {typeName}::{methodName} -> {injectTypeName}::{injectMethodName}");
                    return false;
                }

                bool skipOriginal = string.Equals(entry.Descriptor.Method, "PrefixSkip", StringComparison.OrdinalIgnoreCase)
                    && injectMethod.ReturnType.FullName == "System.Boolean";
                InsertPrefixCall(il, targetMethod, injectMethod, call, skipOriginal, gameAsm, gameRoot, patchSlotId, throttleEveryN);
            }
            else
            {
                var postfixLoads = CreateInjectArgumentLoads(il, targetMethod, injectMethod);
                if (injectMethod.Parameters.Count > 0 && postfixLoads.Count == 0)
                {
                    errors.Add($"Inject argument mismatch for {typeName}::{methodName} -> {injectTypeName}::{injectMethodName}");
                    return false;
                }

                var retInstructions = targetMethod.Body.Instructions
                    .Where(i => i.OpCode == OpCodes.Ret)
                    .ToList();

                if (retInstructions.Count == 0)
                {
                    InsertInjectCall(il, targetMethod, injectMethod, null, call, gameAsm, gameRoot, patchSlotId, throttleEveryN);
                }
                else
                {
                    foreach (Instruction ret in retInstructions)
                    {
                        var retCall = il.Create(OpCodes.Call, injectRef);
                        InsertInjectCall(il, targetMethod, injectMethod, ret, retCall, gameAsm, gameRoot, patchSlotId, throttleEveryN);
                    }
                }
            }

            return true;
        }

        private static bool MethodAlreadyContainsInjectCall(MethodDefinition targetMethod, string injectMethodName)
        {
            if (!targetMethod.HasBody)
                return false;

            foreach (Instruction ins in targetMethod.Body.Instructions)
            {
                if (ins.OpCode.Code != Code.Call && ins.OpCode.Code != Code.Callvirt)
                    continue;

                if (ins.Operand is MethodReference reference
                    && string.Equals(reference.Name, injectMethodName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool TryValidateRedirectSignature(
            MethodDefinition targetMethod,
            MethodDefinition injectMethod,
            out string error)
        {
            error = string.Empty;
            if (!injectMethod.IsStatic)
            {
                error = "Redirect inject must be static.";
                return false;
            }

            if (targetMethod.IsStatic != injectMethod.IsStatic)
            {
                error = "Redirect inject static/instance must match target.";
                return false;
            }

            if (targetMethod.ReturnType.FullName != injectMethod.ReturnType.FullName)
            {
                error = "Redirect inject return type must match target.";
                return false;
            }

            if (targetMethod.Parameters.Count != injectMethod.Parameters.Count)
            {
                error = "Redirect inject parameter count must match target.";
                return false;
            }

            for (int i = 0; i < targetMethod.Parameters.Count; i++)
            {
                if (targetMethod.Parameters[i].ParameterType.FullName != injectMethod.Parameters[i].ParameterType.FullName)
                {
                    error = "Redirect inject parameter types must match target.";
                    return false;
                }
            }

            return true;
        }

        private static void EnsureRedirectBody(MethodDefinition method)
        {
            if (!method.HasBody)
                method.Body = new Mono.Cecil.Cil.MethodBody(method);

            method.ImplAttributes = Mono.Cecil.MethodImplAttributes.IL;
        }

        private static void ApplyRedirectMethodBody(
            ILProcessor il,
            MethodDefinition targetMethod,
            MethodDefinition injectMethod,
            Instruction call)
        {
            targetMethod.Body.Instructions.Clear();
            targetMethod.Body.ExceptionHandlers.Clear();
            targetMethod.Body.Variables.Clear();

            foreach (Instruction load in CreateInjectArgumentLoads(il, targetMethod, injectMethod))
                targetMethod.Body.Instructions.Add(load);

            targetMethod.Body.Instructions.Add(call);
            targetMethod.Body.Instructions.Add(il.Create(OpCodes.Ret));
            targetMethod.Body.InitLocals = injectMethod.Body.InitLocals;
            targetMethod.Body.MaxStackSize = Math.Max(injectMethod.Body.MaxStackSize, (ushort)(targetMethod.Parameters.Count + 8));
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
            bool allowExtern = string.Equals(descriptor.Method, "Redirect", StringComparison.OrdinalIgnoreCase);
            var candidates = targetType.Methods
                .Where(m => m.Name == methodName && (m.HasBody || allowExtern))
                .ToList();
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
            bool skipOriginal,
            AssemblyDefinition gameAsm,
            string gameRoot,
            int patchSlotId,
            int throttleEveryN)
        {
            var loads = CreateInjectArgumentLoads(il, targetMethod, injectMethod);
            Instruction entry = targetMethod.Body.Instructions[0];

            var prefix = new List<Instruction>();
            AppendThrottleGuard(prefix, il, entry, gameAsm, gameRoot, patchSlotId, throttleEveryN);
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
            Instruction call,
            AssemblyDefinition gameAsm,
            string gameRoot,
            int patchSlotId,
            int throttleEveryN)
        {
            var loads = CreateInjectArgumentLoads(il, targetMethod, injectMethod);
            var block = new List<Instruction>();
            if (insertBefore != null)
                AppendThrottleGuard(block, il, insertBefore, gameAsm, gameRoot, patchSlotId, throttleEveryN);
            block.AddRange(loads);
            block.Add(call);

            if (insertBefore != null)
            {
                foreach (Instruction ins in block)
                    il.InsertBefore(insertBefore, ins);
                return;
            }

            foreach (Instruction ins in block)
                il.Append(ins);
        }

        private static int ComputePatchSlotId(PatchEntry entry)
        {
            unchecked
            {
                int hash = entry.ModIdHash;
                hash = (hash * 31) + entry.Descriptor.TargetHash;
                hash = (hash * 31) + entry.Descriptor.InjectHash;
                return hash & 0x7FFFFFFF;
            }
        }

        private static void AppendThrottleGuard(
            List<Instruction> prefix,
            ILProcessor il,
            Instruction skipTarget,
            AssemblyDefinition gameAsm,
            string gameRoot,
            int patchSlotId,
            int throttleEveryN)
        {
#if NOLoader_DEV
            return;
#else
            if (throttleEveryN <= 1)
                return;

            MethodReference? gate = ImportThrottleGate(gameAsm, gameRoot);
            if (gate == null)
                return;

            var throttleOk = il.Create(OpCodes.Nop);
            prefix.Add(il.Create(OpCodes.Ldc_I4, patchSlotId));
            prefix.Add(il.Create(OpCodes.Ldc_I4, throttleEveryN));
            prefix.Add(il.Create(OpCodes.Call, gate));
            prefix.Add(il.Create(OpCodes.Brtrue_S, throttleOk));
            prefix.Add(il.Create(OpCodes.Br_S, skipTarget));
            prefix.Add(throttleOk);
#endif
        }

        private static MethodReference? ImportThrottleGate(AssemblyDefinition gameAsm, string gameRoot)
        {
            const string apiName = "NOLoader.API";
            if (!gameAsm.MainModule.AssemblyReferences.Any(r => string.Equals(r.Name, apiName, StringComparison.Ordinal)))
            {
                gameAsm.MainModule.AssemblyReferences.Add(new AssemblyNameReference(apiName, new Version(0, 1, 0, 0)));
            }

            string apiPath = Path.Combine(gameRoot, "NOLoader", "core", apiName + ".dll");
            if (!File.Exists(apiPath))
                return null;

            var apiAsm = AssemblyDefinition.ReadAssembly(apiPath);
            TypeDefinition? gateType = apiAsm.MainModule.Types
                .FirstOrDefault(t => t.FullName == "NOLoader.API.Runtime.PatchThrottleGate");
            MethodDefinition? method = gateType?.Methods
                .FirstOrDefault(m => m.Name == "ShouldRun" && m.IsStatic && m.Parameters.Count == 2);
            if (method == null)
                return null;

            return gameAsm.MainModule.ImportReference(method);
        }

        private static List<Instruction> CreateInjectArgumentLoads(
            ILProcessor il,
            MethodDefinition targetMethod,
            MethodDefinition injectMethod)
        {
            var loads = new List<Instruction>();
            if (!injectMethod.IsStatic || injectMethod.Parameters.Count == 0)
                return loads;

            int maxTargetSlots = targetMethod.Parameters.Count + (targetMethod.IsStatic ? 0 : 1);
            if (injectMethod.Parameters.Count > maxTargetSlots)
                return loads;

            int nextParamIndex = 0;
            for (int injectIndex = 0; injectIndex < injectMethod.Parameters.Count; injectIndex++)
            {
                ParameterDefinition injectParam = injectMethod.Parameters[injectIndex];
                int ldargIndex;

                if (!targetMethod.IsStatic
                    && nextParamIndex == 0
                    && (IsInstanceParameter(injectParam, targetMethod.DeclaringType)
                        || IsHarmonyObjectInstance(injectParam, injectIndex)))
                {
                    ldargIndex = 0;
                }
                else
                {
                    if (nextParamIndex >= targetMethod.Parameters.Count)
                        return new List<Instruction>();

                    ldargIndex = targetMethod.IsStatic ? nextParamIndex : nextParamIndex + 1;
                    nextParamIndex++;
                }

                AppendParameterLoad(loads, il, ldargIndex, injectParam, targetMethod, injectIndex);
            }

            return loads;
        }

        private static void AppendParameterLoad(
            List<Instruction> loads,
            ILProcessor il,
            int ldargIndex,
            ParameterDefinition injectParam,
            MethodDefinition targetMethod,
            int injectIndex)
        {
            TypeReference injectType = injectParam.ParameterType;
            if (injectType.IsByReference)
            {
                loads.Add(CreateLdarga(il, ldargIndex));
                return;
            }

            loads.Add(CreateLdarg(il, ldargIndex));
            if (injectType.FullName != "System.Object")
                return;

            TypeReference sourceType = GetInjectArgumentSourceType(targetMethod, injectIndex);
            if (sourceType.IsValueType)
                loads.Add(il.Create(OpCodes.Box, sourceType));
        }

        private static bool IsInstanceParameter(ParameterDefinition injectParam, TypeDefinition? declaringType)
        {
            if (declaringType == null)
                return false;

            TypeReference injectType = injectParam.ParameterType;
            if (injectType.IsByReference)
                injectType = ((ByReferenceType)injectType).ElementType;

            return string.Equals(injectType.FullName, declaringType.FullName, StringComparison.Ordinal)
                || string.Equals(injectType.Name, declaringType.Name, StringComparison.Ordinal);
        }

        /// <summary>Harmony Postfix/Prefix(object __instance) on instance methods with no matching formal param.</summary>
        private static bool IsHarmonyObjectInstance(ParameterDefinition injectParam, int injectIndex)
        {
            if (injectIndex != 0)
                return false;

            TypeReference injectType = injectParam.ParameterType;
            return !injectType.IsByReference
                && string.Equals(injectType.FullName, "System.Object", StringComparison.Ordinal);
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

        private static Instruction CreateLdarga(ILProcessor il, int index)
            => il.Create(OpCodes.Ldarga_S, (byte)index);

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
