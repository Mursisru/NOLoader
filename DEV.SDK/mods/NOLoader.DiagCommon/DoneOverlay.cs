using UnityEngine;

namespace NOLoader.DiagCommon
{
    public static class DoneOverlay
    {
        private static GUIStyle? _style;

        public static void Draw(string label = "DONE")
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 96,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }

            float y = Screen.height * 0.5f - 60f;
            var shadow = new GUIStyle(_style) { normal = { textColor = Color.black } };
            GUI.Label(new Rect(2f, y + 2f, Screen.width, 120f), label, shadow);
            GUI.Label(new Rect(0f, y, Screen.width, 120f), label, _style);
        }
    }
}
