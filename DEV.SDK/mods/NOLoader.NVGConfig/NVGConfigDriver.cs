using UnityEngine;

namespace NOLoader.NVGConfig
{
    /// <summary>Applies NVG color after URP/volume updates (fixes pause-unpause and camera switch timing).</summary>
    internal sealed class NVGConfigDriver : MonoBehaviour
    {
        private static NVGConfigDriver? _instance;

        internal static void Ensure()
        {
            if (_instance != null)
                return;

            var go = new GameObject("NOLoader.NVGConfig.Driver");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<NVGConfigDriver>();
        }

        private void LateUpdate()
        {
            NightVision? nv = NightVision.i;
            if (nv == null)
                return;

            NVGConfigCache.Refresh();
            NightVisionColorLogic.ApplyIfNeeded(nv);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
