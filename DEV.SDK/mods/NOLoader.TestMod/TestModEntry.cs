using NOLoader.API;
using UnityEngine;

namespace NOLoader.TestMod
{
    public sealed class TestModEntry : INOMod
    {
        public void OnLoad(ref NOModContext ctx)
        {
            Debug.Log("[NOLoader] TestMod loaded OK");
        }

        public void OnUnload(ref NOModContext ctx)
        {
            Debug.Log("[NOLoader] TestMod unloaded");
        }
    }
}
