using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NOLoader.Core.Logging;

namespace NOLoader.Core.Patching
{
    internal static class ModIlMethodProbe
    {
        internal static bool TryCountInjectCallsOnDisk(
            string gameRoot,
            string targetTypeName,
            string targetMethodName,
            string injectMethodName,
            out int callCount)
        {
            callCount = 0;
            string path = Path.Combine(gameRoot, "NuclearOption_Data", "Managed", "Assembly-CSharp.dll");
            if (!File.Exists(path))
                return false;

            try
            {
                using var asm = AssemblyDefinition.ReadAssembly(path);
                TypeDefinition? type = FindType(asm.MainModule, targetTypeName);
                MethodDefinition? method = type?.Methods.FirstOrDefault(m =>
                    m.Name == targetMethodName && m.HasBody);
                if (method?.Body == null)
                    return false;

                callCount = CountCallsTo(method, injectMethodName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryCountInjectCallsInMemory(
            Type targetType,
            string targetMethodName,
            out int callCount)
        {
            callCount = 0;
            try
            {
                MethodInfo? method = targetType.GetMethod(
                    targetMethodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                System.Reflection.MethodBody? body = method?.GetMethodBody();
                byte[]? il = body?.GetILAsByteArray();
                if (il == null || il.Length == 0)
                    return false;

                callCount = CountCallOpcodes(il);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void LogProbe(string gameRoot, Type? aircraftType, string injectMethodName)
        {
            if (TryCountInjectCallsOnDisk(gameRoot, "Aircraft", "FixedUpdate", injectMethodName, out int diskCalls))
                RingBufferLog.WriteAscii("[NOLoader] Mod IL probe disk FixedUpdate injectCalls=" + diskCalls);
            else
                RingBufferLog.WriteAscii("[NOLoader] Mod IL probe disk FixedUpdate unreadable");

            if (aircraftType != null
                && TryCountInjectCallsInMemory(aircraftType, "FixedUpdate", out int memCalls))
            {
                RingBufferLog.WriteAscii("[NOLoader] Mod IL probe memory FixedUpdate callOpcodes=" + memCalls);
                if (diskCalls > 0 && memCalls == 0)
                    RingBufferLog.WriteAscii("[GateL2] Mod IL stale in memory — restart game after deploy");
            }
            else
            {
                RingBufferLog.WriteAscii("[NOLoader] Mod IL probe memory FixedUpdate unreadable");
            }
        }

        private static int CountCallsTo(MethodDefinition method, string injectMethodName)
        {
            int count = 0;
            foreach (Instruction ins in method.Body.Instructions)
            {
                if (ins.OpCode.Code != Code.Call && ins.OpCode.Code != Code.Callvirt)
                    continue;
                if (ins.Operand is MethodReference reference
                    && reference.Name.IndexOf(injectMethodName, StringComparison.Ordinal) >= 0)
                    count++;
            }

            return count;
        }

        private static int CountCallOpcodes(byte[] il)
        {
            int count = 0;
            for (int i = 0; i < il.Length; i++)
            {
                if (il[i] == (byte)Code.Call || il[i] == (byte)Code.Callvirt)
                    count++;
            }

            return count;
        }

        private static TypeDefinition? FindType(ModuleDefinition module, string typeName)
        {
            foreach (TypeDefinition type in module.Types)
            {
                TypeDefinition? match = FindTypeRecursive(type, typeName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static TypeDefinition? FindTypeRecursive(TypeDefinition type, string typeName)
        {
            if (string.Equals(type.Name, typeName, StringComparison.Ordinal)
                || string.Equals(type.FullName, typeName, StringComparison.Ordinal))
                return type;

            foreach (TypeDefinition nested in type.NestedTypes)
            {
                TypeDefinition? match = FindTypeRecursive(nested, typeName);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
