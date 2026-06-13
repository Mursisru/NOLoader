using System;
using System.Reflection;
using NOLoader.API;

namespace NOLoader.ModOptimizerVerify
{
    /// <summary>RDYTU field-test mod for DEV4O1 (NOModOptimizer).</summary>
    public sealed class ModOptimizerVerifyMod : INOMod, INOModTickSlow
    {
        public static void ProbePing()
        {
            ModOptimizerVerifyState.ReflectionPingCount++;
        }

        public void OnLoad(ref NOModContext ctx)
        {
            ModOptimizerVerifyReporter.LogOnLoad(ref ctx);
            ModOptimizerVerifyReporter.RunSpawnProbe(ref ctx);
            ModOptimizerVerifyReporter.RunAllProbes(typeof(ModOptimizerVerifyMod).Assembly);
            ModOptimizerVerifyReporter.TryLogOverallPass();
        }

        public void OnUnload(ref NOModContext ctx)
        {
            ModOptimizerVerifyReporter.LogSummary();
        }

        public void OnSlowUpdate(ref NOModContext ctx, float dt)
        {
            ModOptimizerVerifyState.SlowCount++;
            ModOptimizerVerifyReporter.RunAllProbes(typeof(ModOptimizerVerifyMod).Assembly);
            ModOptimizerVerifyReporter.TryLogOverallPass();
            if (ModOptimizerVerifyState.ShouldReport())
                ModOptimizerVerifyReporter.LogPeriodic();
        }
    }
}
