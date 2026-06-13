using NOLoader.API;

namespace NOLoader.GpuRenderVerify
{
    internal static class GpuRenderVerifyLogger
    {
        internal static void Phase(string name) =>
            LoaderLog.Write("[GpuVerify] === " + name + " ===");

        internal static void Info(string message) =>
            LoaderLog.Write("[GpuVerify] " + message);

        internal static void Pass(string layer, string detail) =>
            LoaderLog.Write("[GpuVerify][PASS] " + layer + ": " + detail);

        internal static void Warn(string layer, string detail) =>
            LoaderLog.Write("[GpuVerify][WARN] " + layer + ": " + detail);

        internal static void Fail(string layer, string detail) =>
            LoaderLog.Write("[GpuVerify][FAIL] " + layer + ": " + detail);
    }
}
