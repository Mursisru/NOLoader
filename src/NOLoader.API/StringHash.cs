using System;
using System.Text;

namespace NOLoader.API
{
    /// <summary>MurmurHash3 32-bit — runtime compares ints only.</summary>
    public static class StringHash
    {
        public const uint Seed = 0x9747B28Cu;

        public static int Murmur32(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            byte[] data = Encoding.UTF8.GetBytes(value);
            return (int)Murmur32(data, 0, data.Length, Seed);
        }

        public static uint Murmur32(byte[] data, int offset, int length, uint seed)
        {
            const uint c1 = 0xCC9E2D51u;
            const uint c2 = 0x1B873593u;
            uint hash = seed;
            int roundedEnd = offset + (length & ~3);

            for (int i = offset; i < roundedEnd; i += 4)
            {
                uint k = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                k *= c1;
                k = RotateLeft(k, 15);
                k *= c2;
                hash ^= k;
                hash = RotateLeft(hash, 13);
                hash = hash * 5 + 0xE6546B64u;
            }

            uint tail = 0;
            int rem = length & 3;
            int tailIndex = roundedEnd;
            if (rem == 3) tail ^= (uint)data[tailIndex + 2] << 16;
            if (rem >= 2) tail ^= (uint)data[tailIndex + 1] << 8;
            if (rem >= 1)
            {
                tail ^= data[tailIndex];
                tail *= c1;
                tail = RotateLeft(tail, 15);
                tail *= c2;
                hash ^= tail;
            }

            hash ^= (uint)length;
            hash = FMix32(hash);
            return hash;
        }

        private static uint RotateLeft(uint x, int r) => (x << r) | (x >> (32 - r));

        private static uint FMix32(uint h)
        {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}
