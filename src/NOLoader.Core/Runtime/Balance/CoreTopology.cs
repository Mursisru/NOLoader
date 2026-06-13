using System;
using System.Runtime.InteropServices;

namespace NOLoader.Core.Runtime.Balance
{
    internal sealed class CoreTopology
    {
        private const int RelationProcessorCore = 0;
        private const int RelationProcessorPackage = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            public int Relationship;
            public int Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSOR_RELATIONSHIP
        {
            public byte Flags;
            public byte EfficiencyClass;
            public ushort Reserved;
            public uint GroupCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(
            int relationship,
            IntPtr buffer,
            ref int returnedLength);

        internal int LogicalCount { get; private set; }
        internal int PhysicalCount { get; private set; }
        internal bool IsHybrid { get; private set; }
        internal int PerformanceCoreCount { get; private set; }
        internal int EfficientCoreCount { get; private set; }
        internal ulong ModWorkerAffinityMask { get; private set; }
        internal ulong MainThreadAffinityMask { get; private set; }
        internal ulong BackgroundAffinityMask { get; private set; }

        internal static CoreTopology Detect(int workerCount)
        {
            var topo = new CoreTopology();
            topo.LogicalCount = Math.Max(1, Environment.ProcessorCount);
            topo.PhysicalCount = topo.LogicalCount;
            topo.TryDetectHybrid();

            if (topo.PhysicalCount <= 0)
                topo.PhysicalCount = topo.LogicalCount;

            topo.BuildAffinityMasks(workerCount);
            return topo;
        }

        private void TryDetectHybrid()
        {
            int perf = 0;
            int eff = 0;
            int physical = 0;

            try
            {
                int length = 0;
                GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
                if (length <= 0)
                    return;

                IntPtr buffer = Marshal.AllocHGlobal(length);
                try
                {
                    if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
                        return;

                    int offset = 0;
                    while (offset < length)
                    {
                        var header = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>(
                            buffer + offset);
                        if (header.Relationship == RelationProcessorCore && header.Size >= 20)
                        {
                            physical++;
                            var rel = Marshal.PtrToStructure<PROCESSOR_RELATIONSHIP>(buffer + offset + 8);
                            if (rel.EfficiencyClass >= 1)
                                perf++;
                            else
                                eff++;
                        }

                        offset += header.Size > 0 ? header.Size : 8;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return;
            }

            if (physical > 0)
                PhysicalCount = physical;

            if (perf > 0 && eff > 0)
            {
                IsHybrid = true;
                PerformanceCoreCount = perf;
                EfficientCoreCount = eff;
            }
        }

        private void BuildAffinityMasks(int workerCount)
        {
            int logical = LogicalCount;
            if (workerCount < 1)
                workerCount = Math.Max(1, logical / 4);
            if (workerCount > 8)
                workerCount = 8;

            ulong allMask = logical >= 64 ? ulong.MaxValue : ((1UL << logical) - 1UL);

            if (IsHybrid && PerformanceCoreCount > 0)
            {
                int pBits = Math.Min(PerformanceCoreCount, logical);
                ulong pMask = pBits >= 64 ? ulong.MaxValue : ((1UL << pBits) - 1UL);
                MainThreadAffinityMask = pMask & 0x3UL;
                if (MainThreadAffinityMask == 0)
                    MainThreadAffinityMask = 1UL;

                int eStart = pBits;
                int eCount = Math.Max(1, logical - eStart);
                BackgroundAffinityMask = BuildContiguousMask(eStart, eCount, logical);

                int modStart = Math.Max(pBits / 2, 1);
                ModWorkerAffinityMask = BuildContiguousMask(modStart, workerCount, logical) & ~MainThreadAffinityMask;
            }
            else
            {
                int half = Math.Max(1, logical / 2);
                MainThreadAffinityMask = BuildContiguousMask(0, Math.Min(2, logical), logical);
                ModWorkerAffinityMask = BuildContiguousMask(half, workerCount, logical);
                BackgroundAffinityMask = ModWorkerAffinityMask;
            }

            if (ModWorkerAffinityMask == 0)
                ModWorkerAffinityMask = allMask & ~MainThreadAffinityMask;
            if (ModWorkerAffinityMask == 0)
                ModWorkerAffinityMask = allMask;

            if (BackgroundAffinityMask == 0)
                BackgroundAffinityMask = ModWorkerAffinityMask;
        }

        private static ulong BuildContiguousMask(int start, int count, int logical)
        {
            ulong mask = 0;
            for (int i = 0; i < count; i++)
            {
                int bit = start + i;
                if (bit >= logical || bit >= 64)
                    break;
                mask |= 1UL << bit;
            }

            return mask;
        }

        internal ulong ParseMask(string value, ulong autoMask)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
                return autoMask;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out ulong hex))
                    return hex;
            }

            if (ulong.TryParse(value, out ulong dec))
                return dec;

            return autoMask;
        }
    }
}
