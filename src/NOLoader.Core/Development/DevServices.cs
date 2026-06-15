#if NOLoader_DEV
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using NOLoader.Core.Gates;
using NOLoader.Core.Interop;
using NOLoader.Core.Logging;
using NOLoader.Core.Mods;
using UnityEngine;

namespace NOLoader.Core.Development
{
    public static class DevOverlayHost
    {
        private static GameObject? _host;
        internal static bool Installed => _host != null;

        public static void Initialize(string loaderRoot)
        {
            if (_host != null) return;
            RingBufferLog.StartBackgroundFlush(loaderRoot);
            UnityMainThread.Post(InstallOverlay);
        }

        private static void InstallOverlay()
        {
            if (_host != null) return;
            _host = new GameObject("NOLoader.DevOverlay");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<DevOverlayBehaviour>();
        }
    }

    public static class GateL1PanelHost
    {
        private static GameObject? _host;

        public static void InstallIfNeeded()
        {
            if (!GateReportStore.HasL1Errors && GateReportStore.GetL2Errors().Count == 0)
                return;

            UnityMainThread.Post(InstallPanel);
        }

        private static void InstallPanel()
        {
            if (_host != null)
                return;

            _host = new GameObject("NOLoader.GateL1Panel");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<GateL1PanelBehaviour>();
        }
    }

    internal sealed class GateL1PanelBehaviour : MonoBehaviour
    {
        private bool _visible = true;

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F12))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            IReadOnlyList<string> l1 = GateReportStore.GetL1Errors();
            IReadOnlyList<string> l2 = GateReportStore.GetL2Errors();
            if (l1.Count == 0 && l2.Count == 0)
                return;

            GUI.Box(new Rect(Screen.width - 440, 10, 420, 260), "NOLoader Gate L1/L2 — F12 toggle");
            var sb = new StringBuilder();
            if (l1.Count > 0)
            {
                sb.AppendLine("Gate L1 (manifest):");
                foreach (string err in l1)
                    sb.AppendLine(" • " + err);
            }

            if (l2.Count > 0)
            {
                sb.AppendLine("Gate L2 (IL patch):");
                foreach (string err in l2)
                    sb.AppendLine(" • " + err);
            }

            GUI.Label(new Rect(Screen.width - 430, 36, 400, 220), sb.ToString());
        }
    }

    internal sealed class DevOverlayBehaviour : MonoBehaviour
    {
        private bool _visible = true;

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F10))
                _visible = !_visible;
            if (UnityEngine.Input.GetKeyDown(KeyCode.F11))
                HotReloadService.ReloadAll();
        }

        private void OnGUI()
        {
            if (!_visible) return;

            long gcBytes = GC.GetTotalMemory(false);
            int physicsCatch = NOLoader.Registry.PhysicsCatchHooks.InterceptCount;
            int hashKnown = NOLoader.API.StringHashTable.KnownCount;
            bool gateVisible = ModLifecycleManager.AnyMissionBlocked;
            int typeCache = GameTypeCache.CachedTypeCount;
            int asmCache = ModAssemblyCache.EntryCount;
#if NOLoader_DEV
            int refl = ReflectionTracker.TotalResolves;
            string reflLine = $" | Types cached: {typeCache} | DLL index: {asmCache} | Reflection: {refl} ({ReflectionTracker.TypeHits}T+{ReflectionTracker.MemberHits}M hit)";
#else
            string reflLine = $" | Types cached: {typeCache} | DLL index: {asmCache}";
#endif

            GUI.Box(new Rect(10, 10, 620, 380),
                "NOLoader DEV.SDK — F10 toggle | F11 hot-reload (unload assets + reload mods)");
            GUI.Label(new Rect(20, 36, 600, 20),
                $"GC heap: {gcBytes / 1024} KB | PhysicsCatch: {physicsCatch} | Known hashes: {hashKnown} | GateL4: {(gateVisible ? "BLOCKED" : "ok")}{reflLine}");

            string tail = RingBufferLog.ReadTail(2800);
            string decoded = DevOverlayHashDecoder.AppendDecodedHashes(tail);
            GUI.Label(new Rect(20, 60, 540, 300), decoded);
        }
    }

    internal static class DevOverlayHashDecoder
    {
        public static string AppendDecodedHashes(string logTail)
        {
            if (string.IsNullOrEmpty(logTail))
                return logTail;

            var lines = logTail.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return logTail;

            var last = lines[lines.Length - 1];
            int hashIdx = last.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
            if (hashIdx < 0)
                return logTail;

            string hex = last.Substring(hashIdx + 2, Math.Min(8, last.Length - hashIdx - 2));
            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int hash)
                && NOLoader.API.StringHashTable.TryDecode(hash, out string? decoded))
            {
                return logTail + Environment.NewLine + "[hash] " + hex + " -> " + decoded;
            }

            return logTail;
        }
    }

    public static class HotReloadService
    {
        private static FileSystemWatcher? _dllWatcher;
        private static FileSystemWatcher? _sourceWatcher;
        private static string _modsRoot = string.Empty;
        private static int _reloadScheduled;

        public static void Initialize(string modsRoot)
        {
            if (!Directory.Exists(modsRoot))
                return;

            _modsRoot = modsRoot;
            _dllWatcher = new FileSystemWatcher(modsRoot)
            {
                IncludeSubdirectories = true,
                Filter = "*.dll",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            _dllWatcher.Changed += (_, __) => ScheduleReload();
            _dllWatcher.Created += (_, __) => ScheduleReload();
            _dllWatcher.Deleted += (_, __) => ScheduleReload();
            _dllWatcher.Renamed += (_, __) => ScheduleReload();
            _dllWatcher.EnableRaisingEvents = true;

            _sourceWatcher = new FileSystemWatcher(modsRoot)
            {
                IncludeSubdirectories = true,
                Filter = "*.cs",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            _sourceWatcher.Changed += (_, e) => ScriptCompileService.ScheduleBuild(e.FullPath);
            _sourceWatcher.Created += (_, e) => ScriptCompileService.ScheduleBuild(e.FullPath);
            _sourceWatcher.Deleted += (_, __) => ScheduleReload();
            _sourceWatcher.Renamed += (_, __) => ScheduleReload();
            _sourceWatcher.EnableRaisingEvents = true;
        }

        private static void ScheduleReload()
        {
            if (Interlocked.Exchange(ref _reloadScheduled, 1) != 0)
                return;

            UnityMainThread.Post(() =>
            {
                System.Threading.Thread.Sleep(350);
                Interlocked.Exchange(ref _reloadScheduled, 0);
                ReloadAll();
            });
        }

        public static void ReloadAll()
        {
            UnityMainThread.Invoke(() =>
            {
                Resources.UnloadUnusedAssets();
                GC.Collect();
            });

            ModLifecycleManager.SyncWithDisk();
            ModLifecycleManager.ReloadAllMods();
            RingBufferLog.WriteAscii("[NOLoader] Hot-reload complete (mods synced + reloaded)");
        }
    }

    internal static class ScriptCompileService
    {
        private static readonly object Gate = new object();
        private static string? _pendingProject;
        private static int _buildScheduled;

        public static void ScheduleBuild(string changedFile)
        {
            string? project = FindProjectForFile(changedFile);
            if (project == null)
                return;

            lock (Gate)
                _pendingProject = project;

            if (Interlocked.Exchange(ref _buildScheduled, 1) != 0)
                return;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                System.Threading.Thread.Sleep(500);
                Interlocked.Exchange(ref _buildScheduled, 0);
                string? toBuild;
                lock (Gate)
                {
                    toBuild = _pendingProject;
                    _pendingProject = null;
                }

                if (string.IsNullOrEmpty(toBuild))
                    return;

                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "build \"" + toBuild + "\" -c Debug --verbosity quiet",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    proc?.WaitForExit(120000);
                    RingBufferLog.WriteAscii("[NOLoader] Script compile " + (proc?.ExitCode == 0 ? "OK" : "FAIL") + ": " + toBuild);
                }
                catch (Exception ex)
                {
                    RingBufferLog.WriteAscii("[NOLoader] Script compile error: " + ex.Message);
                }
            });
        }

        private static string? FindProjectForFile(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                string[] projects = Directory.GetFiles(dir, "*.csproj");
                if (projects.Length > 0)
                    return projects[0];
                dir = Directory.GetParent(dir)?.FullName;
            }

            return null;
        }
    }
}
#endif
