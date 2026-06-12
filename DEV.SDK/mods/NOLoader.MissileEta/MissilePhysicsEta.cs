using UnityEngine;
using NOLoader.HudCommon;

namespace NOLoader.MissileEta
{
    /// <summary>
    /// Missile flight hints from game API: remaining ΔV, burn time, speed, engine state.
    /// </summary>
    internal struct MissileFlightHints
    {
        internal float SpeedMps;
        internal float RemainingDeltaV;
        internal float RemainingBurnSec;
        internal bool EngineOn;
        internal float TimeSinceSpawn;
        internal float ForwardAlignToTarget;
        internal float VelocityAlignToTarget;
    }

    internal static class MissilePhysicsEta
    {
        internal static MissileFlightHints ReadHints(Missile missile, Vector3 dirToTarget)
        {
            var hints = new MissileFlightHints();
            if (missile == null)
                return hints;

            hints.SpeedMps = missile.speed;
            hints.RemainingDeltaV = missile.GetRemainingDeltaV();
            hints.RemainingBurnSec = missile.GetRemainingBurnTime();
            hints.EngineOn = missile.EngineOn();
            hints.TimeSinceSpawn = missile.timeSinceSpawn;

            Vector3 forward = missile.rb != null && missile.rb.velocity.sqrMagnitude > 1f
                ? missile.rb.velocity.normalized
                : missile.transform.forward;

            hints.ForwardAlignToTarget = Vector3.Dot(missile.transform.forward, dirToTarget);
            hints.VelocityAlignToTarget = Vector3.Dot(forward, dirToTarget);
            return hints;
        }

        internal static float ProjectEffectiveClosure(
            float losClosure,
            MissileFlightHints hints,
            float minClosureMps,
            MissileEtaPhysicsSettings settings)
        {
            if (!settings.UseDeltaVProjection)
                return Mathf.Max(losClosure, minClosureMps);

            float align = Mathf.Max(
                settings.MinAlignDot,
                hints.VelocityAlignToTarget,
                hints.ForwardAlignToTarget * 0.85f);

            float speedClosure = hints.SpeedMps * align;
            float projected = losClosure;

            if (hints.EngineOn && hints.RemainingBurnSec > 0.05f && hints.RemainingDeltaV > 1f)
            {
                float dvClosure = hints.RemainingDeltaV * Mathf.Max(settings.MinAlignDot, hints.ForwardAlignToTarget);
                float burnSec = Mathf.Max(hints.RemainingBurnSec, 0.25f);
                float boostClosure = speedClosure + dvClosure / burnSec;
                projected = Mathf.Max(projected, boostClosure * settings.BoostClosureWeight);
            }

            projected = Mathf.Max(projected, speedClosure * settings.SpeedClosureWeight);

            if (hints.TimeSinceSpawn < settings.LaunchSettleSeconds && hints.EngineOn)
            {
                float launchFloor = hints.SpeedMps * settings.LaunchSpeedAlignWeight
                    + hints.RemainingDeltaV * settings.LaunchDeltaVWeight;
                projected = Mathf.Max(projected, launchFloor);
            }

            return Mathf.Max(projected, minClosureMps);
        }

        internal static bool ShouldUseCpa(MissileFlightHints hints, float losClosure, MissileEtaPhysicsSettings settings)
        {
            if (!settings.UseCpaWhenCoasting)
                return false;
            if (hints.EngineOn && hints.RemainingBurnSec > 0.1f)
                return false;
            if (hints.TimeSinceSpawn < settings.CpaMinAgeSeconds)
                return false;
            return losClosure >= settings.MinClosureMps;
        }
    }

    internal struct MissileEtaPhysicsSettings
    {
        internal bool UseDeltaVProjection;
        internal bool UseCpaWhenCoasting;
        internal float BoostClosureWeight;
        internal float SpeedClosureWeight;
        internal float LaunchSpeedAlignWeight;
        internal float LaunchDeltaVWeight;
        internal float LaunchSettleSeconds;
        internal float CpaMinAgeSeconds;
        internal float MinAlignDot;
        internal float MinClosureMps;
    }
}
