#if NOLoader_DEV
using System.Threading;

namespace NOLoader.Core.Development
{
    /// <summary>DEV-only counters for reflection resolves (cached vs cold).</summary>
    public static class ReflectionTracker
    {
        private static int _typeResolves;
        private static int _typeHits;
        private static int _memberResolves;
        private static int _memberHits;

        public static int TypeResolves => Volatile.Read(ref _typeResolves);
        public static int TypeHits => Volatile.Read(ref _typeHits);
        public static int MemberResolves => Volatile.Read(ref _memberResolves);
        public static int MemberHits => Volatile.Read(ref _memberHits);
        public static int TotalResolves => TypeResolves + MemberResolves;

        public static void RecordTypeResolve(string name, bool found)
        {
            Interlocked.Increment(ref _typeResolves);
            if (found)
                Interlocked.Increment(ref _typeHits);
        }

        public static void RecordMemberResolve(string key, bool found)
        {
            Interlocked.Increment(ref _memberResolves);
            if (found)
                Interlocked.Increment(ref _memberHits);
        }
    }
}
#endif
