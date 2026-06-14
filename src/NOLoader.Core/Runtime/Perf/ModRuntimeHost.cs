using System.Collections;
using NOLoader.Core.EngineTweaker;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.Runtime.Perf
{
    internal static class ModRuntimeHost
    {
        private static GameObject? _host;

        public static void EnsureInstalled()
        {
            if (_host != null)
                return;

            UnityMainThread.Post(CreateHost);
        }

        internal static void EnsureForEngineTweaker()
        {
            if (!RuntimeConfig.EngineTweakerEnabled)
                return;

            EnsureInstalled();
        }

        private static void CreateHost()
        {
            if (_host != null)
                return;

            _host = new GameObject("NOLoader.ModRuntimeHost");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<ModRuntimeHostBehaviour>();
        }
    }

    /// <summary>Runs after all gameplay LateUpdates (incl. CameraStateManager order 2 + TrackIR).</summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class ModRuntimeHostBehaviour : MonoBehaviour
    {
        private static readonly WaitForEndOfFrame EndOfFrameWait = new WaitForEndOfFrame();
        private Coroutine? _tweakerCoroutine;

        private void Start()
        {
#if !NOLoader_DEV
            if (RuntimeConfig.EngineTweakerEnabled)
                _tweakerCoroutine = StartCoroutine(TweakerEndOfFrameLoop());
#endif
        }

        private void OnDestroy()
        {
            if (_tweakerCoroutine != null)
                StopCoroutine(_tweakerCoroutine);
        }

        private IEnumerator TweakerEndOfFrameLoop()
        {
            while (enabled)
            {
                yield return EndOfFrameWait;
                EngineTweakerFrameLoop.RunEndOfFrame();
            }
        }

        private void Update()
        {
#if !NOLoader_DEV
            bool protectCamera = TrackIrStabilityGuard.ShouldProtectVanillaCameraFrame();

            if (RuntimeConfig.EngineTweakerEnabled && !protectCamera)
                GameplayMechanicsGuard.PinLocalPlayerSimState();
#endif

            if (!ModTickScheduler.HasTickMods && !WorldSnapshotService.Instance.IsActive)
                return;

            WorldSnapshotService.Instance.Tick();
            ModTickScheduler.TickNormal();
            ModTickScheduler.TickSlow();
        }

        private void LateUpdate()
        {
            if (!ModTickScheduler.HasTickMods)
                return;

            ModTickScheduler.TickFast(Time.deltaTime);
            ModExecutionBudget.Instance.EndFrame();
        }
    }
}
