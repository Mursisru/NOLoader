#if !NOLoader_DEV
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NOLoader.Core.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOLoader.Core.ModOptimizer
{
    public static class ModOptimizerHooks
    {
        [ThreadStatic] private static string? _lastCallerName;
        [ThreadStatic] private static bool _lastCallerIsMod;

        public static GameObject? FindRedirect(string name)
        {
            if (!RuntimeConfig.ModOptimizerEnabled || !RuntimeConfig.ModSceneLocatorEnabled)
                return ResolveNonModFind(name);

            string callerName = GetCallerAssemblyName();
            if (string.IsNullOrEmpty(callerName) || IsKnownNonModAssemblyName(callerName))
                return ResolveNonModFind(name);

            if (ReferenceEquals(callerName, _lastCallerName))
            {
                if (!_lastCallerIsMod)
                    return ResolveNonModFind(name);
            }
            else
            {
                _lastCallerName = callerName;
                _lastCallerIsMod = ModAssemblyTracker.IsModAssemblyByName(callerName);
                if (!_lastCallerIsMod)
                    return ResolveNonModFind(name);
            }

            if (ModSceneLocator.Instance.TryGet(name, out object cachedObj) && cachedObj is GameObject cached && cached != null)
                return cached;

            GameObject? found = FindByHierarchy(name);
            if (found != null)
                ModSceneLocator.Instance.Register(name, found);

            return found;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetCallerAssemblyName()
        {
            Assembly caller = Assembly.GetCallingAssembly();
            return caller.GetName().Name ?? string.Empty;
        }

        private static bool IsKnownNonModAssemblyName(string name)
        {
            if (ModAssemblyTracker.IsCoreAssemblyName(name))
                return true;

            if (name.StartsWith("UnityEngine.", StringComparison.OrdinalIgnoreCase))
                return true;

            if (name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static GameObject? ResolveNonModFind(string name)
        {
            if (ModNativeGameObjectFind.IsAvailable)
                return ModNativeGameObjectFind.Invoke(name);

            return FindByHierarchy(name);
        }

        private static GameObject? FindByHierarchy(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            int sceneCount = SceneManager.sceneCount;
            for (int s = 0; s < sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    GameObject? hit = FindInHierarchy(roots[r].transform, name);
                    if (hit != null)
                        return hit;
                }
            }

            return null;
        }

        private static GameObject? FindInHierarchy(Transform root, string name)
        {
            if (root == null)
                return null;

            try
            {
                if (string.Equals(root.name, name, StringComparison.Ordinal))
                    return root.gameObject;

                int childCount = root.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    GameObject? childHit = FindInHierarchy(child, name);
                    if (childHit != null)
                        return childHit;
                }
            }
            catch (MissingReferenceException)
            {
                return null;
            }

            return null;
        }
    }
}
#endif
