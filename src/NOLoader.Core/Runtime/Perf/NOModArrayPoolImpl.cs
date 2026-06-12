using System;
using System.Collections.Generic;
using NOLoader.API;

namespace NOLoader.Core.Runtime.Perf
{
    internal sealed class NOModArrayPoolImpl : INOModArrayPool
    {
        public static readonly NOModArrayPoolImpl Instance = new NOModArrayPoolImpl();
        private readonly Stack<int[]> _intPools = new Stack<int[]>();
        private readonly Stack<float[]> _floatPools = new Stack<float[]>();

        public int[] RentInt(int length)
        {
            if (length <= 0)
                return Array.Empty<int>();
            if (_intPools.Count > 0)
            {
                int[] arr = _intPools.Pop();
                if (arr.Length >= length)
                    return arr;
            }
            return new int[length];
        }

        public float[] RentFloat(int length)
        {
            if (length <= 0)
                return Array.Empty<float>();
            if (_floatPools.Count > 0)
            {
                float[] arr = _floatPools.Pop();
                if (arr.Length >= length)
                    return arr;
            }
            return new float[length];
        }

        public void Return(int[] array)
        {
            if (array == null || array.Length == 0)
                return;
            Array.Clear(array, 0, array.Length);
            if (_intPools.Count < 32)
                _intPools.Push(array);
        }

        public void Return(float[] array)
        {
            if (array == null || array.Length == 0)
                return;
            Array.Clear(array, 0, array.Length);
            if (_floatPools.Count < 32)
                _floatPools.Push(array);
        }
    }
}
