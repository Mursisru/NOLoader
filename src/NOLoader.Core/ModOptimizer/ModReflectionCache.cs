#if !NOLoader_DEV
using System;
using System.Collections.Generic;
using System.Reflection;
using NOLoader.API;
using NOLoader.API.Manifest;
using NOLoader.Core.Logging;

namespace NOLoader.Core.ModOptimizer
{
    internal sealed class ModReflectionCache : INOModReflectionCache
    {
        public static readonly ModReflectionCache Instance = new ModReflectionCache();

        private readonly Dictionary<long, Delegate> _delegates = new Dictionary<long, Delegate>();
        private readonly object _gate = new object();

        public bool TryGetDelegate<T>(Assembly modAsm, string typeName, string methodName, out T del) where T : Delegate
        {
            del = null!;
            if (!ModOptimizerBootstrap.IsReflectionCacheActive)
                return false;

            if (!UnityMainThread.IsMainThread)
            {
                RingBufferLog.WriteAscii("[ModOpt][WARN] reflection cache main-thread only");
                return false;
            }

            long key = MakeKey(ModAssemblyTracker.TryGetModIdHash(modAsm), typeName, methodName);
            lock (_gate)
            {
                if (_delegates.TryGetValue(key, out Delegate? existing) && existing is T typed)
                {
                    del = typed;
                    return true;
                }
            }

            return false;
        }

        public void Bake(Assembly modAsm, int modIdHash, string typeName, string methodName)
        {
            if (!ModOptimizerBootstrap.IsReflectionCacheActive)
                return;

            if (!UnityMainThread.IsMainThread)
                return;

            long key = MakeKey(modIdHash, typeName, methodName);
            lock (_gate)
            {
                if (_delegates.ContainsKey(key))
                    return;
            }

            try
            {
                Type? type = modAsm.GetType(typeName, throwOnError: false);
                if (type == null)
                    return;

                MethodInfo? method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (method == null || !method.IsStatic)
                    return;

                Delegate del = Delegate.CreateDelegate(typeof(Action), method);
                lock (_gate)
                {
                    _delegates[key] = del;
                }
            }
            catch (Exception ex)
            {
                RingBufferLog.WriteAscii("[ModOpt][WARN] reflection bake failed type=" + typeName
                    + " method=" + methodName + " err=" + ex.Message);
            }
        }

        internal void BakeManifest(Assembly modAsm, int modIdHash, IEnumerable<ReflectionBakeEntry> entries)
        {
            foreach (ReflectionBakeEntry entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Type) || string.IsNullOrEmpty(entry.Method))
                    continue;

                Bake(modAsm, modIdHash, entry.Type, entry.Method);
            }
        }

        internal void BakeAnalyzerHits(Assembly modAsm, int modIdHash, IEnumerable<(string Type, string Method)> hits)
        {
            foreach ((string type, string method) in hits)
                Bake(modAsm, modIdHash, type, method);
        }

        internal void ClearMod(int modIdHash)
        {
            if (modIdHash == 0)
                return;

            lock (_gate)
            {
                var remove = new List<long>();
                foreach (KeyValuePair<long, Delegate> kv in _delegates)
                {
                    if ((int)(kv.Key >> 32) == modIdHash)
                        remove.Add(kv.Key);
                }

                for (int i = 0; i < remove.Count; i++)
                    _delegates.Remove(remove[i]);
            }
        }

        private static long MakeKey(int modIdHash, string typeName, string methodName)
        {
            unchecked
            {
                int typeHash = typeName.GetHashCode();
                int methodHash = methodName.GetHashCode();
                return ((long)modIdHash << 32) | (uint)(typeHash ^ methodHash);
            }
        }
    }
}
#endif
