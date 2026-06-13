#if !NOLoader_DEV
using System;
using System.Reflection;
using NOLoader.Core.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOLoader.Core.ModOptimizer
{
    public static class ModOptimizerHooks
    {
        public static GameObject FindRedirect(string name)
        {
            if (!RuntimeConfig.ModOptimizerEnabled || !RuntimeConfig.ModSceneLocatorEnabled)
                return ResolveNonModFind(name);

            Assembly caller = Assembly.GetCallingAssembly();
            if (!ModAssemblyTracker.IsModAssembly(caller))
                return ResolveNonModFind(name);

            if (ModSceneLocator.Instance.TryGet(name, out object cachedObj) && cachedObj is GameObject cached && cached != null)
                return cached;

            GameObject found = FindByHierarchy(name);
            if (found != null)
                ModSceneLocator.Instance.Register(name, found);

            return found;
        }

        private static GameObject ResolveNonModFind(string name)
        {
            if (ModNativeGameObjectFind.IsAvailable)
                return ModNativeGameObjectFind.Invoke(name)!;

            return FindByHierarchy(name);
        }

        private static GameObject FindByHierarchy(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null!;

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

            return null!;
        }

        private static GameObject? FindInHierarchy(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root.gameObject;

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject? childHit = FindInHierarchy(root.GetChild(i), name);
                if (childHit != null)
                    return childHit;
            }

            return null;
        }
    }
}
#endif
