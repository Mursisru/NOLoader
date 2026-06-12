using System.Collections.Generic;
using NOLoader.HudCommon;
using Mirage.Serialization;
using NuclearOption.Networking;
using UnityEngine;

namespace NOLoader.MissileEta
{
    internal sealed class MissileEtaController : MonoBehaviour
    {
        internal static MissileEtaController? Instance { get; private set; }
        private readonly List<Missile> _ownMissiles = new List<Missile>(16);
        private readonly List<Missile> _incomingMissiles = new List<Missile>(16);
        private readonly List<MissileLabelSlot> _slots = new List<MissileLabelSlot>(16);
        private readonly HashSet<int> _aliveIds = new HashSet<int>();

        private MissileLabelPoolView _labelView;
        private OffScreenArrowView _arrowView;
        private EtaFilterRegistry _filters = new EtaFilterRegistry();
        private float _nextTick;
        private float _nextHeartbeat;
        private string _lastSkipReason = string.Empty;

        private void Start()
        {
            Instance = this;
            MissileEtaDiagLog.Info("Controller Start (LateUpdate loop active)");
        }

        internal void RunTick()
        {
            TickCore();
        }

#if !NOLoader_RDYTU
        private void LateUpdate()
        {
            TickCore();
        }
#endif

        private void TickCore()
        {
            MissileEtaConfigCache.RefreshForTick();
            if (!MissileEtaConfigCache.Enabled)
            {
                LogSkipOnce("master_disabled");
                HideAll();
                return;
            }

            if (!GameManager.flightControlsEnabled)
            {
                LogSkipOnce("flightControlsEnabled=false");
                HideAll();
                return;
            }

            float hz = MissileEtaConfigCache.UpdateHz;
            if (hz > 0f)
            {
                float interval = 1f / hz;
                if (Time.unscaledTime < _nextTick)
                    return;
                _nextTick = Time.unscaledTime + interval;
                MissileEtaDiagLog.VerboseLine($"LateUpdate: tick hz={hz}");
            }

            if (!TryResolveLocalAircraft(out Aircraft aircraft, out string aircraftSource))
            {
                LogSkipOnce("no_local_aircraft");
                HideAll();
                return;
            }

            MissileEtaDiagLog.VerboseLine(
                $"LateUpdate: aircraft={aircraft.persistentID} source={aircraftSource} disabled={aircraft.disabled}");

            EnsureViews();
            if (_labelView == null || _arrowView == null)
            {
                LogSkipOnce($"views_missing label={(_labelView != null)} arrow={(_arrowView != null)}");
                HideAll();
                return;
            }

            Camera cam = SceneSingleton<CameraStateManager>.i != null
                ? SceneSingleton<CameraStateManager>.i.mainCamera
                : null;
            if (cam == null)
            {
                LogSkipOnce("mainCamera=null");
                HideAll();
                return;
            }

            _lastSkipReason = string.Empty;
            BuildSlots(aircraft, cam);
            _filters.Prune(_aliveIds);
            _arrowView.PruneSmoothed(_aliveIds);
            MissileTargetTracker.Prune(_aliveIds);

            int onScreen = 0;
            int offScreen = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Mode == MissileLabelMode.OwnOnScreen || _slots[i].Mode == MissileLabelMode.IncomingOnScreen)
                    onScreen++;
                else if (_slots[i].Mode == MissileLabelMode.OwnOffScreen || _slots[i].Mode == MissileLabelMode.IncomingOffScreen)
                    offScreen++;
            }

            if (_slots.Count > 0)
            {
                MissileEtaDiagLog.Info(
                    $"Apply: slots={_slots.Count} onScreen={onScreen} offScreen={offScreen} " +
                    $"screen={Screen.width}x{Screen.height}");
            }
            else if (_ownMissiles.Count + _incomingMissiles.Count > 0)
            {
                MissileEtaDiagLog.Warn(
                    $"Apply: 0 slots but missiles own={_ownMissiles.Count} incoming={_incomingMissiles.Count} — see verbose skip lines");
            }
            else if (Time.unscaledTime >= _nextHeartbeat)
            {
                _nextHeartbeat = Time.unscaledTime + 3f;
                MissileEtaDiagLog.Info(
                    $"Heartbeat: alive own=0 incoming=0 aircraft={aircraft.persistentID} cam={cam.name} " +
                    $"gameState={GameManager.gameState}");
                MissileDiscovery.LogRegistrySnapshot(aircraft);
            }

            _labelView.Apply(_slots);
            _arrowView.Apply(_slots);
        }

        private void LogSkipOnce(string reason)
        {
            if (_lastSkipReason == reason && !MissileEtaDiagLog.Verbose)
                return;
            _lastSkipReason = reason;
            MissileEtaDiagLog.Info($"LateUpdate SKIP: {reason}");
        }

        private static bool TryResolveLocalAircraft(out Aircraft aircraft, out string source)
        {
            aircraft = null;
            source = "none";
            var combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud != null && combatHud.aircraft != null)
            {
                aircraft = combatHud.aircraft;
                source = "CombatHUD.aircraft";
                return true;
            }

            if (GameManager.GetLocalAircraft(out aircraft) && aircraft != null)
            {
                source = "GameManager.GetLocalAircraft";
                return true;
            }

            return false;
        }

        private void BuildSlots(Aircraft aircraft, Camera cam)
        {
            _slots.Clear();
            _aliveIds.Clear();

            float now = Time.unscaledTime;
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            var filterSettings = MissileEtaConfigCache.Filter;
            float margin = MissileEtaConfigCache.OnScreenMarginPx;
            float edgeMargin = MissileEtaConfigCache.EdgeMarginPx;

            MissileEtaDiagLog.VerboseLine(
                $"BuildSlots: ShowOwn={MissileEtaConfigCache.ShowOwnEta} ShowIncoming={MissileEtaConfigCache.ShowIncoming} " +
                $"MinClosure={filterSettings.MinClosureMps} MaxEta={filterSettings.MaxEtaSeconds}");

            if (MissileEtaConfigCache.ShowOwnEta)
            {
                MissileDiscovery.CollectOwnMissiles(aircraft, _ownMissiles);
                CollectOwnSlots(aircraft, cam, now, dt, filterSettings, margin, edgeMargin);
            }

            if (MissileEtaConfigCache.ShowIncoming)
            {
                MissileWarning warning = aircraft.GetMissileWarningSystem();
                MissileDiscovery.CollectIncomingMissiles(aircraft, warning, _incomingMissiles);
                CollectIncomingSlots(aircraft, cam, now, dt, filterSettings, margin, edgeMargin);
            }

            int max = Mathf.Max(1, MissileEtaConfigCache.MaxLabels);
            if (_slots.Count > max)
            {
                MissileEtaDiagLog.Info($"BuildSlots: trim {_slots.Count} -> {max}");
                _slots.RemoveRange(max, _slots.Count - max);
            }

            MissileEtaDiagLog.VerboseLine($"BuildSlots: done slots={_slots.Count}");
        }

        private void CollectOwnSlots(
            Aircraft aircraft,
            Camera cam,
            float now,
            float dt,
            MissileEtaFilterSettings filterSettings,
            float margin,
            float edgeMargin)
        {
            for (int i = 0; i < _ownMissiles.Count; i++)
            {
                Missile m = _ownMissiles[i];
                string tag = $"own[{i}] id={m?.persistentID} inst={m?.GetInstanceID()}";
                if (m == null || m.disabled)
                {
                    MissileEtaDiagLog.VerboseLine($"{tag} SKIP null_or_disabled");
                    continue;
                }

                int id = m.GetInstanceID();
                _aliveIds.Add(id);
                MissileTargetTracker.Note(m);

                Unit target = ResolveTarget(m);
                bool isArh = m.GetSeekerType() == "ARH";
                bool hardLost = ArhSeekerApi.HasHardTargetLoss(m, target);
                bool noTargetLaunch = MissileTargetTracker.WasLaunchedWithoutTarget(m);

                if (target == null && !(MissileEtaConfigCache.ShowArhCountdown && (noTargetLaunch || (isArh && hardLost))))
                {
                    MissileEtaDiagLog.VerboseLine(
                        $"{tag} SKIP no_target targetID={m.targetID} valid={m.targetID.IsValid}");
                    continue;
                }

                EtaSample raw = default;
                EtaFilterState etaState = _filters.GetEta(id, filterSettings.RawMedianWindow);
                string etaText = null;

                if (target != null)
                {
                    raw = MissileEtaCalculator.ComputeRawEta(target, m);
                    etaState.Update(
                        raw.Valid ? (float?)raw.RawEta : null,
                        raw.Closure,
                        raw.Distance,
                        dt,
                        now,
                        filterSettings);

                    if (!TryFormatEtaText(
                        etaState,
                        raw,
                        MissileEtaConfigCache.OwnDecimalPlaces,
                        filterSettings.DisplayQuantizeStep,
                        out etaText))
                    {
                        MissileEtaDiagLog.VerboseLine(
                            $"{tag} SKIP no_eta_text rawValid={raw.Valid} raw={raw.RawEta:F2} " +
                            $"closure={raw.Closure:F1} dist={raw.Distance:F0} hasDisplayed={etaState.HasDisplayed}");
                        continue;
                    }
                }
                else
                {
                    etaState.Update(null, 0f, 0f, dt, now, filterSettings);
                    if (!etaState.TryGetDisplayText(
                        MissileEtaConfigCache.OwnDecimalPlaces,
                        filterSettings.DisplayQuantizeStep,
                        out etaText))
                        etaText = "—";
                }

                string arhText = null;
                bool showArh = false;
                ArhDisplayPhase arhPhase = ArhDisplayPhase.None;
                if (MissileEtaConfigCache.ShowArhCountdown)
                {
                    string prefix = MissileEtaConfigCache.ArhPrefix ?? "R";
                    ArhSeekerApi.ArhDisplaySample arhDisplay = ArhSeekerApi.ComputeDisplay(m, target);

                    switch (arhDisplay.Phase)
                    {
                        case ArhDisplayPhase.NoTarget:
                            showArh = true;
                            arhPhase = ArhDisplayPhase.NoTarget;
                            arhText = prefix + " NOTR";
                            break;

                        case ArhDisplayPhase.TargetLost:
                            showArh = true;
                            arhPhase = ArhDisplayPhase.TargetLost;
                            arhText = prefix + " LOST";
                            break;

                        case ArhDisplayPhase.Datalink:
                            EtaFilterState arhState = _filters.GetArh(id, filterSettings.RawMedianWindow);
                            arhState.Update(
                                arhDisplay.DatalinkSeconds,
                                raw.Closure,
                                raw.Distance,
                                dt,
                                now,
                                filterSettings);

                            if (arhState.TryGetDisplayText(0, filterSettings.DisplayQuantizeStep, out string arhSec))
                            {
                                showArh = true;
                                arhPhase = ArhDisplayPhase.Datalink;
                                arhText = prefix + arhSec;
                            }
                            break;

                        case ArhDisplayPhase.Search:
                            showArh = true;
                            arhPhase = ArhDisplayPhase.Search;
                            arhText = prefix + " SRH";
                            break;

                        case ArhDisplayPhase.ActiveLock:
                            showArh = true;
                            arhPhase = ArhDisplayPhase.ActiveLock;
                            arhText = prefix + " ACT";
                            break;
                    }
                }

                if (!TryBuildSlot(
                    m,
                    cam,
                    margin,
                    edgeMargin,
                    MissileEtaConfigCache.ShowOwnOffScreenArrows,
                    true,
                    etaText,
                    out MissileLabelSlot slot,
                    tag))
                    continue;

                slot.ArhText = arhText;
                slot.ShowArh = showArh;
                slot.ArhPhase = arhPhase;
                _slots.Add(slot);
                MissileEtaDiagLog.Info($"{tag} SLOT mode={slot.Mode} eta={etaText} pos=({slot.ScreenPosition.x:F0},{slot.ScreenPosition.y:F0})");
            }
        }

        private void CollectIncomingSlots(
            Aircraft aircraft,
            Camera cam,
            float now,
            float dt,
            MissileEtaFilterSettings filterSettings,
            float margin,
            float edgeMargin)
        {
            for (int i = 0; i < _incomingMissiles.Count; i++)
            {
                Missile m = _incomingMissiles[i];
                string tag = $"inc[{i}] id={m?.persistentID} inst={m?.GetInstanceID()}";
                if (m == null || m.disabled)
                {
                    MissileEtaDiagLog.VerboseLine($"{tag} SKIP null_or_disabled");
                    continue;
                }

                int id = m.GetInstanceID();
                _aliveIds.Add(id);

                EtaSample raw = MissileEtaCalculator.ComputeRawEta(aircraft, m);
                EtaFilterState etaState = _filters.GetEta(id, filterSettings.RawMedianWindow);
                etaState.Update(
                    raw.Valid ? (float?)raw.RawEta : null,
                    raw.Closure,
                    raw.Distance,
                    dt,
                    now,
                    filterSettings);

                if (!TryFormatEtaText(
                    etaState,
                    raw,
                    MissileEtaConfigCache.IncomingDecimalPlaces,
                    filterSettings.DisplayQuantizeStep,
                    out string etaText))
                {
                    MissileEtaDiagLog.VerboseLine(
                        $"{tag} SKIP no_eta_text rawValid={raw.Valid} raw={raw.RawEta:F2} " +
                        $"closure={raw.Closure:F1} dist={raw.Distance:F0}");
                    continue;
                }

                if (!TryBuildSlot(
                    m,
                    cam,
                    margin,
                    edgeMargin,
                    MissileEtaConfigCache.ShowOffScreenArrows,
                    false,
                    etaText,
                    out MissileLabelSlot slot,
                    tag))
                    continue;

                _slots.Add(slot);
                MissileEtaDiagLog.Info($"{tag} SLOT mode={slot.Mode} eta={etaText} pos=({slot.ScreenPosition.x:F0},{slot.ScreenPosition.y:F0})");
            }
        }

        private static bool TryBuildSlot(
            Missile missile,
            Camera cam,
            float margin,
            float edgeMargin,
            bool allowOffScreen,
            bool ownMissile,
            string etaText,
            out MissileLabelSlot slot,
            string tag)
        {
            slot = default;
            ScreenPlacement place = HudWorldPlacement.Evaluate(cam, missile.GlobalPosition(), margin, edgeMargin);
            if (!place.Valid)
            {
                MissileEtaDiagLog.VerboseLine($"{tag} SKIP placement invalid");
                return false;
            }

            MissileLabelMode mode;
            if (place.OnScreen)
                mode = ownMissile ? MissileLabelMode.OwnOnScreen : MissileLabelMode.IncomingOnScreen;
            else if (allowOffScreen)
                mode = ownMissile ? MissileLabelMode.OwnOffScreen : MissileLabelMode.IncomingOffScreen;
            else
            {
                MissileEtaDiagLog.VerboseLine($"{tag} SKIP off_screen not allowed onScreen={place.OnScreen}");
                return false;
            }

            slot = new MissileLabelSlot
            {
                Missile = missile,
                Mode = mode,
                ScreenPosition = place.ScreenPosition,
                AngleDeg = place.AngleDeg,
                EtaText = etaText,
            };

            MissileEtaDiagLog.VerboseLine(
                $"{tag} placement onScreen={place.OnScreen} angle={place.AngleDeg:F0} pos=({place.ScreenPosition.x:F0},{place.ScreenPosition.y:F0})");
            return true;
        }

        private static bool TryFormatEtaText(
            EtaFilterState etaState,
            EtaSample raw,
            int decimalPlaces,
            float quantizeStep,
            out string text)
        {
            if (etaState.TryGetDisplayText(decimalPlaces, quantizeStep, out text))
                return true;

            if (!raw.Valid)
                return false;

            text = decimalPlaces <= 0
                ? raw.RawEta.ToString("F0")
                : raw.RawEta.ToString("F1");
            return true;
        }

        private static Unit ResolveTarget(Missile missile)
        {
            if (missile == null || !missile.targetID.IsValid)
                return null;

            if (UnitRegistry.TryGetUnit(new PersistentID?(missile.targetID), out Unit unit))
                return unit;

            return null;
        }

        private void EnsureViews()
        {
            Transform parent = HudScreenPlacement.ResolveHudParent();
            if (parent == null)
                return;

            if (_labelView != null && _labelView.NeedsRebuild(parent))
            {
                MissileEtaDiagLog.Info("EnsureViews: rebuild LabelPool (parent changed)");
                _labelView.Dispose();
                _labelView = null;
            }

            if (_arrowView != null && _arrowView.NeedsRebuild(parent))
            {
                MissileEtaDiagLog.Info("EnsureViews: rebuild ArrowView (parent changed)");
                _arrowView.Dispose();
                _arrowView = null;
            }

            if (_labelView == null)
            {
                _labelView = new MissileLabelPoolView(parent);
                MissileEtaDiagLog.Info($"EnsureViews: created LabelPool under '{parent.name}'");
            }

            if (_arrowView == null)
            {
                _arrowView = new OffScreenArrowView(parent);
                MissileEtaDiagLog.Info($"EnsureViews: created ArrowView under '{parent.name}'");
            }
        }

        private void HideAll()
        {
            _labelView?.HideAll();
            _arrowView?.HideAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            MissileEtaDiagLog.Info("Controller OnDestroy");
            _labelView?.Dispose();
            _arrowView?.Dispose();
            _labelView = null;
            _arrowView = null;
        }
    }
}
