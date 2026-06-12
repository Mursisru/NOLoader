using NOLoader.API;
using NOLoader.ModConfig;
using UnityEngine;

namespace NOLoader.AutoFlare
{
    public sealed class AutoFlareMod : INOMod
    {
        private GameObject? _runtime;

        public void OnLoad(ref NOModContext ctx)
        {
            AutoFlareConfigCache.Load(ModIniConfig.Load(ctx.ModRoot));
            _runtime = new GameObject("NOLoader.AutoFlare");
            Object.DontDestroyOnLoad(_runtime);
            _runtime.AddComponent<AutoFlareRuntime>();
#if NOLoader_DEV
            Debug.Log("[NOLoader] AutoFlare loaded");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
            if (_runtime != null)
            {
                Object.Destroy(_runtime);
                _runtime = null;
            }
        }
    }
}
