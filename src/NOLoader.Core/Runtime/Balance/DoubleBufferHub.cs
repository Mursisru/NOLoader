using System.Threading;

namespace NOLoader.Core.Runtime.Balance
{
    /// <summary>Double-buffer swap for main/worker handoff without per-frame locks.</summary>
    internal sealed class DoubleBufferHub<T> where T : class
    {
        private T _a;
        private T _b;
        private int _publishedIndex;
        private int _version;

        internal DoubleBufferHub(T initial)
        {
            _a = initial;
            _b = initial;
        }

        internal int Version => Volatile.Read(ref _version);

        internal void Publish(T snapshot)
        {
            int next = Volatile.Read(ref _publishedIndex) == 0 ? 1 : 0;
            if (next == 0)
                _a = snapshot;
            else
                _b = snapshot;

            Volatile.Write(ref _publishedIndex, next);
            Interlocked.Increment(ref _version);
        }

        internal T ReadPublished()
        {
            return Volatile.Read(ref _publishedIndex) == 0 ? _a : _b;
        }
    }
}
