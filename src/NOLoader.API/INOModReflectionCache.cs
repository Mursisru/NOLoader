using System;
using System.Reflection;

namespace NOLoader.API
{
    /// <summary>Opt-in reflection delegate cache — main thread only.</summary>
    public interface INOModReflectionCache
    {
        bool TryGetDelegate<T>(Assembly modAsm, string typeName, string methodName, out T del) where T : Delegate;

        void Bake(Assembly modAsm, int modIdHash, string typeName, string methodName);
    }

    internal sealed class NOModReflectionCacheStub : INOModReflectionCache
    {
        public static readonly NOModReflectionCacheStub Instance = new NOModReflectionCacheStub();

        public bool TryGetDelegate<T>(Assembly modAsm, string typeName, string methodName, out T del) where T : Delegate
        {
            del = null!;
            return false;
        }

        public void Bake(Assembly modAsm, int modIdHash, string typeName, string methodName) { }
    }
}
