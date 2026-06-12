#if NOLoader_DEV
using System;
using System.Reflection;
using NOLoader.API;
using NOLoader.API.Manifest;
using NOLoader.Core.Gates;
using NOLoader.Core.Logging;

namespace NOLoader.Core.Mods
{
    /// <summary>DEV — enforce hash-only manifests, ref NOModContext, and struct registry API.</summary>
    public static class ModAuthorContractEnforcer
    {
        public static void Validate(LoadedMod mod)
        {
            ModManifest manifest = mod.Manifest;
            string modLabel = !string.IsNullOrEmpty(manifest.Id)
                ? manifest.Id
                : manifest.IdHash.ToString("X8");

            if (manifest.IdHash == 0)
            {
                GateReportStore.RecordL1("Mod " + modLabel + ": missing idHash (required in DEV)");
            }

            if (string.IsNullOrWhiteSpace(manifest.Guid))
            {
                GateReportStore.RecordL1("Mod " + modLabel + ": missing guid (required in DEV)");
            }

            if (mod.Assembly == null)
                return;

            ValidateInoModRefContract(mod.Assembly, modLabel);
            ValidateRegistryStructs(mod.Assembly, modLabel);
        }

        private static void ValidateInoModRefContract(Assembly assembly, string modLabel)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (!typeof(INOMod).IsAssignableFrom(type) || type.IsAbstract)
                    continue;

                MethodInfo? onLoad = type.GetMethod("OnLoad", BindingFlags.Public | BindingFlags.Instance);
                if (onLoad == null)
                    continue;

                ParameterInfo[] parameters = onLoad.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(NOModContext))
                {
                    GateReportStore.RecordL1("Mod " + modLabel + ": INOMod.OnLoad must take ref NOModContext");
                    continue;
                }

                if (!parameters[0].IsIn)
                {
                    GateReportStore.RecordL1("Mod " + modLabel + ": INOMod.OnLoad must use ref NOModContext (IsIn)");
                }
            }
        }

        private static void ValidateRegistryStructs(Assembly assembly, string modLabel)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.Name.EndsWith("Entry", StringComparison.Ordinal)
                    && type.IsClass
                    && type.Namespace != null
                    && type.Namespace.IndexOf("Registry", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    RingBufferLog.WriteAscii("[NOLoader] Contract warn " + modLabel + ": registry entry type should be struct, not class: " + type.FullName);
                }
            }
        }
    }
}
#endif
