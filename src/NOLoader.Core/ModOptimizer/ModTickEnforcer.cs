#if !NOLoader_DEV
using NOLoader.API.Manifest;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime.Perf;

namespace NOLoader.Core.ModOptimizer
{
    internal static class ModTickEnforcer
    {
        internal static void Apply(ModManifest manifest, ModLoadAnalyzerResult analysis)
        {
            if (!ModOptimizerBootstrap.IsTickAnalyzerActive)
                return;

            string modLabel = !string.IsNullOrEmpty(manifest.Id)
                ? manifest.Id
                : manifest.IdHash.ToString("X8");

            if (analysis.TickClean)
            {
                RingBufferLog.WriteAscii("[ModOpt][PASS] tick_clean mod=" + modLabel);
                return;
            }

            RingBufferLog.WriteAscii("[ModOpt][WARN] mod=" + modLabel
                + " magic_update=" + analysis.MagicUpdateCount
                + " find_calls=" + analysis.FindCallCount
                + " reflection_invoke=" + analysis.ReflectionInvokeCount
                + " docs=MOD_AUTHOR.md#rdytu-perf");

            if (analysis.MagicUpdateCount > 0 && analysis.HasTickInterfaces)
            {
                ModExecutionBudget.Instance.ApplyAnalyzerDemotion(manifest.IdHash, 1);
                RingBufferLog.WriteAscii("[ModOpt] demote mod=" + modLabel + " level=1 (redundant tick+Update)");
            }
        }
    }
}
#endif
