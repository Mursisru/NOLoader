using System.Collections.Generic;
using UnityEngine;

namespace NOLoader.MissileHoldCam
{
    internal static class MissileHoldCamController
    {
        private static readonly List<Missile> OwnedActive = new List<Missile>();

        private static Aircraft _subscribedAircraft;
        private static bool _missileCamActive;
        private static Missile _followedMissile;
        private static Aircraft _savedAircraft;
        private static CameraBaseState _savedState;
        /// <summary>Unscaled time when to restore after missile loss; negative if not waiting.</summary>
        private static float _restoreAfterLossAtUnscaled = -1f;

        internal static void Shutdown()
        {
            ForceRestoreIfNeeded();
            TryUnbindAircraft();
            OwnedActive.Clear();
        }

        internal static void Tick()
        {
            MissileHoldCamConfigCache.Refresh();
            if (!MissileHoldCamConfigCache.Enabled)
            {
                ForceRestoreIfNeeded();
                TryUnbindAircraft();
                return;
            }

            bool hold = IsHoldKeyPressed();
            if (!_missileCamActive && !hold)
            {
                TryBindLocalAircraft();
                return;
            }

            TryBindLocalAircraft();

            var cam = SceneSingleton<CameraStateManager>.i;
            if (cam == null)
                return;

            if (!AllowMissileCam(cam))
            {
                if (_missileCamActive)
                    RestoreCamera(cam);
                return;
            }

            if (_missileCamActive)
            {
                if (MissileLost(cam))
                {
                    float linger = MissileHoldCamConfigCache.PostExplosionHoldSeconds;
                    if (linger <= 0f)
                    {
                        _restoreAfterLossAtUnscaled = -1f;
                        RestoreCamera(cam);
                        return;
                    }

                    if (_restoreAfterLossAtUnscaled < 0f)
                        _restoreAfterLossAtUnscaled = Time.unscaledTime + linger;

                    if (Time.unscaledTime >= _restoreAfterLossAtUnscaled)
                    {
                        _restoreAfterLossAtUnscaled = -1f;
                        RestoreCamera(cam);
                    }

                    return;
                }

                _restoreAfterLossAtUnscaled = -1f;

                if (!hold)
                {
                    RestoreCamera(cam);
                    return;
                }

                return;
            }

            if (!hold)
                return;

            Aircraft local;
            if (!GameManager.GetLocalAircraft(out local))
                return;

            if (!IsSafeCameraState(cam))
                return;

            if (!GameManager.IsLocalAircraft(cam.followingUnit))
                return;

            var missile = PickLatestMissile();
            if (missile == null || missile.disabled || missile.rb == null)
                return;

            _savedState = cam.currentState;
            _savedAircraft = local;
            _followedMissile = missile;
            _missileCamActive = true;
            _restoreAfterLossAtUnscaled = -1f;

            cam.SetFollowingUnit(missile);
        }

        private static bool IsHoldKeyPressed()
        {
            var k = MissileHoldCamConfigCache.HoldKey;
            return k != KeyCode.None && Input.GetKey(k);
        }

        private static void ForceRestoreIfNeeded()
        {
            if (!_missileCamActive)
                return;
            var cam = SceneSingleton<CameraStateManager>.i;
            if (cam != null)
                RestoreCamera(cam);
            else
                ResetState();
        }

        private static bool MissileLost(CameraStateManager cam)
        {
            if (_followedMissile == null || _followedMissile.disabled || _followedMissile.rb == null)
                return true;
            if (cam.followingUnit != _followedMissile)
                return true;
            return false;
        }

        private static void RestoreCamera(CameraStateManager cam)
        {
            if (!_missileCamActive)
                return;

            var aircraft = _savedAircraft;
            var state = _savedState;

            ResetState();

            if (aircraft == null || aircraft.disabled)
                return;

            cam.SetFollowingUnit(aircraft);
            if (state != null)
                cam.SwitchState(state);
        }

        private static void ResetState()
        {
            _missileCamActive = false;
            _followedMissile = null;
            _savedAircraft = null;
            _savedState = null;
            _restoreAfterLossAtUnscaled = -1f;
        }

        private static bool AllowMissileCam(CameraStateManager cam)
        {
            var gs = GameManager.gameState;
            if (gs != GameState.SinglePlayer && gs != GameState.Multiplayer)
                return false;
            if (!GameManager.flightControlsEnabled)
                return false;

            var s = cam.currentState;
            if (s is CameraEncyclopediaState)
                return false;
            if (s is CameraSelectionState)
                return false;
            if (s is CameraTVState)
                return false;
            return true;
        }

        private static bool IsSafeCameraState(CameraStateManager cam)
        {
            var s = cam.currentState;
            return s is CameraCockpitState
                || s is CameraChaseState
                || s is CameraOrbitState
                || s is CameraRelativeState
                || s is CameraControlledState;
        }

        private static Missile PickLatestMissile()
        {
            for (int i = OwnedActive.Count - 1; i >= 0; i--)
            {
                var m = OwnedActive[i];
                if (m == null || m.disabled || m.rb == null)
                {
                    OwnedActive.RemoveAt(i);
                    continue;
                }

                return m;
            }

            return null;
        }

        private static void TryBindLocalAircraft()
        {
            Aircraft a;
            if (!GameManager.GetLocalAircraft(out a))
            {
                if (_missileCamActive)
                {
                    var c = SceneSingleton<CameraStateManager>.i;
                    if (c != null)
                        RestoreCamera(c);
                    else
                        ResetState();
                }
                TryUnbindAircraft();
                OwnedActive.Clear();
                return;
            }

            if (_subscribedAircraft == a)
                return;

            TryUnbindAircraft();
            _subscribedAircraft = a;
            _subscribedAircraft.onRegisterMissile += OnRegisterMissile;
            _subscribedAircraft.onDeregisterMissile += OnDeregisterMissile;
        }

        private static void TryUnbindAircraft()
        {
            if (_subscribedAircraft == null)
                return;
            _subscribedAircraft.onRegisterMissile -= OnRegisterMissile;
            _subscribedAircraft.onDeregisterMissile -= OnDeregisterMissile;
            _subscribedAircraft = null;
        }

        private static void OnRegisterMissile(Missile missile)
        {
            if (missile == null)
                return;
            if (!OwnedActive.Contains(missile))
                OwnedActive.Add(missile);
        }

        private static void OnDeregisterMissile(Missile missile)
        {
            if (missile == null)
                return;
            OwnedActive.Remove(missile);
        }
    }
}
