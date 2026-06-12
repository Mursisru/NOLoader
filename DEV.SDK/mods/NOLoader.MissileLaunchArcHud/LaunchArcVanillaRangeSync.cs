using System.Reflection;
using NOLoader.HudCommon;
using UnityEngine;

namespace NOLoader.MissileLaunchArcHud
{
    internal static class LaunchArcVanillaRangeSync
    {
        private static FieldInfo _weaponStateField;
        private static FieldInfo _maxRangeField;
        private static FieldInfo _noEscapeRangeField;
        private static FieldInfo _maxTargetDistField;
        private static float _lastSyncTime = -999f;
        private static float _lastMaxTargetDist;

        internal static bool TrySync(
            ref float cachedMaxRange,
            ref float cachedNoEscapeRange,
            out float maxTargetDist)
        {
            maxTargetDist = _lastMaxTargetDist;
            float now = Time.unscaledTime;
            if (now - _lastSyncTime < 0.5f && cachedMaxRange > 1f)
                return true;

            EnsureFields();
            if (_weaponStateField == null)
                return false;

            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null)
                return false;

            var weaponState = _weaponStateField.GetValue(combatHud) as HUDWeaponState;
            var missileHud = weaponState as HUDMissileState;
            if (missileHud == null)
                return false;

            cachedMaxRange = (float)_maxRangeField.GetValue(missileHud);
            cachedNoEscapeRange = (float)_noEscapeRangeField.GetValue(missileHud);
            maxTargetDist = (float)_maxTargetDistField.GetValue(missileHud);
            _lastMaxTargetDist = maxTargetDist;
            _lastSyncTime = now;
            return cachedMaxRange > 1f;
        }

        internal static void Reset()
        {
            _lastSyncTime = -999f;
            _lastMaxTargetDist = 0f;
        }

        private static void EnsureFields()
        {
            if (_weaponStateField != null)
                return;

            _weaponStateField = GetField(typeof(CombatHUD), "weaponState");
            _maxRangeField = GetField(typeof(HUDMissileState), "maxRange");
            _noEscapeRangeField = GetField(typeof(HUDMissileState), "noEscapeRange");
            _maxTargetDistField = GetField(typeof(HUDMissileState), "maxTargetDist");
        }

        private static FieldInfo GetField(System.Type type, string name) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
