namespace NOLoader.API
{
    /// <summary>Opt-in GPU compute dispatch for visual mods (main thread only).</summary>
    public interface INOModGpuCompute
    {
        /// <param name="commandBuffer">UnityEngine.Rendering.CommandBuffer on main thread.</param>
        void OnDispatchGpu(ref NOModContext ctx, object commandBuffer);
    }

    /// <summary>Core-bound GPU runtime for mod registration.</summary>
    public interface INOModGpuRuntime
    {
        void Register(INOModGpuCompute mod, int modIdHash);
        void Unregister(int modIdHash);
    }

    internal sealed class NOModGpuRuntimeStub : INOModGpuRuntime
    {
        public static readonly NOModGpuRuntimeStub Instance = new NOModGpuRuntimeStub();

        public void Register(INOModGpuCompute mod, int modIdHash) { }

        public void Unregister(int modIdHash) { }
    }
}
