namespace NOLoader.Core.Runtime.Balance
{
    /// <summary>L5: Win32 topology/affinity via C# P/Invoke only — no native DLL required on Win10+.</summary>
    internal static class NativeBalanceNote
    {
        internal const string Implementation = "managed-kernel32";
    }
}
