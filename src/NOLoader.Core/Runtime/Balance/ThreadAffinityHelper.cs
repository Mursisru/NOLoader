using System;
using System.Runtime.InteropServices;

namespace NOLoader.Core.Runtime.Balance
{
    internal static class ThreadAffinityHelper
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        internal static bool TryPinCurrentThread(ulong affinityMask)
        {
            if (affinityMask == 0)
                return false;

            try
            {
                UIntPtr result = SetThreadAffinityMask(GetCurrentThread(), new UIntPtr(affinityMask));
                return result != UIntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }
    }
}
