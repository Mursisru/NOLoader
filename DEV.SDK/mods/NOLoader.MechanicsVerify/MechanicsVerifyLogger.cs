using NOLoader.API;

namespace NOLoader.MechanicsVerify
{
    internal static class MechanicsVerifyLogger
    {
        internal static void Phase(string name) => LoaderLog.Write("[MechVerify] === " + name + " ===");

        internal static void Info(string message) => LoaderLog.Write("[MechVerify] " + message);

        internal static void Pass(string layer, string detail) =>
            LoaderLog.Write("[MechVerify][PASS] " + layer + ": " + detail);

        internal static void Warn(string layer, string detail) =>
            LoaderLog.Write("[MechVerify][WARN] " + layer + ": " + detail);

        internal static void Fail(string layer, string detail) =>
            LoaderLog.Write("[MechVerify][FAIL] " + layer + ": " + detail);
    }
}
