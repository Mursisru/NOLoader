using NOLoader.API;

namespace NOLoader.ModOptimizerVerify
{
    internal static class ModOptimizerVerifyLogger
    {
        internal static void Phase(string name) =>
            LoaderLog.Write("[ModOptVerify] phase=" + name);

        internal static void Info(string msg) =>
            LoaderLog.Write("[ModOptVerify] " + msg);

        internal static void Pass(string layer, string detail) =>
            LoaderLog.Write("[ModOptVerify][PASS] " + layer + " " + detail);

        internal static void Warn(string layer, string detail) =>
            LoaderLog.Write("[ModOptVerify][WARN] " + layer + " " + detail);

        internal static void Fail(string layer, string detail) =>
            LoaderLog.Write("[ModOptVerify][FAIL] " + layer + " " + detail);
    }
}
