using System.IO;
using System.Linq;
using Mono.Cecil;

namespace NOLoader.Patcher
{
    /// <summary>Preserves native GameObject.Find InternalCall before Cecil Redirect replaces it.</summary>
    internal static class NativeFindStubInjector
    {
        internal const string StubTypeFullName = "UnityEngine.NOLoaderNativeFind";
        internal const string StubMethodName = "InvokeNative";

        internal static bool IsGameObjectFindTarget(string typeName, string methodName)
        {
            if (!string.Equals(methodName, "Find", System.StringComparison.Ordinal))
                return false;

            string normalized = typeName.Replace('/', '.');
            return normalized.EndsWith("GameObject", System.StringComparison.Ordinal)
                || normalized.Equals("UnityEngine.GameObject", System.StringComparison.Ordinal);
        }

        internal static bool TryEnsureStub(ModuleDefinition module, MethodDefinition findMethod, string gameRoot)
        {
            if (module.GetType(StubTypeFullName) != null)
                return true;

            if (findMethod.Parameters.Count != 1
                || findMethod.Parameters[0].ParameterType.FullName != "System.String")
                return false;

            MethodDefinition attributeSource = ResolveAttributeSource(findMethod, gameRoot);

            var stubType = new TypeDefinition(
                "UnityEngine",
                "NOLoaderNativeFind",
                TypeAttributes.NotPublic | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass
                    | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed | TypeAttributes.Abstract,
                module.TypeSystem.Object);

            var stubMethod = new MethodDefinition(
                StubMethodName,
                MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig,
                findMethod.ReturnType);
            stubMethod.Parameters.Add(new ParameterDefinition("name", ParameterAttributes.None, module.TypeSystem.String));
            stubMethod.ImplAttributes = MethodImplAttributes.InternalCall;
            CloneCustomAttributes(attributeSource, stubMethod);

            stubType.Methods.Add(stubMethod);
            module.Types.Add(stubType);
            return true;
        }

        private static MethodDefinition ResolveAttributeSource(MethodDefinition findMethod, string gameRoot)
        {
            if (!findMethod.HasBody || findMethod.Body.Instructions.Count == 0)
                return findMethod;

            MethodDefinition? vanilla = TryLoadVanillaFindMethod(gameRoot);
            return vanilla ?? findMethod;
        }

        private static MethodDefinition? TryLoadVanillaFindMethod(string gameRoot)
        {
            const string moduleFile = "UnityEngine.CoreModule.dll";
            string path = ManagedModuleGuard.GetVanillaBackupPath(gameRoot, moduleFile);
            if (!File.Exists(path))
            {
                path = ManagedModuleGuard.GetLivePath(gameRoot, moduleFile) + ManagedModuleGuard.LegacyBackupExtension;
                if (!File.Exists(path))
                    return null;
            }

            try
            {
                using var asm = AssemblyDefinition.ReadAssembly(path);
                TypeDefinition? goType = asm.MainModule.GetType("UnityEngine.GameObject");
                return goType?.Methods.FirstOrDefault(m =>
                    m.Name == "Find"
                    && m.Parameters.Count == 1
                    && m.Parameters[0].ParameterType.FullName == "System.String");
            }
            catch
            {
                return null;
            }
        }

        private static void CloneCustomAttributes(MethodDefinition source, MethodDefinition target)
        {
            foreach (CustomAttribute attr in source.CustomAttributes)
            {
                var copy = new CustomAttribute(attr.Constructor);
                foreach (CustomAttributeArgument arg in attr.ConstructorArguments)
                    copy.ConstructorArguments.Add(arg);
                foreach (CustomAttributeNamedArgument field in attr.Fields)
                    copy.Fields.Add(field);
                foreach (CustomAttributeNamedArgument prop in attr.Properties)
                    copy.Properties.Add(prop);
                target.CustomAttributes.Add(copy);
            }
        }
    }
}
