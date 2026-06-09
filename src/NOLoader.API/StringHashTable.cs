using System;
using System.Collections.Generic;

namespace NOLoader.API
{
    /// <summary>DEV-only known string registry for hash decode in overlay (explicit keys only).</summary>
    public static class StringHashTable
    {
        private static readonly Dictionary<int, string> Known = new Dictionary<int, string>();

        /// <summary>When false (DEV default), manifest strings are not auto-registered.</summary>
        public static bool DevAutoRegisterEnabled { get; set; }

        public static void Register(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            Known[StringHash.Murmur32(value)] = value;
        }

        public static void RegisterDevDecodeKeys(IEnumerable<string> keys)
        {
            foreach (string key in keys)
                Register(key);
        }

        public static bool TryDecode(int hash, out string? value)
        {
            if (Known.TryGetValue(hash, out string? found))
            {
                value = found;
                return true;
            }

            value = null;
            return false;
        }

        public static int KnownCount => Known.Count;
    }
}
