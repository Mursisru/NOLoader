#if !NOLoader_DEV
using System;
using System.Collections.Generic;
using NOLoader.API;
using UnityEngine;

namespace NOLoader.Core.ModOptimizer
{
    internal sealed class ModSceneLocator : INOModSceneLocator
    {
        public static readonly ModSceneLocator Instance = new ModSceneLocator();

        private readonly Dictionary<string, GameObject> _byName = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<int, HashSet<string>> _namesByMod = new Dictionary<int, HashSet<string>>();
        private readonly object _gate = new object();

        public void Register(string name, object go)
        {
            if (go is not GameObject gameObject)
                return;

            if (!ModOptimizerBootstrap.IsSceneLocatorActive || string.IsNullOrEmpty(name))
                return;

            lock (_gate)
            {
                _byName[name] = gameObject;
            }
        }

        public bool TryGet(string name, out object go)
        {
            if (!ModOptimizerBootstrap.IsSceneLocatorActive || string.IsNullOrEmpty(name))
            {
                go = null!;
                return false;
            }

            lock (_gate)
            {
                if (_byName.TryGetValue(name, out GameObject? cached) && cached != null)
                {
                    go = cached;
                    return true;
                }
            }

            go = null!;
            return false;
        }

        public void Unregister(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            lock (_gate)
            {
                _byName.Remove(name);
            }
        }

        internal void TrackModName(int modIdHash, string name)
        {
            if (modIdHash == 0 || string.IsNullOrEmpty(name))
                return;

            lock (_gate)
            {
                if (!_namesByMod.TryGetValue(modIdHash, out HashSet<string>? set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    _namesByMod[modIdHash] = set;
                }

                set.Add(name);
            }
        }

        internal void ClearMod(int modIdHash)
        {
            if (modIdHash == 0)
                return;

            lock (_gate)
            {
                if (_namesByMod.TryGetValue(modIdHash, out HashSet<string>? names))
                {
                    foreach (string name in names)
                        _byName.Remove(name);
                    _namesByMod.Remove(modIdHash);
                }
            }
        }
    }
}
#endif
