using NOLoader.API;

namespace NOLoader.BrokenMod
{
    /// <summary>Gate L2 negative-test stub — never loaded in normal deploy.</summary>
    public sealed class BrokenModEntry : INOMod
    {
        public void OnLoad(ref NOModContext context) { }

        public void OnUnload(ref NOModContext context) { }

        /// <summary>Postfix hook for Gate L2 negative test (wrong hash in mod.json).</summary>
        public static void BrokenHook() { }
    }
}
