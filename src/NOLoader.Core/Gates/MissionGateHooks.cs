using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using NOLoader.Core.Mods;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOLoader.Core.Gates
{
    public static class MissionGate
    {
        private static bool _installed;

        public static bool IsInstalled => _installed;

        public static void Install()
        {
            _installed = true;
        }

        internal static void EnsureExceptionTracking()
        {
            if (!RuntimeConfig.ExceptionTracking || ModLifecycleManager.LoadedModCount <= 0)
                return;

            if (!RuntimeConfig.ExceptionTrackingNeedsSubscription)
                return;

            ModExceptionTracker.Install();
        }
    }

    internal static class ModExceptionTracker
    {
        private struct ModStackNeedle
        {
            public string AsmName;
            public string FolderPath;
            public string ModLabel;
        }

        private static bool _installed;
        private static ModStackNeedle[] _needles = Array.Empty<ModStackNeedle>();

        public static void Install()
        {
            if (_installed)
            {
                RefreshNeedles();
                return;
            }

            _installed = true;
            RefreshNeedles();
            Application.logMessageReceived += OnLogMessage;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
        }

        public static void RefreshNeedles()
        {
            int count = 0;
            foreach (LoadedMod mod in ModLifecycleManager.AllMods)
            {
                if (mod.Loaded && mod.Assembly != null)
                    count++;
            }

            if (count == 0)
            {
                _needles = Array.Empty<ModStackNeedle>();
                return;
            }

            var next = new ModStackNeedle[count];
            int i = 0;
            foreach (LoadedMod mod in ModLifecycleManager.AllMods)
            {
                if (!mod.Loaded || mod.Assembly == null)
                    continue;

                next[i++] = new ModStackNeedle
                {
                    AsmName = mod.Assembly.GetName().Name ?? string.Empty,
                    FolderPath = mod.Manifest.FolderPath,
                    ModLabel = !string.IsNullOrEmpty(mod.Manifest.Id)
                        ? mod.Manifest.Id
                        : mod.Manifest.IdHash.ToString("X8")
                };
            }

            _needles = next;
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
                return;

            if (_needles.Length == 0)
                return;

            TryFlagModFromStack(stackTrace, condition);
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                TryFlagModFromStack(ex.StackTrace ?? ex.ToString(), ex.Message);
        }

        private static void TryFlagModFromStack(string stack, string? condition = null)
        {
            for (int i = 0; i < _needles.Length; i++)
            {
                ModStackNeedle needle = _needles[i];
                if (MatchesNeedle(needle, stack, condition))
                {
                    ModLifecycleManager.FlagModForMissionBlock(needle.ModLabel, new Exception(
                        string.IsNullOrEmpty(condition) ? stack : condition + "\n" + stack));
                    RingBufferLog.WriteAscii("[GateL4] Mod fault tracked: " + needle.ModLabel);
                    if (!string.IsNullOrEmpty(condition))
                        RingBufferLog.WriteAscii("[GateL4] Fault detail: " + condition);
                    return;
                }
            }
        }

        private static bool MatchesNeedle(ModStackNeedle needle, string stack, string? condition)
        {
            if (needle.AsmName.Length > 0)
            {
                if (condition != null
                    && condition.IndexOf(needle.AsmName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (stack.IndexOf(needle.AsmName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            if (needle.FolderPath.Length > 0)
            {
                if (condition != null
                    && condition.IndexOf(needle.FolderPath, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (stack.IndexOf(needle.FolderPath, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }

    public static class MissionGateHooks
    {
        private static FieldInfo? _mapKeyPathField;
        private static string? _mainMenuScene;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanLoadPrefixSkip(object mapLoader, object mapKey)
        {
            if (ModLifecycleManager.MissionBlockedFlagRaw == 0)
                return true;

            if (IsMainMenuMapKey(mapKey))
                return true;

            MissionGateState.SetBlocked(MissionGateState.BuildMessage());
#if NOLoader_DEV
            MissionGateState.ShowBlockedBanner();
#endif
            RingBufferLog.WriteAscii("[GateL4] MapLoader.CanLoad blocked");
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SceneLoadPrefixSkip(string sceneName)
        {
            if (ModLifecycleManager.MissionBlockedFlagRaw == 0)
                return true;

            if (IsMainMenuScene(sceneName))
                return true;

            MissionGateState.SetBlocked(MissionGateState.BuildMessage());
#if NOLoader_DEV
            MissionGateState.ShowBlockedBanner();
#endif
            RingBufferLog.WriteAscii("[GateL4] SceneManager.LoadSceneAsync blocked: " + sceneName);
            ReturnToMainMenu();
            return false;
        }

        public static string GetMainMenuScene() => ResolveMainMenuScene();

        private static bool IsMainMenuMapKey(object mapKey)
        {
            EnsureMapKeyReflection(mapKey.GetType());
            string? path = _mapKeyPathField?.GetValue(mapKey) as string;
            if (string.IsNullOrEmpty(path))
                return false;
            return IsMainMenuScene(path!);
        }

        private static bool IsMainMenuScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            string mainMenu = ResolveMainMenuScene();
            if (string.IsNullOrEmpty(mainMenu))
                return sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0;

            return string.Equals(sceneName, mainMenu, StringComparison.OrdinalIgnoreCase)
                || string.Equals(System.IO.Path.GetFileNameWithoutExtension(sceneName),
                    System.IO.Path.GetFileNameWithoutExtension(mainMenu), StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveMainMenuScene()
        {
            if (!string.IsNullOrEmpty(_mainMenuScene))
                return _mainMenuScene!;

            Type? mapLoaderType = FindType("NuclearOption.SceneLoading.MapLoader");
            FieldInfo? field = mapLoaderType?.GetField("MainMenu", BindingFlags.Public | BindingFlags.Static);
            _mainMenuScene = field?.GetValue(null) as string ?? string.Empty;
            return _mainMenuScene ?? string.Empty;
        }

        private static void EnsureMapKeyReflection(Type mapKeyType)
        {
            if (_mapKeyPathField != null)
                return;

            BindingFlags inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _mapKeyPathField = mapKeyType.GetField("Path", inst) ?? mapKeyType.GetField("path", inst);
        }

        private static Type? FindType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t = asm.GetType(fullName, throwOnError: false);
                if (t != null)
                    return t;
            }

            return null;
        }

        private static void ReturnToMainMenu()
        {
            UnityMainThread.Post(() =>
            {
                try
                {
                    string mainMenu = ResolveMainMenuScene();
                    if (!string.IsNullOrEmpty(mainMenu))
                        SceneManager.LoadScene(mainMenu);
                }
                catch (Exception ex)
                {
                    RingBufferLog.WriteAscii("[GateL4] Return to menu failed: " + ex.Message);
                }
            });
        }
    }

    internal static class MissionGateState
    {
        private static string _bannerText = string.Empty;
        private static bool _visible;
#if NOLoader_DEV
        private static GameObject? _host;
#endif

        public static bool IsVisible => _visible;

        public static void SetBlocked(string message)
        {
            _bannerText = message;
            _visible = true;
            RingBufferLog.WriteAscii("[GateL4] " + message.Replace("\n", " | "));
        }

#if NOLoader_DEV
        public static void ShowBlockedBanner()
        {
            UnityMainThread.Post(EnsureHost);
        }
#endif

        public static string BuildMessage()
        {
            foreach (LoadedMod mod in ModLifecycleManager.AllMods)
            {
                if (!mod.BlockedForMission)
                    continue;

                string modLabel = !string.IsNullOrEmpty(mod.Manifest.Id)
                    ? mod.Manifest.Id
                    : mod.Manifest.IdHash.ToString("X8");
                string trace = mod.LastException?.ToString() ?? mod.Error ?? "Unknown mod fault";
                return "NOLoader Gate L4 — mission load blocked\nMod: " + modLabel + "\n\n" + trace;
            }

            return "NOLoader Gate L4 — mission load blocked due to mod fault.";
        }

        private static void EnsureHost()
        {
#if NOLoader_DEV
            if (_host != null)
                return;

            _host = new GameObject("NOLoader.MissionGateBanner");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<MissionGateBannerBehaviour>();
#endif
        }

#if NOLoader_DEV
        internal sealed class MissionGateBannerBehaviour : MonoBehaviour
        {
            private Vector2 _scroll;

            private void OnGUI()
            {
                if (!_visible)
                    return;

                GUI.color = new Color(1f, 0.12f, 0.12f, 0.97f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Box(new Rect(20, 20, Screen.width - 40, Screen.height - 40), "NOLoader — Gate L4 (mission blocked)");
                _scroll = GUI.BeginScrollView(
                    new Rect(40, 70, Screen.width - 80, Screen.height - 170),
                    _scroll,
                    new Rect(0, 0, Screen.width - 120, Mathf.Max(600, _bannerText.Length / 2)));
                GUI.Label(new Rect(0, 0, Screen.width - 140, Mathf.Max(600, _bannerText.Length / 2)), _bannerText);
                GUI.EndScrollView();

                if (GUI.Button(new Rect(Screen.width * 0.5f - 140, Screen.height - 90, 280, 48), "Return to Main Menu"))
                {
                    _visible = false;
                    string mainMenu = MissionGateHooks.GetMainMenuScene();
                    if (!string.IsNullOrEmpty(mainMenu))
                        SceneManager.LoadScene(mainMenu);
                }
            }
        }
#endif
    }
}
