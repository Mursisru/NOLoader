using System.Collections.Generic;
using NOLoader.HudCommon;
using UnityEngine;

namespace NOLoader.MissileLaunchArcHud
{
    internal sealed class LaunchArcHudController : MonoBehaviour
    {
        internal static LaunchArcHudController? Instance { get; private set; }
        private LaunchArcCircleView _arcView;
        private NezTargetMarkersView _nezView;
        private MultiTargetRingsView _multiTargetView;
        private float _insideArcHoldUntil;
        private float _nextTick;
        private Transform _cachedCanvas;
        private FlightHud _cachedFlightHud;
        private readonly Dictionary<int, float> _multiTargetHoldUntil = new Dictionary<int, float>();

        private void Start()
        {
            Instance = this;
        }

        internal void RunTick()
        {
            TickCore();
        }

#if !NOLoader_RDYTU
        private void LateUpdate()
        {
            float hz = MissileLaunchArcHudConfigCache.UpdateHz;
            if (hz > 0f)
            {
                float interval = 1f / hz;
                if (Time.unscaledTime < _nextTick)
                    return;
                _nextTick = Time.unscaledTime + interval;
            }

            TickCore();
        }
#endif

        private void TickCore()
        {

            if (!LaunchArcGameApi.TryGetSnapshot(out LaunchArcGameApi.LaunchArcSnapshot snap))
            {
                SetVisible(false);
                return;
            }

            FlightHud fh = SceneSingleton<FlightHud>.i;
            Transform hudCenter = fh == null ? null : fh.GetHUDCenter();
            Transform canvas = GetFlightHudCanvas(fh);
            if (hudCenter == null || canvas == null)
            {
                SetVisible(false);
                return;
            }

            if (!EnsureViews(canvas, hudCenter))
                return;

            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            Camera cam = SceneSingleton<CameraStateManager>.i != null
                ? SceneSingleton<CameraStateManager>.i.mainCamera
                : null;

            Vector3 hudCenterScreen = hudCenter.position;
            Aircraft aircraft = combatHud != null ? combatHud.aircraft : null;

            _arcView.Apply(snap, hudCenter);
            _arcView.SetVisible(true);

            if (snap.TargetCount > 1 && combatHud != null && aircraft != null && cam != null)
            {
                ApplyMultiTargetRings(snap, combatHud, aircraft, cam, hudCenterScreen);
                return;
            }

            ApplySingleTargetNez(snap, combatHud, cam, hudCenterScreen);
        }

        private void ApplyMultiTargetRings(
            LaunchArcGameApi.LaunchArcSnapshot snap,
            CombatHUD combatHud,
            Aircraft aircraft,
            Camera cam,
            Vector3 hudCenterScreen)
        {
            _nezView.SetVisible(false);
            _nezView.Apply(default, default, default, false, false);

            var placements = LaunchArcGameApi.CollectMultiTargetRings(
                snap, combatHud, cam, aircraft, hudCenterScreen, _multiTargetHoldUntil);

            bool show = placements.Count > 0;
            _multiTargetView.SetVisible(show);
            if (show)
                _multiTargetView.Apply(snap, placements);
            else
                _multiTargetView.HideAll();
        }

        private void ApplySingleTargetNez(
            LaunchArcGameApi.LaunchArcSnapshot snap,
            CombatHUD combatHud,
            Camera cam,
            Vector3 hudCenterScreen)
        {
            _multiTargetView.SetVisible(false);
            _multiTargetView.HideAll();

            Vector3 targetPos = default;
            bool hasLiveTarget = combatHud != null
                && snap.PrimaryTarget != null
                && HudScreenPlacement.TryGetTargetHudPosition(
                    combatHud, snap.PrimaryTarget, cam, out targetPos);

            if (hasLiveTarget
                && LaunchArcScreenBounds.IsTargetInsideMainArc(snap, hudCenterScreen, targetPos, forTargetRings: true))
            {
                _insideArcHoldUntil = Time.unscaledTime
                    + Mathf.Max(0.05f, MissileLaunchArcHudConfigCache.TargetRingInsideArcHoldSeconds);
            }

            bool insideMainArc = hasLiveTarget && Time.unscaledTime < _insideArcHoldUntil;

            bool showNez = hasLiveTarget
                && snap.NezPhase != LaunchArcNezPhase.None
                && snap.NezPhase != LaunchArcNezPhase.Calm;

            _nezView.SetVisible(showNez);
            _nezView.Apply(snap, hudCenterScreen, targetPos, hasLiveTarget, insideMainArc);
        }

        private Transform GetFlightHudCanvas(FlightHud fh)
        {
            if (fh == null)
            {
                _cachedFlightHud = null;
                _cachedCanvas = null;
                return null;
            }

            if (_cachedFlightHud != fh || _cachedCanvas == null)
            {
                _cachedFlightHud = fh;
                Canvas c = fh.GetComponent<Canvas>();
                _cachedCanvas = c != null ? c.transform : fh.transform;
            }

            return _cachedCanvas;
        }

        private bool EnsureViews(Transform canvas, Transform hudCenter)
        {
            bool arcOk = _arcView != null && !_arcView.NeedsRebuild(canvas);
            if (!arcOk)
            {
                _arcView?.Dispose();
                _arcView = new LaunchArcCircleView(canvas, hudCenter);
            }

            bool nezOk = _nezView != null && !_nezView.NeedsRebuild(canvas);
            if (!nezOk)
            {
                _nezView?.Dispose();
                _nezView = new NezTargetMarkersView(canvas);
            }

            bool multiOk = _multiTargetView != null && !_multiTargetView.NeedsRebuild(canvas);
            if (!multiOk)
            {
                _multiTargetView?.Dispose();
                _multiTargetView = new MultiTargetRingsView(canvas);
            }

            return _arcView != null && _nezView != null && _multiTargetView != null;
        }

        private void SetVisible(bool visible)
        {
            if (_arcView != null)
                _arcView.SetVisible(visible);

            if (_nezView != null)
            {
                _nezView.SetVisible(false);
                _nezView.Apply(default, default, default, false, false);
            }

            if (_multiTargetView != null)
            {
                _multiTargetView.SetVisible(false);
                _multiTargetView.HideAll();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            LaunchArcGameApi.ResetRangeCache();
            _arcView?.Dispose();
            _nezView?.Dispose();
            _multiTargetView?.Dispose();
            _arcView = null;
            _nezView = null;
            _multiTargetView = null;
            _multiTargetHoldUntil.Clear();
        }
    }
}
