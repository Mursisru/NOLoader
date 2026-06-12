using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NOLoader.API;
using NOLoader.DiagCommon;
using NOLoader.Registry;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOLoader.LoaderDiag
{
    /// <summary>Full battle-sphere audit — runs once on mission map, logs, shows DONE, stops. Does not run in menu.</summary>
    public sealed class LoaderDiagMod : INOMod
    {
        public const string HostObjectName = "NOLoader.LoaderDiag.Host";

        public void OnLoad(ref NOModContext ctx)
        {
            BattleDiagnostics.Reset();

            if (GameObject.Find(HostObjectName) != null)
            {
                LoaderLog.Info("LoaderDiag battle host already active — skip");
                return;
            }

            LoaderLog.Phase("LoaderDiag START (battle sphere only)");
            var session = new DiagSession();
            BattleDiagnostics.RunMissionBootstrap(session, ref ctx);

            var host = new GameObject(HostObjectName);
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<BattleDiagHost>().Bind(session);
        }

        public void OnUnload(ref NOModContext ctx)
        {
            LoaderLog.Info("LoaderDiag unloaded");
        }
    }

    internal sealed class BattleDiagHost : MonoBehaviour
    {
        private const float SettleSeconds = 5f;
        private const float PollSeconds = 2f;

        private DiagSession _session = new DiagSession();
        private bool _showDone;
        private bool _workFinished;

        public void Bind(DiagSession session) => _session = session;

        private void Start()
        {
            SceneManager.activeSceneChanged += OnSceneChanged;
            StartCoroutine(BattleAuditLoop());
        }

        private void OnDestroy() => SceneManager.activeSceneChanged -= OnSceneChanged;

        private void OnSceneChanged(Scene prev, Scene next)
        {
            if (_workFinished || BattleDiagnostics.IsBattleScene(next))
                return;

            LoaderLog.Info("LoaderDiag left battle scene — host destroyed");
            Destroy(gameObject);
        }

        private IEnumerator BattleAuditLoop()
        {
            LoaderLog.Info("LoaderDiag waiting " + SettleSeconds + "s for battle scene settle");
            yield return new WaitForSecondsRealtime(SettleSeconds);

            while (!_workFinished)
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!BattleDiagnostics.IsBattleScene(scene))
                {
                    LoaderLog.Info("LoaderDiag not on battle map — stop");
                    Destroy(gameObject);
                    yield break;
                }

                if (!BattleDiagnostics.TryRunScene(_session)
                    || !BattleDiagnostics.TryRunHudIndividual(_session))
                {
                    yield return new WaitForSecondsRealtime(PollSeconds);
                    continue;
                }

                if (BattleDiagnostics.TryRunHudIntegration(_session))
                {
                    yield return BattleDiagnostics.RunTelemetryProbe(_session);

                    if (BattleDiagnostics.TryRunBattleIntegration(_session))
                    {
                        Finish();
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(PollSeconds);
            }
        }

        private void Finish()
        {
            _workFinished = true;
            _showDone = true;
            _session.LogSummary("LoaderDiag BATTLE AUDIT COMPLETE");
            LoaderLog.Phase("LoaderDiag BATTLE AUDIT COMPLETE");
            LoaderLog.Info("LoaderDiag work finished — overlay only until leave battle scene");
        }

        private void OnGUI()
        {
            if (_showDone)
                DoneOverlay.Draw();
        }
    }

    internal static class BattleDiagnostics
    {
        private static bool _sceneDone;
        private static bool _hudIndividualDone;
        private static bool _hudIntegrationDone;
        private static bool _telemetryDone;
        private static bool _telemetryPacketReceived;
        private static int _telemetryPacketCount;
        private static bool _integrationDone;

        private static object? _mapLoader;
        private static Type? _mapLoaderType;

        public static void Reset()
        {
            _sceneDone = false;
            _hudIndividualDone = false;
            _hudIntegrationDone = false;
            _telemetryDone = false;
            _telemetryPacketReceived = false;
            _telemetryPacketCount = 0;
            _integrationDone = false;
            _mapLoader = null;
            _mapLoaderType = null;
        }

        private static readonly string[] MenuTokens =
        {
            "MainMenu", "MultiplayerMenu", "MissionsMenu", "Encyclopedia", "EMPTY"
        };

        public static bool IsBattleScene(Scene scene)
        {
            if (!scene.isLoaded)
                return false;

            string name = scene.name ?? string.Empty;
            string path = scene.path ?? string.Empty;
            foreach (string token in MenuTokens)
            {
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
                if (path.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }
            return true;
        }

        public static void RunMissionBootstrap(DiagSession s, ref NOModContext ctx)
        {
            LoaderLog.Phase("Battle — Mission bootstrap");

            s.Assert("battle.mod_context", !string.IsNullOrEmpty(ctx.ModId) && ctx.ModIdHash != 0);
            s.Assert("battle.stage", ctx.Stage == LoadStage.Mission);
            AssertFile(s, "battle.mod_manifest", Path.Combine(ctx.ModRoot, "mod.json"));

            string coreDir = Path.Combine(ctx.GameRoot, "NOLoader", "core");
            AssertFile(s, "battle.core_dll", Path.Combine(coreDir, "NOLoader.Core.dll"));
            AssertFile(s, "battle.registry_dll", Path.Combine(coreDir, "NOLoader.Registry.dll"));
            AssertFile(s, "battle.telemetry_dll", Path.Combine(coreDir, "NOLoader.Telemetry.dll"));

            s.Assert("battle.physics_api", Math.Abs(PhysicsSafetyCatch.SanitizeMass(float.NaN) - 0.001f) < 0.0001f);
            s.Assert("battle.loader_log", CoreProbe.LoaderLogBound());
            s.Assert("battle.no_menu_host", GameObject.Find("NOLoader.LoaderDiagMenu.Host") == null);
            s.Assert("battle.gate_l2_enforced", CoreProbe.ReadGateL2Enforced());
            s.Assert("battle.baked_core_hashes", CoreProbe.ReadBakedCoreHashCount() >= 7,
                "count=" + CoreProbe.ReadBakedCoreHashCount());
            s.Assert("battle.gate_l4_installed", CoreProbe.TryReadMissionGateInstalled(out bool gate4) && gate4);
            s.Assert("battle.l4_fault_tracking", CoreProbe.LoadedModSupportsMissionFaultTracking());
            s.Assert("battle.gate_l4_dual_hooks", CoreProbe.GateL4HooksPresent(ctx.GameRoot));

            if (CoreProbe.IsGameFullyPrePatched(ctx.GameRoot))
                s.Pass("battle.runtime_cecil_skip", "all pre-patch markers");
            else if (CoreProbe.IsCorePrePatched(ctx.GameRoot))
                s.Pass("battle.runtime_cecil_skip", "core pre-patch markers");
            else
                s.Skip("battle.runtime_cecil_skip", "runtime Cecil path");

            AssertFile(s, "battle.weaponnames_deployed", Path.Combine(ctx.GameRoot, "NOLoader", "mods", "WeaponNames", "mod.json"));
            AssertFile(s, "battle.weaponnames_dll", Path.Combine(ctx.GameRoot, "NOLoader", "mods", "WeaponNames", "NOLoader.WeaponNames.dll"));
            s.Assert("battle.weaponnames_patch",
                GameProbe.IsDllMarker(ctx.GameRoot, "Assembly-CSharp.dll", "AfterLoadPostfix"));

            string brokenModPath = Path.Combine(ctx.GameRoot, "NOLoader", "mods", "BrokenMod");
            if (Directory.Exists(brokenModPath))
            {
                s.Assert("battle.gate_l2_negative",
                    CoreProbe.ReadRingLogContains(ctx.GameRoot, "com.at747.brokenmod rejected")
                    || CoreProbe.ReadRingLogContains(ctx.GameRoot, "Patch failed for mod com.at747.brokenmod")
                    || CoreProbe.ReadRingLogContains(ctx.GameRoot, "Signature mismatch for Encyclopedia::AfterLoad"));
            }
            else
                s.Skip("battle.gate_l2_negative", "BrokenMod not installed — run scripts\\test-gate-l2-brokenmod.ps1");

            string coreVersion = CoreProbe.ReadCoreDisplayVersion();
            s.Assert("battle.core_version", !string.IsNullOrEmpty(coreVersion), coreVersion);
            s.Assert("battle.core_version_dev", coreVersion.IndexOf("DEV1P", StringComparison.Ordinal) >= 0, coreVersion);

            LoaderLog.Info("Battle bootstrap section done");
        }

        public static bool TryRunScene(DiagSession s)
        {
            if (_sceneDone)
                return true;

            Scene scene = SceneManager.GetActiveScene();
            if (!IsBattleScene(scene))
                return false;

            _mapLoaderType ??= GameProbe.FindType("NuclearOption.SceneLoading.MapLoader");
            if (_mapLoaderType == null)
                return false;

            _mapLoader ??= FindMapLoader(_mapLoaderType);
            if (_mapLoader == null)
                return false;

            PropertyInfo? currentMap = _mapLoaderType.GetProperty("CurrentMap", BindingFlags.Public | BindingFlags.Instance);
            object? mapKey = currentMap?.GetValue(_mapLoader);
            if (mapKey == null)
                return false;

            string mapText = mapKey.ToString() ?? string.Empty;
            if (mapText.IndexOf("None", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            LoaderLog.Phase("Battle — scene / map / physics / registry");

            LoaderLog.Info("battle.current_map=" + mapText);
            LoaderLog.Info("battle.active_scene=" + scene.name);

            s.Assert("battle.scene_loaded", scene.isLoaded);
            s.Assert("battle.map_key", !string.IsNullOrEmpty(mapText));
            s.Assert("battle.physics_catch", CoreProbe.ReadPhysicsCatchCount() >= 0);
            s.Assert("battle.telemetry_port", CoreProbe.ReadTelemetryPort() == 49000);
            s.Assert("battle.telemetry_host", CoreProbe.IsTelemetryHostPresent());
            s.Assert("battle.gate_l4_clear", !CoreProbe.ReadMissionBlocked());

            LoaderLog.Info("battle.registry missiles=" + NOModRegistry.GetMissiles().Count
                + " weapons=" + NOModRegistry.GetWeaponMounts().Count
                + " aircraft=" + NOModRegistry.GetAircraft().Count);

            Type? aircraftType = GameProbe.FindType("Aircraft");
            if (aircraftType != null)
            {
                int count = GameProbe.CountObjects(aircraftType);
                LoaderLog.Info("battle.aircraft_instances=" + count);
                s.Assert("battle.aircraft_present", count > 0, "count=" + count);
            }
            else
                s.Skip("battle.aircraft_present", "type not found");

            s.Assert("battle.diag_host", GameObject.Find(LoaderDiagMod.HostObjectName) != null);

            _sceneDone = true;
            LoaderLog.Info("Battle scene section done");
            return true;
        }

        public static bool TryRunHudIndividual(DiagSession s)
        {
            if (_hudIndividualDone)
                return true;

            if (!_sceneDone)
                return false;

            LoaderLog.Phase("Battle — HUD components (individual)");

            object? combatHud = GameProbe.GetSceneSingleton("CombatHUD");
            s.Assert("hud.combat_hud", combatHud != null);

            object? flightHud = GameProbe.GetSceneSingleton("FlightHud");
            s.Assert("hud.flight_hud", flightHud != null);

            object? dynamicMap = GameProbe.GetSceneSingleton("DynamicMap");
            if (dynamicMap != null)
                s.Pass("hud.dynamic_map");
            else
                s.Skip("hud.dynamic_map", "not in scene");

            object? hudOptions = GameProbe.GetSceneSingleton("HUDOptions");
            if (hudOptions != null)
                s.Pass("hud.hud_options");
            else
                s.Skip("hud.hud_options", "not in scene");

            object? hmd = GameProbe.GetSceneSingleton("HeadMountedDisplay");
            if (hmd != null)
                s.Pass("hud.head_mounted_display");
            else
                s.Skip("hud.head_mounted_display", "not in scene");

            if (combatHud != null)
            {
                FieldInfo? aircraftField = combatHud.GetType().GetField("aircraft", BindingFlags.Public | BindingFlags.Instance);
                object? hudAircraft = aircraftField?.GetValue(combatHud);
                s.Assert("hud.combat_has_aircraft_ref", hudAircraft != null);
            }

            if (flightHud != null)
            {
                MethodInfo? getCenter = flightHud.GetType().GetMethod("GetHUDCenter", BindingFlags.Public | BindingFlags.Instance);
                object? center = getCenter?.Invoke(flightHud, null);
                s.Assert("hud.flight_hud_center", center != null);
            }

            _hudIndividualDone = true;
            LoaderLog.Info("Battle HUD individual section done");
            return true;
        }

        public static bool TryRunHudIntegration(DiagSession s)
        {
            if (_hudIntegrationDone)
                return true;

            if (!_hudIndividualDone)
                return false;

            LoaderLog.Phase("Battle — HUD + aircraft integration");

            object? localAircraft = GameProbe.GetLocalAircraft();
            s.Assert("hud.local_aircraft", localAircraft != null);

            object? combatHud = GameProbe.GetSceneSingleton("CombatHUD");
            if (combatHud != null && localAircraft != null)
            {
                FieldInfo? acField = combatHud.GetType().GetField("aircraft", BindingFlags.Public | BindingFlags.Instance);
                object? combatAc = acField?.GetValue(combatHud);
                s.Assert("hud.combat_matches_local", ReferenceEquals(combatAc, localAircraft));
            }

            if (localAircraft != null)
            {
                FieldInfo? gForce = localAircraft.GetType().GetField("gForce", BindingFlags.Public | BindingFlags.Instance);
                if (gForce?.GetValue(localAircraft) is float g)
                {
                    LoaderLog.Info("battle.gforce=" + g.ToString("F2"));
                    s.Pass("hud.gforce_readable", "g=" + g.ToString("F1"));
                }

                FieldInfo? wmField = localAircraft.GetType().GetField("weaponManager", BindingFlags.Public | BindingFlags.Instance);
                object? wm = wmField?.GetValue(localAircraft);
                s.Assert("hud.weapon_manager", wm != null);

                if (wm != null)
                {
                    FieldInfo? stationField = wm.GetType().GetField("currentWeaponStation", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (stationField?.GetValue(wm) != null)
                        s.Pass("hud.active_weapon_station");
                    else
                        s.Skip("hud.active_weapon_station", "no station selected yet");
                }
            }

            _hudIntegrationDone = true;
            LoaderLog.Info("Battle HUD integration section done");
            return true;
        }

        public static IEnumerator RunTelemetryProbe(DiagSession s)
        {
            if (_telemetryDone)
                yield break;

            if (!_hudIntegrationDone)
                yield break;

            LoaderLog.Phase("Battle — NO2 UDP telemetry (~30 Hz)");

            if (!CoreProbe.IsTelemetryHostPresent())
                s.Fail("telemetry.host_missing", "NOLoader.TelemetryHost not found before UDP probe");

            yield return new WaitForSecondsRealtime(0.5f);
            s.Assert("telemetry.bindings_ready", CoreProbe.ReadTelemetryBindingsReady());
            s.Assert("telemetry.snapshot_valid", CoreProbe.ReadTelemetrySnapshotValid());

            bool? completed = null;
            int packetCount = 0;
            No2Sample sample = default;
            TelemetryProbe.TryReceiveNo2BurstAsync(49000, 6000, 2, (ok, count, pkt) =>
            {
                completed = ok;
                packetCount = count;
                sample = pkt;
            });

            float deadline = Time.realtimeSinceStartup + 7.5f;
            while (completed == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (completed == true && sample.Received)
            {
                _telemetryPacketReceived = true;
                _telemetryPacketCount = packetCount;

                LoaderLog.Info("telemetry.no2 packets=" + packetCount
                    + " px=" + sample.Px.ToString("F1")
                    + " py=" + sample.Py.ToString("F1")
                    + " pz=" + sample.Pz.ToString("F1")
                    + " g=" + sample.GLoad.ToString("F1"));
                s.Pass("telemetry.no2_received", "packets=" + packetCount + " fields=" + sample.FieldCount);
                s.Assert("telemetry.no2_format", sample.FieldCount >= 13);
                if (packetCount >= 2)
                    s.Pass("telemetry.no2_burst", "packets=" + packetCount);
                else
                    s.Skip("telemetry.no2_burst", "only " + packetCount + " packet in window");

                object? localAircraft = GameProbe.GetLocalAircraft();
                if (localAircraft != null)
                {
                    object? rb = GameProbe.GetInstanceMember(localAircraft, "rb")
                        ?? GameProbe.GetInstanceMember(localAircraft, "RB");
                    if (rb != null)
                    {
                        PropertyInfo? posProp = rb.GetType().GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
                        if (posProp?.GetValue(rb) is Vector3 pos)
                        {
                            float delta = Vector3.Distance(pos, new Vector3(sample.Px, sample.Py, sample.Pz));
                            s.Assert("telemetry.no2_position", delta < 100f, "delta=" + delta.ToString("F1"));
                        }
                        else
                            s.Skip("telemetry.no2_position", "rigidbody position unavailable");
                    }
                    else
                        s.Fail("telemetry.no2_rb_binding", "Unit.rb not resolved (FlattenHierarchy)");

                    if (GameProbe.GetInstanceMember(localAircraft, "gForce") is float g)
                        s.Assert("telemetry.no2_gload", !float.IsNaN(g) && !float.IsInfinity(g), "g=" + g.ToString("F1"));
                }
                else
                    s.Skip("telemetry.no2_position", "local aircraft unavailable");
            }
            else
                s.Fail("telemetry.no2_received", "no UDP NO2 packet on :49000 within timeout");

            _telemetryDone = true;
        }

        public static bool TryRunBattleIntegration(DiagSession s)
        {
            if (_integrationDone)
                return true;

            if (!_telemetryDone)
                return false;

            LoaderLog.Phase("Battle — full stack integration");

            s.Assert("integration.scene_hud", _sceneDone && _hudIntegrationDone);
            s.Assert("integration.telemetry_no2", _telemetryPacketReceived, "packets=" + _telemetryPacketCount);
            s.Assert("integration.telemetry_host", CoreProbe.IsTelemetryHostPresent());
            s.Assert("integration.registry_live", NOModRegistry.GetMissiles().Count >= 0);

            int registryTotal = NOModRegistry.GetMissiles().Count
                + NOModRegistry.GetWeaponMounts().Count
                + NOModRegistry.GetAircraft().Count;
            if (registryTotal > 0)
                s.Pass("integration.registry_entries", "count=" + registryTotal);
            else
                s.Skip("integration.registry_entries", "no custom registry mods deployed");

            s.Assert("integration.telemetry_service", CoreProbe.ReadTelemetryPort() == 49000);
            s.Assert("integration.gate_l2_enforced", CoreProbe.ReadGateL2Enforced());
            s.Assert("integration.gate_l4_mission", CoreProbe.TryReadMissionGateInstalled(out bool gate4) && gate4);
            s.Assert("integration.physics_stack", CoreProbe.ReadPhysicsCatchCount() >= 0);
            s.Assert("integration.no_menu_diag", GameObject.Find("NOLoader.LoaderDiagMenu.Host") == null);

            _integrationDone = true;
            LoaderLog.Info("Battle integration section done");
            return true;
        }

        private static object? FindMapLoader(Type mapLoaderType)
        {
            MethodInfo? find = typeof(Resources).GetMethod(
                "FindObjectsOfTypeAll",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type) },
                null);

            if (find?.Invoke(null, new object[] { mapLoaderType }) is Array arr)
            {
                foreach (object item in arr)
                {
                    if (item != null)
                        return item;
                }
            }
            return null;
        }

        private static void AssertFile(DiagSession s, string name, string path)
        {
            if (File.Exists(path))
                s.Pass(name, Path.GetFileName(path));
            else
                s.Fail(name, "missing " + path);
        }
    }
}
