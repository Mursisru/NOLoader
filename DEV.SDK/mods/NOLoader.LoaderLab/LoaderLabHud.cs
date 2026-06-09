using UnityEngine;

namespace NOLoader.LoaderLab
{
    public sealed class LoaderLabHud : MonoBehaviour
    {
        private static readonly Color BannerFill = new Color(0.95f, 0.15f, 0.55f, 0.92f);
        private static readonly Color BannerGlow = new Color(0.3f, 1f, 0.45f, 1f);

        private void OnGUI()
        {
            if (!LoaderLabState.Active)
                return;

            float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 5f);
            int w = Mathf.Min(720, Screen.width - 20);
            var banner = new Rect((Screen.width - w) * 0.5f, 12, w, 56);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = BannerFill * new Color(1f, 1f, 1f, pulse);
            GUI.Box(banner, GUIContent.none);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = BannerGlow }
            };

            GUI.Label(banner, "NOLOADER HYPER MODE", style);

            var sub = new Rect(banner.x, banner.yMax + 4, banner.width, 22);
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            GUI.Label(sub, "x20 ROF  |  neon tracers  |  x6 rockets  |  x5 blasts", subStyle);

            GUI.backgroundColor = oldBg;
        }
    }
}
