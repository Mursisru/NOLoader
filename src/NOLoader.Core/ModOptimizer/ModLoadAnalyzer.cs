#if !NOLoader_DEV
using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NOLoader.Core.Logging;

namespace NOLoader.Core.ModOptimizer
{
    internal sealed class ModLoadAnalyzerResult
    {
        public int MagicUpdateCount;
        public int FindCallCount;
        public int ReflectionInvokeCount;
        public readonly List<(string Type, string Method)> ReflectionHits = new List<(string, string)>();
        public bool HasTickInterfaces;
        public bool TickClean => MagicUpdateCount == 0 && FindCallCount == 0 && ReflectionInvokeCount == 0;
    }

    internal static class ModLoadAnalyzer
    {
        private const string MonoBehaviourFullName = "UnityEngine.MonoBehaviour";
        private const string GameObjectFindFullName = "UnityEngine.GameObject::Find";

        internal static ModLoadAnalyzerResult Analyze(string modDllPath, bool hasTickInterfaces)
        {
            var result = new ModLoadAnalyzerResult { HasTickInterfaces = hasTickInterfaces };
            if (!ModOptimizerBootstrap.IsTickAnalyzerActive)
                return result;

            try
            {
                using var module = ModuleDefinition.ReadModule(modDllPath);
                foreach (TypeDefinition type in module.Types)
                    ScanType(type, result);
            }
            catch (Exception ex)
            {
                RingBufferLog.WriteAscii("[ModOpt][WARN] analyzer failed path=" + modDllPath + " err=" + ex.Message);
            }

            return result;
        }

        private static void ScanType(TypeDefinition type, ModLoadAnalyzerResult result)
        {
            if (type.HasNestedTypes)
            {
                foreach (TypeDefinition nested in type.NestedTypes)
                    ScanType(nested, result);
            }

            if (InheritsMonoBehaviour(type))
            {
                if (HasNonTrivialMethod(type, "Update"))
                    result.MagicUpdateCount++;
                if (HasNonTrivialMethod(type, "LateUpdate"))
                    result.MagicUpdateCount++;
                if (HasNonTrivialMethod(type, "FixedUpdate"))
                    result.MagicUpdateCount++;
            }

            foreach (MethodDefinition method in type.Methods)
                ScanMethodBody(method, result);
        }

        private static bool InheritsMonoBehaviour(TypeDefinition type)
        {
            TypeReference? baseType = type.BaseType;
            while (baseType != null)
            {
                if (string.Equals(baseType.FullName, MonoBehaviourFullName, StringComparison.Ordinal))
                    return true;

                try
                {
                    TypeDefinition? resolved = baseType.Resolve();
                    baseType = resolved?.BaseType;
                }
                catch
                {
                    break;
                }
            }

            return false;
        }

        private static bool HasNonTrivialMethod(TypeDefinition type, string name)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                    continue;

                if (!method.HasBody)
                    continue;

                if (method.Body.Instructions.Count > 1)
                    return true;

                if (method.Body.Instructions.Count == 1
                    && method.Body.Instructions[0].OpCode != OpCodes.Ret)
                    return true;
            }

            return false;
        }

        private static void ScanMethodBody(MethodDefinition method, ModLoadAnalyzerResult result)
        {
            if (!method.HasBody)
                return;

            bool sawGetMethod = false;
            string? getMethodType = null;
            string? getMethodName = null;

            foreach (Instruction ins in method.Body.Instructions)
            {
                if (ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt)
                {
                    if (ins.Operand is MethodReference called)
                    {
                        string full = called.DeclaringType?.FullName + "::" + called.Name;
                        if (string.Equals(full, GameObjectFindFullName, StringComparison.Ordinal)
                            || (called.DeclaringType?.FullName == "UnityEngine.GameObject"
                                && called.Name == "Find"))
                            result.FindCallCount++;

                        if (called.DeclaringType?.FullName == "System.Type" && called.Name == "GetMethod")
                        {
                            sawGetMethod = true;
                            getMethodType = method.DeclaringType.FullName;
                            getMethodName = method.Name;
                        }

                        if (sawGetMethod
                            && called.DeclaringType?.FullName == "System.Reflection.MethodInfo"
                            && called.Name == "Invoke")
                        {
                            result.ReflectionInvokeCount++;
                            if (!string.IsNullOrEmpty(getMethodType) && !string.IsNullOrEmpty(getMethodName))
                                result.ReflectionHits.Add((getMethodType!, getMethodName!));
                            sawGetMethod = false;
                        }
                    }
                }
            }
        }
    }
}
#endif
