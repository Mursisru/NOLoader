using System.Collections.Generic;
using NOLoader.HudCommon;
using UnityEngine;

namespace NOLoader.MissileLaunchArcHud
{
    /// <summary>
    /// OUT OF ARC: <see cref="HUDMissileState"/> — <c>maxTargetAngle &gt; minAlignment</c>.
    /// NEZ: <see cref="Missile.CalcRange"/> — same as <see cref="HUDMissileState.CalcWeaponRange"/>.
    /// </summary>
    internal static class LaunchArcGameApi
    {
        private static float _lastRangeCalcTime;
        private static float _cachedMaxRange;
        private static float _cachedNoEscapeRange;

        internal struct LaunchArcSnapshot
        {
            public float MaxTargetAngle;
            public float MinAlignment;
            public bool InArc;
            public float CircleScale;
            public LaunchArcNezPhase NezPhase;
            public float MaxTargetDist;
            public float NoEscapeRange;
            public Unit PrimaryTarget;
            public bool HasTargetHudPosition;
            public int TargetCount;
        }

        internal struct TargetRingPlacement
        {
            public Unit Unit;
            public Vector3 ScreenPosition;
        }

        private static readonly List<TargetRingPlacement> _targetRingScratch = new List<TargetRingPlacement>(16);

        internal static bool TryGetSnapshot(out LaunchArcSnapshot snapshot)
        {
            snapshot = default;

            if (!MissileLaunchArcHudConfigCache.Enabled)
                return false;

            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null)
                return false;

            Aircraft aircraft = combatHud.aircraft;
            if (aircraft == null)
                return false;

            if (!GameManager.flightControlsEnabled)
                return false;

            WeaponStation station = combatHud.GetWeaponStation();
            if (station == null)
                return false;

            WeaponInfo info = station.WeaponInfo;
            if (info == null || !info.missile)
                return false;

            if (info.laserGuided && MissileLaunchArcHudConfigCache.HideForLaserGuided)
                return false;

            if (station.Ammo <= 0)
                return false;

            List<Unit> targets = combatHud.GetTargetList();
            if (targets == null || targets.Count == 0)
                return false;

            float minAlignment = info.targetRequirements.minAlignment;

            GlobalPosition acPos = aircraft.GlobalPosition();
            Vector3 acForward = aircraft.transform.forward;

            float maxTargetAngle = 0f;
            float maxTargetDist = 0f;
            float maxTargetSpeed = 0f;
            bool anyKnown = false;
            Unit farTarget = null;
            Unit primaryTarget = targets[0];
            float farDistSq = 0f;
            GlobalPosition farTargetPos = default;

            foreach (Unit unit in targets)
            {
                if (unit == null)
                    continue;

                GlobalPosition tgtPos;
                if (aircraft.NetworkHQ == null || !aircraft.NetworkHQ.TryGetKnownPosition(unit, out tgtPos))
                    continue;

                anyKnown = true;
                float dist = FastMath.Distance(tgtPos, acPos);
                float distSq = FastMath.SquareDistance(tgtPos, acPos);
                if (distSq >= farDistSq)
                {
                    farDistSq = distSq;
                    farTarget = unit;
                    farTargetPos = tgtPos;
                }

                maxTargetDist = Mathf.Max(maxTargetDist, dist);
                maxTargetSpeed = Mathf.Max(unit.speed, maxTargetSpeed);
                maxTargetAngle = Mathf.Max(maxTargetAngle, Vector3.Angle(tgtPos - acPos, acForward));
            }

            if (MissileLaunchArcHudConfigCache.RequireTargetLock && !anyKnown)
                return false;

            if (farTarget == null || !anyKnown || primaryTarget == null)
                return false;

            var camMgr = SceneSingleton<CameraStateManager>.i;
            Camera cam = camMgr != null ? camMgr.mainCamera : null;
            if (cam == null)
                return false;

            RefreshWeaponRanges(aircraft, info, maxTargetDist, farTargetPos.y, maxTargetSpeed);

            float scale = ComputeCircleScale(cam.fieldOfView, minAlignment);
            bool inArc = maxTargetAngle <= minAlignment;

            bool hasTargetHud = HudScreenPlacement.TryGetPrimaryTargetHudPosition(
                combatHud, primaryTarget, cam, out _);

            float distForNez = maxTargetDist;
            if (LaunchArcVanillaRangeSync.TrySync(ref _cachedMaxRange, ref _cachedNoEscapeRange, out float vanillaDist))
                distForNez = vanillaDist;

            LaunchArcNezPhase nezPhase = LaunchArcNezPhase.Calm;
            if (MissileLaunchArcHudConfigCache.NezMarkersEnabled && IsNezMeaningful())
                nezPhase = EvaluateNezPhase(distForNez);

            snapshot = new LaunchArcSnapshot
            {
                MaxTargetAngle = maxTargetAngle,
                MinAlignment = minAlignment,
                InArc = inArc,
                CircleScale = scale,
                NezPhase = nezPhase,
                MaxTargetDist = maxTargetDist,
                NoEscapeRange = _cachedNoEscapeRange,
                PrimaryTarget = primaryTarget,
                HasTargetHudPosition = hasTargetHud,
                TargetCount = targets.Count,
            };
            return true;
        }

        /// <summary>
        /// When <paramref name="snap"/>.TargetCount &gt; 1: units within launch arc (angle + HUD gate).
        /// </summary>
        internal static IReadOnlyList<TargetRingPlacement> CollectMultiTargetRings(
            LaunchArcSnapshot snap,
            CombatHUD combatHud,
            Camera cam,
            Aircraft aircraft,
            Vector3 hudCenterScreen,
            Dictionary<int, float> holdUntilByUnitId)
        {
            _targetRingScratch.Clear();
            if (combatHud == null || aircraft == null || cam == null || snap.TargetCount <= 1)
                return _targetRingScratch;

            List<Unit> targets = combatHud.GetTargetList();
            if (targets == null || targets.Count <= 1)
                return _targetRingScratch;

            GlobalPosition acPos = aircraft.GlobalPosition();
            Vector3 acForward = aircraft.transform.forward;
            float minAlignment = snap.MinAlignment;
            float holdSec = Mathf.Max(0.05f, MissileLaunchArcHudConfigCache.TargetRingInsideArcHoldSeconds);
            float now = Time.unscaledTime;

            for (int i = 0; i < targets.Count; i++)
            {
                Unit unit = targets[i];
                if (unit == null)
                    continue;

                GlobalPosition tgtPos;
                if (aircraft.NetworkHQ == null || !aircraft.NetworkHQ.TryGetKnownPosition(unit, out tgtPos))
                    continue;

                float angle = Vector3.Angle(tgtPos - acPos, acForward);
                int unitId = unit.GetInstanceID();
                if (angle > minAlignment)
                {
                    holdUntilByUnitId.Remove(unitId);
                    continue;
                }

                if (!HudScreenPlacement.TryGetTargetHudPosition(combatHud, unit, cam, out Vector3 screenPos))
                    continue;

                bool insideScreen = LaunchArcScreenBounds.IsTargetInsideMainArc(
                    snap, hudCenterScreen, screenPos, forTargetRings: true);

                if (insideScreen)
                    holdUntilByUnitId[unitId] = now + holdSec;

                bool show = insideScreen
                    || (holdUntilByUnitId.TryGetValue(unitId, out float until) && now < until);

                if (!show)
                    continue;

                _targetRingScratch.Add(new TargetRingPlacement
                {
                    Unit = unit,
                    ScreenPosition = screenPos,
                });
            }

            return _targetRingScratch;
        }

        private static void RefreshWeaponRanges(
            Aircraft aircraft,
            WeaponInfo info,
            float targetDist,
            float targetAltitude,
            float targetSpeed)
        {
            float now = Time.timeSinceLevelLoad;
            if (now - _lastRangeCalcTime < 1f && _cachedMaxRange > 0f)
                return;

            _lastRangeCalcTime = now;
            _cachedMaxRange = info.targetRequirements.maxRange;
            _cachedNoEscapeRange = _cachedMaxRange;

            Missile prefabMissile = null;
            if (info.weaponPrefab != null)
                prefabMissile = info.weaponPrefab.GetComponent<Missile>();

            if (prefabMissile == null)
                return;

            float launchAlt = aircraft.GlobalPosition().y;
            _cachedMaxRange = prefabMissile.CalcRange(
                aircraft.speed,
                launchAlt,
                targetAltitude,
                targetDist,
                targetSpeed,
                out _cachedNoEscapeRange);
        }

        private static bool IsNezMeaningful()
        {
            return _cachedNoEscapeRange > 0f && _cachedNoEscapeRange < _cachedMaxRange * 0.9f;
        }

        private static LaunchArcNezPhase EvaluateNezPhase(float targetDist)
        {
            float nez = _cachedNoEscapeRange;
            if (targetDist < nez)
                return LaunchArcNezPhase.InsideNez;

            float band = Mathf.Max(100f, MissileLaunchArcHudConfigCache.NezApproachBandMeters);
            if (targetDist <= nez + band)
                return LaunchArcNezPhase.Approaching;

            return LaunchArcNezPhase.Calm;
        }

        internal static float ComputeCircleScale(float cameraFov, float arcDegrees)
        {
            float fov = Mathf.Max(1f, cameraFov);
            float arc = Mathf.Max(0.1f, arcDegrees);
            float baseScale = 50f / fov * (arc / 8f);
            float radiusFix = Mathf.Max(1f, MissileLaunchArcHudConfigCache.ArcRadiusScale);
            return baseScale * radiusFix;
        }

        internal static void ResetRangeCache()
        {
            _lastRangeCalcTime = -999f;
            _cachedMaxRange = 0f;
            _cachedNoEscapeRange = 0f;
            LaunchArcVanillaRangeSync.Reset();
        }
    }
}
