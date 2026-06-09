using NOLoader.API;

namespace NOLoader.DiagCommon
{
    public sealed class DiagSession
    {
        public int PassCount { get; private set; }
        public int FailCount { get; private set; }
        public int SkipCount { get; private set; }

        public int Total => PassCount + FailCount + SkipCount;
        public bool AllPassed => FailCount == 0;

        public void Pass(string name, string detail = "")
        {
            PassCount++;
            LoaderLog.Pass(name + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }

        public void Fail(string name, string detail = "")
        {
            FailCount++;
            LoaderLog.Fail(name, detail.Length > 0 ? detail : "assertion failed");
        }

        public void Skip(string name, string reason)
        {
            SkipCount++;
            LoaderLog.Skip(name, reason);
        }

        public void Assert(string name, bool ok, string detail = "")
        {
            if (ok)
                Pass(name, detail);
            else
                Fail(name, detail);
        }

        public void LogSummary(string label)
        {
            LoaderLog.Info(label + " — pass=" + PassCount + " fail=" + FailCount + " skip=" + SkipCount + " total=" + Total);
        }
    }
}
