using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NOLoader.API;
using NOLoader.DiagCommon;
using NOLoader.Registry;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOLoader.LoaderDiagMenu
{
    /// <summary>Full menu-sphere audit — runs once, logs, shows DONE, stops work. Does not touch battle scene.</summary>
    public sealed class LoaderDiagMenuMod : INOMod
    {
        public const string HostObjectName = "NOLoader.LoaderDiagMenu.Host";

        public void OnLoad(ref NOModContext ctx)
        {
            MenuDiagnostics.Reset();

            if (GameObject.Find(HostObjectName) != null)
            {
                LoaderLog.Info("LoaderDiagMenu already finished or active — skip");
                return;
            }

            LoaderLog.Phase("LoaderDiagMenu START (menu sphere only)");
            var session = new DiagSession();
            MenuDiagnostics.RunPreMenu(session, ref ctx);

            var host = new GameObject(HostObjectName);
            host.AddComponent<MenuDiagHost>().Bind(session);
        }

        public void OnUnload(ref NOModContext ctx)
        {
            LoaderLog.Info("LoaderDiagMenu unloaded");
        }
    }

    internal sealed class MenuDiagHost : MonoBehaviour
    {
        private DiagSession _session = new DiagSession();
        private bool _showDone;
        private bool _workFinished;

        public void Bind(DiagSession session) => _session = session;

        private void Start() => StartCoroutine(RunMenuAudit());

        private void OnDestroy()
        {
            if (!_workFinished)
                LoaderLog.Info("LoaderDiagMenu host destroyed before audit finished");
        }

        private IEnumerator RunMenuAudit()
        {
            yield return new WaitForSecondsRealtime(2f);

            while (!_workFinished)
            {
                if (!MenuDiagnostics.IsMenuScene(SceneManager.GetActiveScene()))
                {
                    LoaderLog.Info("LoaderDiagMenu left menu scene — abort");
                    Destroy(gameObject);
                    yield break;
                }

                if (MenuDiagnostics.TryRunMainMenu(_session)
                    && MenuDiagnostics.TryRunIntegration(_session))
                {
                    Finish();
                    yield break;
                }

                yield return new WaitForSecondsRealtime(1.5f);
            }
        }

        private void Finish()
        {
            _workFinished = true;
            _showDone = true;
            _session.LogSummary("LoaderDiagMenu MENU AUDIT COMPLETE");
            LoaderLog.Info("LoaderDiagMenu MENU AUDIT COMPLETE");
            LoaderLog.Info("LoaderDiagMenu work finished — overlay only, no battle checks");
            enabled = true;
        }

        private void OnGUI()
        {
            if (_showDone)
                DoneOverlay.Draw();
        }
    }

    internal static class MenuDiagnostics
    {
        private static bool _mainMenuDone;
        private static bool _integrationDone;

        public static void Reset()
        {
            _mainMenuDone = false;
            _integrationDone = false;
        }

        public static bool IsMenuScene(Scene scene)
        {
            string name = scene.name ?? string.Empty;
            string path = scene.path ?? string.Empty;
            return name.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void RunPreMenu(DiagSession s, ref NOModContext ctx)
        {
            LoaderLog.Phase("Menu — PreMenu components");

            s.Assert("premenu.mod_context", !string.IsNullOrEmpty(ctx.ModId) && ctx.ModIdHash != 0);
            s.Assert("premenu.stage", ctx.Stage == LoadStage.PreMenu);
            AssertFile(s, "premenu.mod_manifest", Path.Combine(ctx.ModRoot, "mod.json"));

            string coreDir = Path.Combine(ctx.GameRoot, "NOLoader", "core");
            AssertFile(s, "premenu.core_dll", Path.Combine(coreDir, "NOLoader.Core.dll"));
            AssertFile(s, "premenu.api_dll", Path.Combine(coreDir, "NOLoader.API.dll"));
            AssertFile(s, "premenu.patcher_dll", Path.Combine(coreDir, "NOLoader.Patcher.dll"));
            AssertFile(s, "premenu.registry_dll", Path.Combine(coreDir, "NOLoader.Registry.dll"));
            AssertFile(s, "premenu.telemetry_dll", Path.Combine(coreDir, "NOLoader.Telemetry.dll"));

            int h1 = StringHash.Murmur32("LoaderDiagMenu-probe");
            s.Assert("premenu.hash_stable", h1 == StringHash.Murmur32("LoaderDiagMenu-probe"));
            StringHashTable.Register("LoaderDiagMenu-probe");
            s.Assert("premenu.hash_table", StringHashTable.TryDecode(h1, out string? d) && d == "LoaderDiagMenu-probe");
            s.Assert("premenu.hash_only_mode", !StringHashTable.DevAutoRegisterEnabled);

            s.Assert("premenu.gate_l1_panel", CoreProbe.IsGateReportStorePresent());
            s.Assert("premenu.mod_patch_scheduler", CoreProbe.IsModPatchSchedulerPresent());
            s.Assert("premenu.physics_mass", Math.Abs(PhysicsSafetyCatch.SanitizeMass(float.NaN) - 0.001f) < 0.0001f);
            s.Assert("premenu.physics_force", PhysicsSafetyCatch.SanitizeForce(float.PositiveInfinity) == 0f);
            s.Assert("premenu.physics_vector3", PhysicsSafetyCatch.SanitizeVector3(new Vector3(float.NaN, 1f, 2f)) == Vector3.zero);

            var badMissile = new MissileEntry { JsonKeyHash = 1 };
            s.Assert("premenu.gate_l3_missile", !NOModRegistry.RegisterMissile(ref badMissile));
            var badWeapon = new WeaponMountEntry { JsonKeyHash = 1 };
            s.Assert("premenu.gate_l3_weapon", !NOModRegistry.RegisterWeaponMount(ref badWeapon));

            s.Assert("premenu.gate_l2_enforced", CoreProbe.ReadGateL2Enforced());
            s.Assert("premenu.baked_core_hashes", CoreProbe.ReadBakedCoreHashCount() >= 7,
                "count=" + CoreProbe.ReadBakedCoreHashCount());

            s.Assert("premenu.patch_bootstrap", GameProbe.IsDllMarker(ctx.GameRoot, "Assembly-CSharp.dll", "OnMainMenuReady"));
            s.Assert("premenu.patch_registry", GameProbe.IsDllMarker(ctx.GameRoot, "Assembly-CSharp.dll", "RegistryGameBridge"));
            s.Assert("premenu.patch_physics_game", GameProbe.IsDllMarker(ctx.GameRoot, "Assembly-CSharp.dll", "PhysicsCatchHooks"));
            s.Assert("premenu.patch_gatel4", GameProbe.IsDllMarker(ctx.GameRoot, "Assembly-CSharp.dll", "CanLoadPrefixSkip"));
            s.Assert("premenu.patch_unity_scene", GameProbe.IsDllMarker(ctx.GameRoot, "UnityEngine.CoreModule.dll", "SceneLoadPrefixSkip"));
            s.Assert("premenu.patch_unity_physics", GameProbe.IsDllMarker(ctx.GameRoot, "UnityEngine.PhysicsModule.dll", "RigidbodyAddForcePrefixSkip"));
            s.Assert("premenu.gate_l4_dual_hooks", CoreProbe.GateL4HooksPresent(ctx.GameRoot));
            if (CoreProbe.IsGameFullyPrePatched(ctx.GameRoot))
                s.Pass("premenu.runtime_cecil_skip", "all pre-patch markers");
            else if (CoreProbe.IsCorePrePatched(ctx.GameRoot))
                s.Pass("premenu.runtime_cecil_skip", "core pre-patch markers");
            else
                s.Skip("premenu.runtime_cecil_skip", "runtime Cecil path");

            string brokenModPath = Path.Combine(ctx.GameRoot, "NOLoader", "mods", "BrokenMod");
            if (Directory.Exists(brokenModPath))
            {
                bool l2Negative = CoreProbe.ReadRingLogContains(ctx.GameRoot, "com.at747.brokenmod rejected")
                    || CoreProbe.ReadRingLogContains(ctx.GameRoot, "Signature mismatch for Encyclopedia::AfterLoad")
                    || CoreProbe.ReadRingLogContains(ctx.GameRoot, "Patch failed for mod com.at747.brokenmod");
                s.Assert("premenu.gate_l2_negative", l2Negative);
            }
            else
                s.Fail("premenu.gate_l2_negative", "BrokenMod not deployed");

            AssertFile(s, "premenu.weaponnames_deployed", Path.Combine(ctx.GameRoot, "NOLoader", "mods", "WeaponNames", "mod.json"));
            AssertFile(s, "premenu.weaponnames_dll", Path.Combine(ctx.GameRoot, "NOLoader", "mods", "WeaponNames", "NOLoader.WeaponNames.dll"));
            s.Assert("premenu.l4_fault_tracking", CoreProbe.LoadedModSupportsMissionFaultTracking());

            string coreVersion = CoreProbe.ReadCoreDisplayVersion();
            s.Assert("premenu.core_version", !string.IsNullOrEmpty(coreVersion), coreVersion);
            s.Assert("premenu.core_version_dev", coreVersion.IndexOf("DEV1P", StringComparison.Ordinal) >= 0, coreVersion);

            s.Assert("premenu.manifest_guid", CoreProbe.ReadManifestHasGuid(ctx.ModRoot));

            s.Assert("premenu.loader_log", CoreProbe.LoaderLogBound());
            s.Assert("premenu.ring_log", Directory.Exists(Path.Combine(ctx.GameRoot, "NOLoader", "logs")));
            s.Assert("premenu.proxy_log", File.Exists(Path.Combine(ctx.GameRoot, "NOLoader", "logs", "proxy.log")));

            s.Assert("premenu.no_battle_host", GameObject.Find("NOLoader.LoaderDiag.Host") == null);

            LoaderLog.Info("Menu PreMenu section done");
        }

        public static bool TryRunMainMenu(DiagSession s)
        {
            if (_mainMenuDone)
                return true;

            if (!IsMenuScene(SceneManager.GetActiveScene()))
                return false;

            Type? encType = GameProbe.FindType("Encyclopedia");
            object? enc = encType?.GetProperty("i", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (enc == null)
                return false;

            LoaderLog.Phase("Menu — MainMenu game state");

            FieldInfo? lookupField = encType!.GetField("Lookup", BindingFlags.Public | BindingFlags.Static);
            int lookupCount = GameProbe.CountDictionary(lookupField?.GetValue(null));
            s.Assert("mainmenu.encyclopedia_lookup", lookupCount > 0, "count=" + lookupCount);

            FieldInfo? weaponLookup = encType.GetField("WeaponLookup", BindingFlags.Public | BindingFlags.Static);
            int weaponCount = GameProbe.CountDictionary(weaponLookup?.GetValue(null));
            s.Assert("mainmenu.weapon_lookup", weaponCount > 0, "count=" + weaponCount);

            Type? mainMenuType = GameProbe.FindType("MainMenu");
            object? state = mainMenuType?.GetProperty("State", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (state == null || state.ToString() != "Loaded")
                return false;

            s.Pass("mainmenu.state_loaded", state.ToString() ?? "Loaded");

            s.Assert("mainmenu.diag_host", GameObject.Find(LoaderDiagMenuMod.HostObjectName) != null);
            s.Assert("mainmenu.telemetry_port", CoreProbe.ReadTelemetryPort() == 49000);
            s.Assert("mainmenu.gate_l4_clear", !CoreProbe.ReadMissionBlocked());

            if (CoreProbe.TryReadMissionGateInstalled(out bool gateInstalled))
                s.Assert("mainmenu.gate_l4_installed", gateInstalled);
            else
                s.Fail("mainmenu.gate_l4_installed", "MissionGate type missing");

            if (CoreProbe.TryReadDevFlag("DevOverlayHost", out bool overlay))
                s.Assert("mainmenu.dev_overlay", overlay);

            s.Assert("mainmenu.telemetry_host", CoreProbe.IsTelemetryHostPresent());
            s.Assert("mainmenu.gate_l4_hooks", CoreProbe.GateL4HooksPresent(
                Path.Combine(Application.dataPath, "..")));
            s.Assert("mainmenu.weaponnames_patch",
                GameProbe.IsDllMarker(Path.Combine(Application.dataPath, ".."), "Assembly-CSharp.dll", "AfterLoadPostfix"));

            LoaderLog.Info("mainmenu.registry missiles=" + NOModRegistry.GetMissiles().Count
                + " weapons=" + NOModRegistry.GetWeaponMounts().Count
                + " aircraft=" + NOModRegistry.GetAircraft().Count);

            _mainMenuDone = true;
            LoaderLog.Info("Menu MainMenu section done");
            return true;
        }

        public static bool TryRunIntegration(DiagSession s)
        {
            if (_integrationDone)
                return true;

            if (!_mainMenuDone)
                return false;

            LoaderLog.Phase("Menu — loader integration (subsystems together)");

            s.Assert("integration.log_registry", CoreProbe.LoaderLogBound() && NOModRegistry.GetMissiles() != null);
            s.Assert("integration.hash_only_mode", !StringHashTable.DevAutoRegisterEnabled);
            s.Assert("integration.registry_pipeline", CoreProbe.IsRegistryPipelinePresent());
            s.Assert("integration.telemetry_live", CoreProbe.ReadTelemetryPort() == 49000);
            s.Assert("integration.physics_api", CoreProbe.ReadPhysicsCatchCount() >= 0);

            int asmCache = CoreProbe.ReadModAssemblyCacheCount();
            s.Assert("integration.asm_cache", asmCache > 0, "entries=" + asmCache);

            int typeCache = CoreProbe.ReadGameTypeCacheCount();
            s.Assert("integration.type_cache", typeCache >= 0, "types=" + typeCache);

            if (CoreProbe.ReadReflectionResolveCount() >= 0)
            {
                int refl = CoreProbe.ReadReflectionResolveCount();
                LoaderLog.Info("integration.reflection_resolves=" + refl);
                s.Pass("integration.reflection_tracker", "resolves=" + refl);
            }
            else
                s.Skip("integration.reflection_tracker", "RDYTU or tracker absent");

            s.Assert("integration.no_battle_diag", GameObject.Find("NOLoader.LoaderDiag.Host") == null);
            s.Assert("integration.gate_l2_enforced", CoreProbe.ReadGateL2Enforced());
            s.Assert("integration.gate_l4_mission", CoreProbe.TryReadMissionGateInstalled(out bool gate4) && gate4);
            s.Assert("integration.l4_fault_tracking", CoreProbe.LoadedModSupportsMissionFaultTracking());
            s.Assert("integration.telemetry_host", CoreProbe.IsTelemetryHostPresent());
            s.Skip("integration.battle_telemetry", "NO2 live UDP verified by LoaderDiag Mission mod");
            s.Skip("integration.combat_hud", "CombatHUD verified by LoaderDiag Mission mod");
            s.Skip("integration.flight_hud", "FlightHud verified by LoaderDiag Mission mod");

            _integrationDone = true;
            LoaderLog.Info("Menu integration section done");
            return true;
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
