using NOLoader.API;
using NOLoader.ModConfig;
using UnityEngine;

namespace NOLoader.MissileHoldCam
{
    public sealed class MissileHoldCamMod : INOMod
    {
        private GameObject? _runner;

        public void OnLoad(ref NOModContext ctx)
        {
            MissileHoldCamConfigCache.Load(ModIniConfig.Load(ctx.ModRoot));
            _runner = new GameObject("NOLoader.MissileHoldCam");
            Object.DontDestroyOnLoad(_runner);
            _runner.AddComponent<MissileHoldCamRunner>();
#if NOLoader_DEV
            Debug.Log("[NOLoader] MissileHoldCam loaded");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
            if (_runner != null)
            {
                Object.Destroy(_runner);
                _runner = null;
            }
        }
    }
}
