#if !NOLoader_DEV
using System.Collections.Generic;
using NOLoader.API;
using UnityEngine.Rendering;

namespace NOLoader.Core.GpuRender
{
    internal sealed class GpuComputeService : INOModGpuRuntime
    {
        public static readonly GpuComputeService Instance = new GpuComputeService();

        private readonly List<RegisteredGpuMod> _mods = new List<RegisteredGpuMod>();

        public void Register(INOModGpuCompute mod, int modIdHash)
        {
            _mods.Add(new RegisteredGpuMod { Mod = mod, ModIdHash = modIdHash });
        }

        public void Unregister(int modIdHash)
        {
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                if (_mods[i].ModIdHash == modIdHash)
                    _mods.RemoveAt(i);
            }
        }

        internal void Dispatch(UnityEngine.Rendering.CommandBuffer cmd, ref NOModContext ctx)
        {
            for (int i = 0; i < _mods.Count; i++)
                _mods[i].Mod.OnDispatchGpu(ref ctx, cmd);
        }

        private sealed class RegisteredGpuMod
        {
            internal INOModGpuCompute Mod = null!;
            internal int ModIdHash;
        }
    }
}
#endif
