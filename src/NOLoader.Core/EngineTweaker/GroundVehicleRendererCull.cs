using System.Collections;
using System.Collections.Generic;
using NOLoader.Core.Interop;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal static class GroundVehicleRendererCull
    {
        private const int BatchPerFrame = 128;

        private static readonly Dictionary<int, Renderer[]> _rendererCache = new Dictionary<int, Renderer[]>();
        private static System.Type? _groundVehicleType;
        private static int _roundRobinIndex;
        private static long _rendererSkipped;

        internal static long RendererSkipped => _rendererSkipped;

        internal static void ResetStats()
        {
            _rendererSkipped = 0;
        }

        internal static void ClearCache()
        {
            _rendererCache.Clear();
            _roundRobinIndex = 0;
            _groundVehicleType = null;
        }

        internal static void ApplyFrame()
        {
            if (!RuntimeConfig.CullingGroundRendererEnabled || !RuntimeConfig.CullingGroundWheelsEnabled)
                return;

            EnsureGroundVehicleType();
            if (_groundVehicleType == null)
                return;

            if (!EngineTweakerGameAccess.TryGetAllUnitsList(out IList? units) || units == null || units.Count == 0)
                return;

            int count = units.Count;

            for (int i = 0; i < count; i++)
            {
                object? unit = units[i];
                if (unit == null || !_groundVehicleType.IsInstanceOfType(unit))
                    continue;

                if (EngineCullingState.ShouldCullGroundRenderer(unit))
                    ApplyUnit(unit);
            }

            int batch = System.Math.Min(BatchPerFrame, count);
            for (int i = 0; i < batch; i++)
            {
                int index = (_roundRobinIndex + i) % count;
                object? unit = units[index];
                if (unit == null || !_groundVehicleType.IsInstanceOfType(unit))
                    continue;

                if (EngineCullingState.ShouldCullGroundRenderer(unit))
                    continue;

                ApplyUnit(unit);
            }

            _roundRobinIndex = (_roundRobinIndex + batch) % count;
        }

        private static void ApplyUnit(object unit)
        {
            bool cull = EngineCullingState.ShouldCullGroundRenderer(unit);
            Renderer[] renderers = GetOrCreateRenderers(unit);
            if (renderers.Length == 0)
                return;

            bool anyChanged = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                bool shouldEnable = !cull;
                if (renderer.enabled != shouldEnable)
                {
                    renderer.enabled = shouldEnable;
                    anyChanged = true;
                }
            }

            if (cull && anyChanged)
                _rendererSkipped++;
        }

        private static Renderer[] GetOrCreateRenderers(object unit)
        {
            int id = GetInstanceId(unit);
            if (_rendererCache.TryGetValue(id, out Renderer[]? cached) && cached != null)
                return cached;

            if (unit is Component component)
            {
                Renderer[] found = component.GetComponentsInChildren<Renderer>(true);
                _rendererCache[id] = found;
                return found;
            }

            _rendererCache[id] = System.Array.Empty<Renderer>();
            return _rendererCache[id];
        }

        private static int GetInstanceId(object unit)
        {
            if (unit is Object unityObject)
                return unityObject.GetInstanceID();
            return unit.GetHashCode();
        }

        private static void EnsureGroundVehicleType()
        {
            if (_groundVehicleType == null)
                _groundVehicleType = GameTypeCache.Resolve("GroundVehicle");
        }
    }
}
