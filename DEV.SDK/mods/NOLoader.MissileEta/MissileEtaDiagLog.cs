using NOLoader.HudCommon;

namespace NOLoader.MissileEta
{
    internal static class MissileEtaDiagLog
    {
        internal static bool Enabled => MissileEtaConfigCache.DebugLog;
        internal static bool Verbose => Enabled && MissileEtaConfigCache.DebugVerbose;

        internal static void Init()
        {
            HudDiagLog.Enabled = Enabled;
            HudDiagLog.Verbose = Verbose;
        }

        internal static void Info(string message)
        {
            if (!Enabled)
                return;
            HudDiagLog.Info("[MissileEta] " + message);
        }

        internal static void Warn(string message)
        {
            if (!Enabled)
                return;
            HudDiagLog.Warn("[MissileEta] " + message);
        }

        internal static void VerboseLine(string message)
        {
            if (!Verbose)
                return;
            HudDiagLog.VerboseLine("[MissileEta] " + message);
        }
    }
}
