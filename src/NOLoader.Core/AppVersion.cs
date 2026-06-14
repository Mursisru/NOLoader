namespace NOLoader.Core
{
    public static class AppVersion
    {
        public const string ReleaseBase = "0.1.0";
        public const string ProductChannel = "RDYTU.mini";

        public static string Display => $"{ReleaseBase} {ProductChannel}";
    }
}
