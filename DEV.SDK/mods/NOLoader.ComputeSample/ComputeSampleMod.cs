using NOLoader.API;

namespace NOLoader.ComputeSample
{
    public sealed class ComputeSampleMod : INOMod, INOModBackgroundWork
    {
        private float _lastApplied;
        private int _computeCount;

        public void OnLoad(ref NOModContext ctx)
        {
            if (NOModRuntime.Scheduler.IsAvailable)
                LoaderLog.Write("[ComputeSample] scheduler available");
            else
                LoaderLog.Write("[ComputeSample] scheduler unavailable — set core_balancer=1");
        }

        public void OnUnload(ref NOModContext ctx)
        {
            LoaderLog.Write("[ComputeSample] runs=" + _computeCount + " last=" + _lastApplied.ToString("F2"));
        }

        public void OnCaptureInputs(ref NOModContext ctx, ref ModWorkInput input)
        {
            input.Param0 = 10000f;
            input.Param1 = _computeCount;
        }

        public void OnCompute(in ModWorkInput input, ref ModWorkOutput output)
        {
            _computeCount++;
            output.Result0 = RunMath((int)input.Param0);
        }

        public void OnApplyResults(ref NOModContext ctx, in ModWorkOutput output)
        {
            _lastApplied = output.Result0;
            if (_computeCount <= 3 || _computeCount % 30 == 0)
                LoaderLog.Write("[ComputeSample] apply sum=" + _lastApplied.ToString("F2") + " runs=" + _computeCount);
        }

        private static float RunMath(int iterations)
        {
            float acc = 0f;
            for (int i = 1; i <= iterations; i++)
                acc += i * 0.0001f;
            return acc;
        }
    }
}
