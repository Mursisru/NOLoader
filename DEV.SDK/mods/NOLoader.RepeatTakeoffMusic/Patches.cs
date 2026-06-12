using UnityEngine;

namespace NOLoader.RepeatTakeoffMusic
{
    internal static class Patches
    {
        private static bool _hasTrackedPid;
        private static PersistentID _trackedPid;
        private static bool _takeoffBoostConsumed;

        internal static void ResetForNewSession()
        {
            _hasTrackedPid = false;
            _takeoffBoostConsumed = false;
        }

        public static void CrossFadeMusicPrefix(
            MusicManager __instance,
            AudioClip audioClip,
            float fadeOutTime,
            float fadeInTime,
            bool repeat,
            ref bool allowReplay,
            bool replacePlaying,
            float priority)
        {
            if (!RepeatTakeoffMusicConfigCache.Enabled)
                return;
            if (audioClip == null)
                return;
            if (allowReplay || !replacePlaying)
                return;

            if (!GameManager.GetLocalAircraft(out var local) || local == null)
                return;

            var pid = local.persistentID;
            if (!_hasTrackedPid || !pid.Equals(_trackedPid))
            {
                _hasTrackedPid = true;
                _trackedPid = pid;
                _takeoffBoostConsumed = false;
            }

            if (_takeoffBoostConsumed)
                return;

            var parameters = local.GetAircraftParameters();
            if (parameters == null || parameters.takeoffMusic != audioClip)
                return;

            allowReplay = true;
            _takeoffBoostConsumed = true;
        }

        public static void SetupGamePostfix()
        {
            ResetForNewSession();
        }
    }
}
