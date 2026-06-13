using NOLoader.API;

namespace NOLoader.CoreBalancerVerify
{
    internal static class CoreBalancerVerifyLogger
    {
        internal static void Phase(string name) =>
            LoaderLog.Write("[CoreBalVerify] === " + name + " ===");

        internal static void Info(string message) =>
            LoaderLog.Write("[CoreBalVerify] " + message);

        internal static void Pass(string layer, string detail) =>
            LoaderLog.Write("[CoreBalVerify][PASS] " + layer + ": " + detail);

        internal static void Warn(string layer, string detail) =>
            LoaderLog.Write("[CoreBalVerify][WARN] " + layer + ": " + detail);

        internal static void Fail(string layer, string detail) =>
            LoaderLog.Write("[CoreBalVerify][FAIL] " + layer + ": " + detail);
    }
}
