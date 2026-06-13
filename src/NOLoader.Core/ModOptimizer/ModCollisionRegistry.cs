#if !NOLoader_DEV
using System;
using NOLoader.API;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.ModOptimizer
{
    internal sealed class ModCollisionRegistry : INOModCollisionRegistry
    {
        public static readonly ModCollisionRegistry Instance = new ModCollisionRegistry();

        private static bool _layerInit;
        private readonly object _gate = new object();
        private int _registered;

        public void RegisterProjectile(object go, ModCollisionProfile profile)
        {
            if (go is not GameObject gameObject)
                return;

            if (!ModOptimizerBootstrap.IsCollisionLayersActive)
                return;

            EnsureLayerMatrix();
            int layer = ResolveLayer(profile);
            gameObject.layer = layer;

            lock (_gate)
            {
                _registered++;
            }

            if (_registered == 1 || (_registered % 10) == 0)
            {
                RingBufferLog.WriteAscii("[ModOpt] collision registered=" + _registered
                    + " layer=" + layer + " profile=" + profile);
            }
        }

        public void Unregister(object go)
        {
            if (go == null)
                return;

            lock (_gate)
            {
                if (_registered > 0)
                    _registered--;
            }
        }

        private static void EnsureLayerMatrix()
        {
            if (_layerInit)
                return;

            _layerInit = true;
            int projectileLayer = RuntimeConfig.ModLayerProjectile;
            Physics.IgnoreLayerCollision(projectileLayer, projectileLayer, true);
            RingBufferLog.WriteAscii("[ModOpt] collision matrix init layer=" + projectileLayer);
        }

        private static int ResolveLayer(ModCollisionProfile profile)
        {
            switch (profile)
            {
                case ModCollisionProfile.Debris:
                    return Math.Min(RuntimeConfig.ModLayerProjectile + 1, 31);
                case ModCollisionProfile.VisualOnly:
                    return Math.Min(RuntimeConfig.ModLayerProjectile + 2, 31);
                default:
                    return RuntimeConfig.ModLayerProjectile;
            }
        }
    }
}
#endif
