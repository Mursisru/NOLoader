using System.Collections.Generic;

namespace NOLoader.Core.Gates
{
    /// <summary>Collects Gate L1/L2 messages for DEV overlay panel and ring log.</summary>
    public static class GateReportStore
    {
        private static readonly List<string> L1Errors = new List<string>();
        private static readonly List<string> L2Errors = new List<string>();

        public static void RecordL1(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            lock (L1Errors)
                L1Errors.Add(message);
        }

        public static void RecordL2(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            lock (L2Errors)
                L2Errors.Add(message);
        }

        public static IReadOnlyList<string> GetL1Errors()
        {
            lock (L1Errors)
                return L1Errors.ToArray();
        }

        public static IReadOnlyList<string> GetL2Errors()
        {
            lock (L2Errors)
                return L2Errors.ToArray();
        }

        public static bool HasL1Errors
        {
            get { lock (L1Errors) return L1Errors.Count > 0; }
        }

        public static void Clear()
        {
            lock (L1Errors) L1Errors.Clear();
            lock (L2Errors) L2Errors.Clear();
        }
    }
}
