using NOLoader.API;

namespace NOLoader.PerfTest
{
    internal static class PerfTestLogger
    {
        internal static void Phase(string name) => LoaderLog.Write("[PerfTest] === " + name + " ===");

        internal static void Info(string message) => LoaderLog.Write("[PerfTest] " + message);

        internal static void Pass(string layer, string detail) =>
            LoaderLog.Write("[PerfTest][PASS] " + layer + ": " + detail);

        internal static void Warn(string layer, string detail) =>
            LoaderLog.Write("[PerfTest][WARN] " + layer + ": " + detail);

        internal static void Fail(string layer, string detail) =>
            LoaderLog.Write("[PerfTest][FAIL] " + layer + ": " + detail);
    }
}
