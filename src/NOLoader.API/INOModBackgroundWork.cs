namespace NOLoader.API
{
    /// <summary>Opt-in three-phase mod compute: capture (main) → compute (worker) → apply (main).</summary>
    public interface INOModBackgroundWork
    {
        void OnCaptureInputs(ref NOModContext ctx, ref ModWorkInput input);
        void OnCompute(in ModWorkInput input, ref ModWorkOutput output);
        void OnApplyResults(ref NOModContext ctx, in ModWorkOutput output);
    }

    /// <summary>Blittable work packet — extend via Param fields or use RunCompute for custom logic.</summary>
    public struct ModWorkInput
    {
        public int FrameId;
        public float Param0;
        public float Param1;
        public float Param2;
    }

    public struct ModWorkOutput
    {
        public int FrameId;
        public float Result0;
        public float Result1;
    }
}
