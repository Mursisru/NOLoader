using System.Collections.Generic;
using System.Linq;
using NOLoader.API;
using NOLoader.API.Manifest;
using NOLoader.Core.Logging;
using NOLoader.Patcher;

namespace NOLoader.Core.Patching
{
    /// <summary>Rebase Assembly-CSharp mod IL when mods are removed or the patch set changes.</summary>
    internal static class ModPatchReconciler
    {
        internal static bool NeedsReconcile(
            string gameRoot,
            string loaderRoot,
            IReadOnlyList<ModManifest> manifests)
        {
            List<PatchEntry> modPlan = PatchPlanBuilder.Build(manifests, LoadStage.Mission);
            HashSet<string> desiredModMarkers = CollectMarkers(modPlan);
            HashSet<string> coreMarkers = CollectMarkers(CoreBootstrapPatches.CreateGameAssemblyPlan(loaderRoot));

            foreach (string marker in desiredModMarkers)
            {
                if (!PatchStateCache.TryIsPatched(gameRoot, "Assembly-CSharp.dll", marker))
                    return true;
            }

            foreach (string marker in PatchStateCache.GetRecordedMarkers(loaderRoot, "Assembly-CSharp.dll"))
            {
                if (coreMarkers.Contains(marker))
                    continue;
                if (!desiredModMarkers.Contains(marker))
                    return true;
            }

            return false;
        }

        internal static bool TryReconcile(
            string gameRoot,
            string loaderRoot,
            IReadOnlyList<ModManifest> manifests)
        {
            if (!NeedsReconcile(gameRoot, loaderRoot, manifests))
                return false;

            if (!ManagedModuleGuard.TryRestoreVanilla(gameRoot, "Assembly-CSharp.dll"))
            {
                RingBufferLog.WriteAscii("[NOLoader] Mod IL reconcile skipped — vanilla snapshot missing");
                return false;
            }

            byte[]? bytes = AssemblyPatcher.LoadGameAssemblyBytes(gameRoot);
            if (bytes == null)
            {
                RingBufferLog.WriteAscii("[NOLoader] Mod IL reconcile failed — Assembly-CSharp.dll missing");
                return false;
            }

            List<PatchEntry> corePlan = CoreBootstrapPatches.CreateGameAssemblyPlan(loaderRoot);
            PatchSignatureResolver.PopulateMissingCoreHashes(bytes, gameRoot, corePlan);
            PatchResult coreResult = AssemblyPatcher.ApplyPatches(bytes, corePlan, gameRoot);
            foreach (string err in coreResult.Errors)
                RingBufferLog.WriteAscii("[GateL2] " + err);

            if (coreResult.RolledBack || coreResult.PatchedBytes == null)
            {
                RingBufferLog.WriteAscii("[NOLoader] Mod IL reconcile failed — core patch rollback");
                return false;
            }

            byte[] working = coreResult.PatchedBytes;
            List<PatchEntry> modPlan = PatchPlanBuilder.Build(manifests, LoadStage.Mission);
            if (modPlan.Count > 0)
            {
                PatchResult modResult = AssemblyPatcher.ApplyPatches(working, modPlan, gameRoot);
                foreach (string err in modResult.Errors)
                    RingBufferLog.WriteAscii("[GateL2] " + err);

                if (modResult.PatchedBytes != null && !modResult.RolledBack)
                    working = modResult.PatchedBytes;
            }

            AssemblyPatcher.WriteManagedModuleBytes(gameRoot, "Assembly-CSharp.dll", working);

            var allMarkers = new List<string>();
            allMarkers.AddRange(CollectMarkers(corePlan));
            allMarkers.AddRange(CollectMarkers(modPlan));
            PatchStateCache.Record(loaderRoot, "Assembly-CSharp.dll", allMarkers);

            ModPatchScheduler.ResetStageFlags();
            ModPatchScheduler.MarkMainMenuApplied();
            ModPatchScheduler.MarkMissionApplied();

            int modPatchCount = modPlan.Count;
            RingBufferLog.WriteAscii("[NOLoader] Mod IL reconciled on Assembly-CSharp (mod patches=" + modPatchCount + ")");
            return true;
        }

        private static HashSet<string> CollectMarkers(IEnumerable<PatchEntry> plan)
        {
            var markers = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (PatchEntry entry in plan)
            {
                string marker = PatchStateCache.ExtractMarker(entry.Descriptor.Inject);
                if (!string.IsNullOrEmpty(marker))
                    markers.Add(marker);
            }

            return markers;
        }
    }
}
