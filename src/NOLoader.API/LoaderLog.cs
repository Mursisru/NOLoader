using System;

namespace NOLoader.API
{
    /// <summary>Write diagnostics into NOLoader ring buffer (bound by Core at startup).</summary>
    public static class LoaderLog
    {
        private static Action<string>? _sink;

        public static void Bind(Action<string> sink) => _sink = sink;

        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            _sink?.Invoke(message);
        }

        public static void Phase(string name) => Write("[NOLoader][Diag] === " + name + " ===");

        public static void Pass(string test) => Write("[NOLoader][Diag][PASS] " + test);

        public static void Fail(string test, string reason) => Write("[NOLoader][Diag][FAIL] " + test + " — " + reason);

        public static void Skip(string test, string reason) => Write("[NOLoader][Diag][SKIP] " + test + " — " + reason);

        public static void Info(string message) => Write("[NOLoader][Diag] " + message);
    }
}
