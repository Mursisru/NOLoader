using System.Threading;

namespace NOLoader.LoaderLab
{
    public static class LoaderLabState
    {
        public static volatile bool Active;

        public static int PhysicsCatchCount;

        public static void RecordPhysicsCatch() =>
            Interlocked.Increment(ref PhysicsCatchCount);
    }
}
