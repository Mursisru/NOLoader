using System.Collections.Generic;
using NOLoader.API;
using NOLoader.API.Manifest;
using NOLoader.Core.Logging;
using NOLoader.Patcher;

namespace NOLoader.Core.Patching
{
    /// <summary>Stage-scoped mod IL patches — MainMenu at bootstrap, Mission on scene ready.</summary>
    public static class ModPatchScheduler
    {
        private static bool _mainMenuPatchesApplied;
        private static bool _missionPatchesApplied;

        public static bool MainMenuPatchesApplied => _mainMenuPatchesApplied;
        public static bool MissionPatchesApplied => _missionPatchesApplied;

        public static void MarkMainMenuApplied() => _mainMenuPatchesApplied = true;

        public static void ApplyThroughStage(string gameRoot, IReadOnlyList<ModManifest> manifests, LoadStage throughStage)
        {
            if (throughStage == LoadStage.Mission)
            {
                if (_missionPatchesApplied)
                    return;
                ApplyInternal(gameRoot, manifests, LoadStage.Mission, LoadStage.Mission);
                _missionPatchesApplied = true;
                return;
            }

            if (_mainMenuPatchesApplied)
                return;

            ApplyInternal(gameRoot, manifests, null, LoadStage.MainMenu);
            _mainMenuPatchesApplied = true;
        }

        private static void ApplyInternal(
            string gameRoot,
            IReadOnlyList<ModManifest> manifests,
            LoadStage? minStage,
            LoadStage maxStage)
        {
            var plan = PatchPlanBuilder.Build(manifests, maxStage, minStage);
            if (plan.Count == 0)
                return;

            byte[]? gameAssembly = AssemblyPatcher.LoadLiveGameAssemblyBytes(gameRoot);
            if (gameAssembly == null)
            {
                RingBufferLog.WriteAscii("[NOLoader] Assembly-CSharp.dll not found for mod patches");
                return;
            }

            var result = AssemblyPatcher.ApplyPatches(gameAssembly, plan, gameRoot);
            foreach (string err in result.Errors)
                RingBufferLog.WriteAscii("[GateL2] " + err);

            if (result.PatchedBytes != null && !result.RolledBack)
            {
                AssemblyPatcher.WriteManagedModuleBytes(gameRoot, "Assembly-CSharp.dll", result.PatchedBytes);
                RingBufferLog.WriteAscii("[NOLoader] Mod patches written to Assembly-CSharp (stage<=" + maxStage + ")");
            }
        }
    }
}
