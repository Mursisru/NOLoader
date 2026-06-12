using System.Reflection;
using NOLoader.HudCommon;
using Mirage.Serialization;
using NuclearOption.Networking;
using UnityEngine;

namespace NOLoader.MissileEta
{
    internal enum ArhDisplayPhase
    {
        None,
        Datalink,
        Search,
        ActiveLock,
        TargetLost,
        NoTarget,
    }

    internal static class ArhSeekerApi
    {
        /// <summary>Same threshold as vanilla <see cref="ARHSeeker"/> datalink updates.</summary>
        private const float DatalinkAccuracyMeters = 2000f;

        private static FieldInfo _terminalRangeField;

        internal struct ArhDisplaySample
        {
            internal ArhDisplayPhase Phase;
            internal float DatalinkSeconds;
        }

        internal static bool HasHardTargetLoss(Missile missile, Unit target)
        {
            if (missile == null)
                return true;
            if (!missile.targetID.IsValid)
                return true;
            return target == null || target.disabled;
        }

        /// <summary>
        /// Friendly datalink available for the missile (mirrors ARHSeeker.DatalinkMode HQ checks).
        /// Player lock on the aircraft is separate — this is the missile's NetworkHQ picture.
        /// </summary>
        internal static bool HasFriendlyTargetTrack(Missile missile, Unit target)
        {
            if (missile?.NetworkHQ == null || target == null || target.disabled)
                return false;

            if (target.NetworkHQ == missile.NetworkHQ)
                return true;

            return missile.NetworkHQ.IsTargetBeingTracked(target)
                || missile.NetworkHQ.IsTargetPositionAccurate(target, DatalinkAccuracyMeters);
        }

        /// <summary>
        /// R LOST: no friendly radar track for the missile (datalink dead). Own terminal radar uses SRH/ACT instead.
        /// </summary>
        internal static bool ShouldShowTargetLost(Missile missile, Unit target)
        {
            if (missile == null || missile.GetSeekerType() != "ARH")
                return false;

            if (missile.seekerMode == Missile.SeekerMode.activeLock
                || missile.seekerMode == Missile.SeekerMode.activeSearch)
                return false;

            if (HasHardTargetLoss(missile, target))
                return true;

            return !HasFriendlyTargetTrack(missile, target);
        }

        internal static ArhDisplaySample ComputeDisplay(Missile missile, Unit target)
        {
            var sample = new ArhDisplaySample();
            if (missile == null || missile.disabled)
                return sample;

            if (MissileTargetTracker.WasLaunchedWithoutTarget(missile))
            {
                sample.Phase = ArhDisplayPhase.NoTarget;
                return sample;
            }

            if (missile.GetSeekerType() != "ARH")
                return sample;

            if (missile.seekerMode == Missile.SeekerMode.activeLock)
            {
                sample.Phase = ArhDisplayPhase.ActiveLock;
                return sample;
            }

            if (missile.seekerMode == Missile.SeekerMode.activeSearch)
            {
                sample.Phase = ArhDisplayPhase.Search;
                return sample;
            }

            if (ShouldShowTargetLost(missile, target))
            {
                sample.Phase = ArhDisplayPhase.TargetLost;
                return sample;
            }

            if (target == null)
                return sample;

            if (missile.seekerMode != Missile.SeekerMode.passive)
                return sample;

            float terminalRange = GetTerminalRange(missile);
            if (terminalRange <= 0f)
                return sample;

            Vector3 mPos = missile.GlobalPosition().ToLocalPosition();
            Vector3 tPos = target.GlobalPosition().ToLocalPosition();
            float dist = (tPos - mPos).magnitude;
            if (dist <= terminalRange)
                return sample;

            EtaSample eta = MissileEtaCalculator.ComputeRawEta(target, missile);
            float closure = eta.Valid && eta.Closure > 0f ? eta.Closure : 50f;
            float sec = (dist - terminalRange) / Mathf.Max(closure, MissileEtaConfigCache.MinClosureMps);
            if (sec <= 0f || sec > MissileEtaConfigCache.MaxEtaSeconds)
                return sample;

            sample.Phase = ArhDisplayPhase.Datalink;
            sample.DatalinkSeconds = sec;
            return sample;
        }

        private static float GetTerminalRange(Missile missile)
        {
            if (_terminalRangeField == null)
                _terminalRangeField = typeof(ARHSeeker).GetField("terminalRange", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var seeker = missile.GetComponent<ARHSeeker>();
            if (seeker == null || _terminalRangeField == null)
                return 12000f;

            object value = _terminalRangeField.GetValue(seeker);
            return value is float f ? f : 12000f;
        }
    }
}
