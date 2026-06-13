using System.Collections.Generic;
using System.IO;
using NOLoader.API.Manifest;
using NOLoader.Core.Runtime;
using NOLoader.Patcher;

namespace NOLoader.Core.Patching
{
    public static class CoreBootstrapPatches
    {
        public static List<PatchEntry> CreateGameAssemblyPlan(string loaderRoot)
        {
            string coreDir = Path.Combine(loaderRoot, "core");
            var plan = new List<PatchEntry>
            {
                Entry(coreDir, "noloader.bootstrap", "NOLoader.Core.dll",
                    "MainMenu::Init", "NOLoader.Core.Bootstrap::OnMainMenuReady", "Postfix"),
                Entry(coreDir, "noloader.registry", "NOLoader.Registry.dll",
                    "Encyclopedia::AfterLoad", "NOLoader.Registry.RegistryGameBridge::OnEncyclopediaAfterLoad", "Postfix"),
            };

            if (RuntimeConfig.PhysicsCatchMotor)
            {
                plan.Add(Entry(coreDir, "noloader.physics", "NOLoader.Registry.dll",
                    "Missile/Motor::Thrust", "NOLoader.Registry.PhysicsCatchHooks::ThrustPrefix", "Prefix"));
            }

            plan.Add(Entry(coreDir, "noloader.gatel4", "NOLoader.Core.dll",
                "NuclearOption.SceneLoading.MapLoader::CanLoad",
                "NOLoader.Core.Gates.MissionGateHooks::CanLoadPrefixSkip", "PrefixSkip"));

#if !NOLoader_DEV
            plan.AddRange(EngineTweaker.EngineTweakerPatches.CreateGamePlan(coreDir));
            plan.AddRange(GpuRender.GpuRenderPatches.CreateGamePlan(coreDir));
#endif

            return plan;
        }

        public static List<PatchEntry> CreateUnityCorePlan(string loaderRoot)
        {
            string coreDir = Path.Combine(loaderRoot, "core");
            return new List<PatchEntry>
            {
                Entry(coreDir, "noloader.gatel4.scene", "NOLoader.Core.dll",
                    "UnityEngine.SceneManagement.SceneManager::LoadSceneAsync",
                    "NOLoader.Core.Gates.MissionGateHooks::SceneLoadPrefixSkip", "PrefixSkip")
            };
        }

        public static List<PatchEntry> CreateUnityPhysicsPlan(string loaderRoot)
        {
            if (!RuntimeConfig.PhysicsCatchUnity)
                return new List<PatchEntry>();

            string coreDir = Path.Combine(loaderRoot, "core");
            return new List<PatchEntry>
            {
                Entry(coreDir, "noloader.physics.unity", "NOLoader.Registry.dll",
                    "UnityEngine.Rigidbody::AddForce",
                    "NOLoader.Registry.PhysicsCatchHooks::RigidbodyAddForcePrefixSkip", "PrefixSkip"),
                Entry(coreDir, "noloader.physics.unity.single", "NOLoader.Registry.dll",
                    "UnityEngine.Rigidbody::AddForce",
                    "NOLoader.Registry.PhysicsCatchHooks::RigidbodyAddForceSinglePrefixSkip", "PrefixSkip")
            };
        }

        public static List<PatchEntry> CreateUnityUiPlan(string loaderRoot)
        {
#if NOLoader_DEV
            return new List<PatchEntry>();
#else
            string coreDir = Path.Combine(loaderRoot, "core");
            var plan = EngineTweaker.EngineTweakerPatches.CreateUnityUiPlan(coreDir);
            return plan;
#endif
        }

        private static PatchEntry Entry(
            string coreDir,
            string modId,
            string injectAssembly,
            string target,
            string inject,
            string method)
        {
            return new PatchEntry
            {
                ModId = modId,
                ModFolder = coreDir,
                InjectAssembly = injectAssembly,
                Descriptor = new PatchDescriptor
                {
                    Target = target,
                    Inject = inject,
                    Method = method,
                    ExpectedSignatureHash = CoreBootstrapPatchHashes.TryGet(modId)
                }
            };
        }
    }
}
