using UnityEngine;
using UnityEngine.UI;
using System;
using System.Reflection;
using System.Collections;
using Mirage.Serialization;
using NOLoader.ModConfig;

namespace NOLoader.AutoFlare
{
    internal sealed class AutoFlareRuntime : MonoBehaviour
    {
        internal static AutoFlareRuntime? Instance;

        private GameObject? _uiObj;
        private Text? _uiText;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        internal bool CountermeasureReady => Instance != null && !GetAircraftCountermeasureTrigger();

        private static bool GetAircraftCountermeasureTrigger()
        {
            try
            {
                var aircraft = SceneSingleton<CombatHUD>.i?.aircraft;
                return aircraft != null && aircraft.countermeasureTrigger;
            }
            catch
            {
                return false;
            }
        }

        private void EnsureUI()
        {
            if (_uiText != null) return;

            var hud = SceneSingleton<FlightHud>.i;
            var parent = hud?.GetHUDCenter() ?? hud?.transform;
            if (parent == null) return;

            _uiObj = new GameObject("AUTO_FLARE_UI");
            _uiObj.transform.SetParent(parent, false);

            _uiText = _uiObj.AddComponent<Text>();
            _uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _uiText.alignment = TextAnchor.MiddleCenter;
            _uiText.color = Color.cyan;
            _uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _uiText.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = _uiText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -170f);
            rt.sizeDelta = new Vector2(420f, 60f);
        }

        internal void SetUI(string text, bool visible) => SetUI(text, visible, Color.cyan);

        internal void SetUI(string text, bool visible, Color color)
        {
            EnsureUI();
            if (_uiText == null) return;
            _uiText.text = text ?? string.Empty;
            _uiText.color = color;
            if (_uiObj != null) _uiObj.SetActive(visible);
        }
    }

    internal static class AutoFlareLogic
    {
    // Настройки (вынесены в конфига плагина)
    private static float _nextBurstTime;
    private static float _triggerReleaseTime;
    private static float _burstEndTime;
    private static int _burstCount;
    private static PersistentID _lastIncomingId = PersistentID.None;
    private static bool _armed;
    private static byte _savedCountermeasureIndex;
    private static bool _hasSavedCountermeasureIndex;
    private static bool _switchedToLtcByAutoSystem;
    private static bool _autoTriggerActive;

    private static float GetMissileEtaToImpactSeconds(Aircraft aircraft, Missile missile, float hitRadius)
    {
        if (aircraft == null || missile == null) return float.NaN;
        if (missile.disabled || aircraft.disabled) return float.NaN;
        if (missile.rb == null || aircraft.rb == null) return float.NaN;

        // Оценка времени до момента, когда ракета окажется в окрестности "hitRadius".
        Vector3 relPos = aircraft.transform.position - missile.transform.position;
        float dist = relPos.magnitude;
        if (dist <= hitRadius) return 0f;

        Vector3 relVel = missile.rb.velocity - aircraft.rb.velocity;
        float closure = Vector3.Dot(relVel, relPos.normalized);
        if (closure <= 1f) return float.NaN;

        float eta = (dist - hitRadius) / closure;
        if (eta < 0f || eta > 120f) return float.NaN;
        return eta;
    }

    private static bool IsIrSeeker(Missile missile)
    {
        if (missile == null) return false;
        try
        {
            // В Missile.cs есть public GetSeekerType()
            string seekerType = missile.GetSeekerType();
            if (string.IsNullOrEmpty(seekerType)) return false;
            var kw = AutoFlareConfigCache.IrSeekerKeyword;
            if (string.IsNullOrEmpty(kw)) return true;
            return seekerType.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasFlaresAvailable(Aircraft aircraft)
    {
        if (aircraft == null || aircraft.countermeasureManager == null) return false;
        object cm = aircraft.countermeasureManager;
        int flareIndex = AutoFlareConfigCache.LtcIndex;

        // 1) Самый вероятный путь: метод с индексом -> ammo
        string[] methodNames = { "GetAmmo", "GetAmmoCount", "GetCountermeasureAmmo", "GetAmmoForIndex", "AmmoForIndex" };
        foreach (string name in methodNames)
        {
            MethodInfo mi = cm.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi == null) continue;
            ParameterInfo[] p = mi.GetParameters();
            try
            {
                object result = null;
                if (p.Length == 1)
                {
                    if (p[0].ParameterType == typeof(int)) result = mi.Invoke(cm, new object[] { flareIndex });
                    else if (p[0].ParameterType == typeof(byte)) result = mi.Invoke(cm, new object[] { (byte)flareIndex });
                }
                else if (p.Length == 0)
                {
                    result = mi.Invoke(cm, null);
                }

                if (result is int i) return i > 0;
                if (result is byte b) return b > 0;
                if (result is float f) return f > 0f;
            }
            catch { }
        }

        // 2) Поля/свойства ammo на самом менеджере (int / массив / list)
        string[] memberNames = { "ammo", "ammoCount", "countermeasureAmmo", "countermeasuresAmmo", "counts" };
        foreach (string member in memberNames)
        {
            try
            {
                var fld = cm.GetType().GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fld != null)
                {
                    object val = fld.GetValue(cm);
                    if (TryReadAmmoValue(val, flareIndex, out int ammoFromField)) return ammoFromField > 0;
                }

                var prop = cm.GetType().GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                {
                    object val = prop.GetValue(cm, null);
                    if (TryReadAmmoValue(val, flareIndex, out int ammoFromProp)) return ammoFromProp > 0;
                }
            }
            catch { }
        }

        // Если не удалось определить — не блокируем систему.
        return true;
    }

    private static bool TryReadAmmoValue(object value, int flareIndex, out int ammo)
    {
        ammo = 0;
        if (value == null) return false;

        if (value is int i) { ammo = i; return true; }
        if (value is byte b) { ammo = b; return true; }
        if (value is float f) { ammo = Mathf.RoundToInt(f); return true; }

        if (value is Array arr)
        {
            if (arr.Length <= 0) return false;
            int idx = Mathf.Clamp(flareIndex, 0, arr.Length - 1);
            object element = arr.GetValue(idx);
            if (element is int ei) { ammo = ei; return true; }
            if (element is byte eb) { ammo = eb; return true; }
            if (element is float ef) { ammo = Mathf.RoundToInt(ef); return true; }
            return false;
        }

        if (value is IList list)
        {
            if (list.Count <= 0) return false;
            int idx = Mathf.Clamp(flareIndex, 0, list.Count - 1);
            object element = list[idx];
            if (element is int li) { ammo = li; return true; }
            if (element is byte lb) { ammo = lb; return true; }
            if (element is float lf) { ammo = Mathf.RoundToInt(lf); return true; }
            return false;
        }

        return false;
    }

    public static void Postfix(Aircraft __instance)
    {
        if (__instance == null) return;
        if (AutoFlareRuntime.Instance == null) return;

        if (!__instance.LocalSim) return;

        // Рабочая проверка на "это локальный игрок" через HUD
        var combatHud = SceneSingleton<CombatHUD>.i;
        if (combatHud == null || combatHud.aircraft != __instance) return;

        byte currentCmIndex = (byte)((__instance.countermeasureManager != null) ? __instance.countermeasureManager.activeIndex : AutoFlareConfigCache.LtcIndex);

        if (!AutoFlareConfigCache.Enabled) { ResetState(false); AutoFlareRuntime.Instance.SetUI("AUTO-FLARE: disabled", false); return; }

        // Если ЛТЦ закончились — система деактивируется и не вмешивается.
        if (!HasFlaresAvailable(__instance))
        {
            StopAutoControl(__instance, currentCmIndex);
            RestoreCountermeasureIfNeeded(__instance);
            ResetState(false);
            AutoFlareRuntime.Instance.SetUI("AF-SYS OFF", true, Color.gray);
            return;
        }

        var warning = __instance.GetMissileWarningSystem();
        if (warning == null)
        {
            ResetState(false);
            AutoFlareRuntime.Instance.SetUI("AUTO-FLARE: no warning system", false);
            return;
        }

        if (!warning.TryGetNearestIncoming(out var incoming))
        {
            StopAutoControl(__instance, currentCmIndex);
            RestoreCountermeasureIfNeeded(__instance);
            ResetState(false);
            AutoFlareRuntime.Instance.SetUI("AUTO-FLARE: waiting...", false);
            return;
        }

        if (incoming == null || incoming.disabled)
        {
            StopAutoControl(__instance, currentCmIndex);
            RestoreCountermeasureIfNeeded(__instance);
            ResetState(false);
            AutoFlareRuntime.Instance.SetUI("AUTO-FLARE: waiting...", false);
            return;
        }

        bool isIrThreat = IsIrSeeker(incoming);
        bool isRadarThreat = !isIrThreat;

        // Пока нет ИК угрозы — система не вмешивается в контрмеры вообще.
        if (!isIrThreat)
        {
            StopAutoControl(__instance, currentCmIndex);
            RestoreCountermeasureIfNeeded(__instance);
            ResetState(false);
            if (isRadarThreat && Vector3.Distance(__instance.transform.position, incoming.transform.position) <= 6000f)
            {
                AutoFlareRuntime.Instance.SetUI("AF-SYS HOLD", true, Color.red);
            }
            else
            {
                AutoFlareRuntime.Instance.SetUI("AF-SYS ON", true);
            }
            return;
        }

        // Если игрок вручную ушел с ЛТЦ (например, на джаммер) во время авто-цикла,
        // считаем это ручным приоритетом и мгновенно отключаем авто-вмешательство.
        if (_switchedToLtcByAutoSystem && __instance.countermeasureManager != null &&
            __instance.countermeasureManager.activeIndex != AutoFlareConfigCache.LtcIndex)
        {
            StopAutoControl(__instance, currentCmIndex);
            _burstEndTime = 0f;
            _triggerReleaseTime = 0f;
            _nextBurstTime = 0f;
            _armed = false;
            _switchedToLtcByAutoSystem = false;
            _hasSavedCountermeasureIndex = false;
            AutoFlareRuntime.Instance.SetUI("AF-SYS HOLD", true, Color.red);
            return;
        }

        // Сбрасываем "кнопку" контрмер, только если её активировал именно авто-мод.
        if (_autoTriggerActive && __instance.countermeasureTrigger && Time.time >= _triggerReleaseTime)
        {
            __instance.Countermeasures(false, currentCmIndex);
            _autoTriggerActive = false;
        }

        // Во время активного окна ИК-залпа удерживаем триггер каждый тик.
        if (Time.time < _burstEndTime)
        {
            __instance.Countermeasures(true, AutoFlareConfigCache.LtcIndex);
            _triggerReleaseTime = _burstEndTime;
            _autoTriggerActive = true;
        }

        // Новый входящий - перезапуск очереди выпуска
        var incomingId = incoming.persistentID;
        if (_lastIncomingId != incomingId)
        {
            _lastIncomingId = incomingId;
            _burstCount = 0;
            _nextBurstTime = 0f;
            _armed = true;
        }

        float eta = GetMissileEtaToImpactSeconds(__instance, incoming, AutoFlareConfigCache.HitRadius);
        float distance = Vector3.Distance(__instance.transform.position, incoming.transform.position);
        float missileSpeed = incoming.speed;
        float mySpeed = __instance.speed;
        float myAlt = __instance.radarAlt;
        float turnRateDeg = (__instance.rb != null) ? __instance.rb.angularVelocity.magnitude * 57.29578f : 0f;

        bool hasCountermeasureManager = __instance.countermeasureManager != null;
        bool ltcSelected = hasCountermeasureManager && __instance.countermeasureManager.activeIndex == AutoFlareConfigCache.LtcIndex;
        bool radarInGateRange = isRadarThreat && distance <= 6000f;
        bool radarAllowed = !isRadarThreat || (radarInGateRange && ltcSelected);

        bool canRelease = AutoFlareRuntime.Instance!.CountermeasureReady && eta > 0f && radarAllowed;

        if (!_armed || float.IsNaN(eta))
        {
            AutoFlareRuntime.Instance.SetUI("AF-SYS ON", true);
            return;
        }

        // Дистанционный режим по ТЗ:
        // 5-2 км: залп 0.5с, каждые 1.0с
        // 2-0 км: залп 1.0с, каждые 0.5с
        bool inFarBand = distance <= 5000f && distance > 2000f;
        bool inNearBand = distance <= 2000f && distance > 0f;
        bool inReleaseWindow = inFarBand || inNearBand;
        float burstDuration = inNearBand ? 1.0f : 0.5f;
        float burstInterval = inNearBand ? 0.5f : 1.0f;
        string bandName = inNearBand ? "2-0km" : (inFarBand ? "5-2km" : "out");

        string stateLine = "AF-SYS ON";
        bool timeOk = Time.time >= _nextBurstTime;

        // Логика по ТЗ:
        // - ИК угроза: разрешено авто-переключение на ЛТЦ (система срабатывает всегда).
        // - Радарная угроза (<=6км): срабатывает только если уже выбран ЛТЦ.
        // - Радарная угроза и ЛТЦ не выбран: не переключаем автоматически.
        if (inReleaseWindow && canRelease && timeOk)
        {
            if (isIrThreat && hasCountermeasureManager && !ltcSelected)
            {
                // Для ИК-угрозы разрешено переключение на ЛТЦ, но запоминаем,
                // что было выбрано до этого (например, джаммер), чтобы вернуть потом.
                _savedCountermeasureIndex = __instance.countermeasureManager.activeIndex;
                _hasSavedCountermeasureIndex = true;
                _switchedToLtcByAutoSystem = true;
            }

            // Старт окна залпа (hold): удерживаем true в течение burstDuration.
            __instance.Countermeasures(true, AutoFlareConfigCache.LtcIndex);
            _burstEndTime = Time.time + burstDuration;
            _triggerReleaseTime = _burstEndTime;
            _autoTriggerActive = true;
            _burstCount++;

            AutoFlareRuntime.Instance.SetUI(stateLine, true);
            _nextBurstTime = Time.time + burstInterval;
            return;
        }

        if (isRadarThreat && radarInGateRange && !ltcSelected)
        {
            AutoFlareRuntime.Instance.SetUI("AF-SYS HOLD", true, Color.red);
            return;
        }

        // Если ракета вышла из рабочих дистанций — снимаем armed.
        if (!inReleaseWindow || eta <= 0.2f)
        {
            StopAutoControl(__instance, currentCmIndex);
            _armed = false;
            RestoreCountermeasureIfNeeded(__instance);
        }

        AutoFlareRuntime.Instance.SetUI(stateLine, true);
    }

    private static void ResetState(bool setUiHidden)
    {
        _armed = false;
        _burstCount = 0;
        _nextBurstTime = 0f;
        _triggerReleaseTime = 0f;
        _burstEndTime = 0f;
        _autoTriggerActive = false;
        _switchedToLtcByAutoSystem = false;
        _hasSavedCountermeasureIndex = false;
        _savedCountermeasureIndex = 0;
        if (!setUiHidden && AutoFlareRuntime.Instance != null)
            AutoFlareRuntime.Instance.SetUI("", false);
    }

    private static void StopAutoControl(Aircraft aircraft, byte currentCmIndex)
    {
        if (aircraft == null) return;
        if (_autoTriggerActive && aircraft.countermeasureTrigger)
        {
            aircraft.Countermeasures(false, currentCmIndex);
        }
        _autoTriggerActive = false;
        _burstEndTime = 0f;
        _triggerReleaseTime = 0f;
    }

    private static void RestoreCountermeasureIfNeeded(Aircraft aircraft)
    {
        if (!_switchedToLtcByAutoSystem || !_hasSavedCountermeasureIndex) return;
        if (aircraft == null || aircraft.countermeasureManager == null) return;

        // Возвращаем выбранный до авто-срабатывания индекс (например, джаммер).
        aircraft.countermeasureManager.activeIndex = _savedCountermeasureIndex;
        _switchedToLtcByAutoSystem = false;
        _hasSavedCountermeasureIndex = false;
    }
    }
}
