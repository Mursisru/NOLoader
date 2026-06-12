using UnityEngine;

namespace NOLoader.HudCommon
{
    public static class HudDiagLog
    {
        public static bool Enabled { get; set; }
        public static bool Verbose { get; set; }

        public static void Info(string message)
        {
            if (!Enabled)
                return;
            Debug.Log("[HudCommon] " + message);
        }

        public static void Warn(string message)
        {
            if (!Enabled)
                return;
            Debug.LogWarning("[HudCommon] " + message);
        }

        public static void VerboseLine(string message)
        {
            if (!Verbose)
                return;
            Debug.Log("[HudCommon|V] " + message);
        }
    }
}
