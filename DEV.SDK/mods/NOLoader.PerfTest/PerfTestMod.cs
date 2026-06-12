using NOLoader.API;
using NOLoader.API.World;
using NOLoader.ModConfig;

namespace NOLoader.PerfTest
{
    public sealed class PerfTestMod : INOMod, INOModTickFast, INOModTickNormal, INOModTickSlow
    {
        public void OnLoad(ref NOModContext ctx)
        {
            PerfTestState.ModIdHash = ctx.ModIdHash;
            PerfTestState.Load(ModIniConfig.Load(ctx.ModRoot));

            INOModWorldReader world = NOModRuntime.ActivateWorld();
            ctx.Services.World = world;

            PerfTestReporter.LogOnLoad(ref ctx, world);
        }

        public void OnUnload(ref NOModContext ctx)
        {
            if (PerfTestState.ReportOnUnload)
                PerfTestReporter.LogSummary(ref ctx);
        }

        public void OnFastUpdate(ref NOModContext ctx, float dt)
        {
            PerfTestState.FastCount++;
            if (PerfTestState.HeavyWork >= 1)
                PerfTestState.Spin(32);
        }

        public void OnNormalUpdate(ref NOModContext ctx, float dt)
        {
            PerfTestState.NormalCount++;

            INOModWorldReader world = ctx.Services.World ?? NOModRuntime.GetWorld();
            int count = world.UnitCount;

            if (PerfTestState.HeavyWork >= 1)
            {
                float[] buf = ctx.Services.Pool.RentFloat(256);
                PerfTestState.PoolRentCount++;
                try
                {
                    for (int i = 0; i < buf.Length; i++)
                        buf[i] = i * 0.001f + count;
                }
                finally
                {
                    ctx.Services.Pool.Return(buf);
                }
            }

            if (PerfTestState.HeavyWork >= 2)
                PerfTestState.Spin(8000);
        }

        public void OnSlowUpdate(ref NOModContext ctx, float dt)
        {
            PerfTestState.SlowCount++;
            if (PerfTestState.HeavyWork >= 1)
                PerfTestState.Spin(128);

            PerfTestReporter.LogPeriodic(ref ctx);
        }
    }
}
