using NOLoader.API;
using NOLoader.API.World;
using NOLoader.Core.Runtime.Balance;

namespace NOLoader.Core.Runtime.Perf
{
    internal static class NOModPerfBootstrap
    {
        public static void Initialize()
        {
            NOModRuntime.Pool = NOModArrayPoolImpl.Instance;
            NOModRuntime.Budget = ModExecutionBudget.Instance;
            NOModRuntime.ActivateWorldCallback = () => WorldSnapshotService.Instance.Activate();
#if !NOLoader_DEV
            CoreBalancerBootstrap.Initialize();
#endif
        }

        public static NOModServices CreateServices(bool requestWorld)
        {
            INOModWorldReader? world = null;
            if (requestWorld)
                world = WorldSnapshotService.Instance.Activate();

            return new NOModServices
            {
                Pool = NOModArrayPoolImpl.Instance,
                World = world,
                Budget = ModExecutionBudget.Instance,
                FrameCache = NOModRuntime.FrameCache
            };
        }

        public static void OnModLoaded(LoadedModEntry entry)
        {
            ModTickScheduler.Register(entry);
            if (ModTickScheduler.HasTickMods)
                ModRuntimeHost.EnsureInstalled();
        }

        public static void OnModUnloaded(LoadedModEntry entry)
        {
            ModTickScheduler.Unregister(entry);
        }
    }
}
