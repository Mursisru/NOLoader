using NOLoader.API;
using UnityEngine;

namespace NOLoader.LoaderLab
{
    public sealed class LoaderLabMod : INOMod
    {
        private static GameObject? _hudHost;

        public void OnLoad(ref NOModContext ctx)
        {
            LoaderLabState.Active = true;
            EnsureHud();
            Debug.Log("[NOLoader] LoaderLab HYPER MODE — x20 guns, neon tracers, x6 missiles, x5 blasts");
        }

        public void OnUnload(ref NOModContext ctx)
        {
            LoaderLabState.Active = false;
            if (_hudHost != null)
            {
                Object.Destroy(_hudHost);
                _hudHost = null;
            }
        }

        private static void EnsureHud()
        {
            if (_hudHost != null)
                return;

            _hudHost = new GameObject("NOLoader.LoaderLab.Hud");
            Object.DontDestroyOnLoad(_hudHost);
            _hudHost.AddComponent<LoaderLabHud>();
        }
    }
}
