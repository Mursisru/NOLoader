namespace NOLoader.Core
{
    public static class AppVersion
    {
        public const string ReleaseBase = "0.1.0";

#if NOLoader_DEV
        public const string VersionChannel = "DEV";
        public const int CycleBuildNumber = 2;
        public const string ChangeLetters = "PM";
        public const int SubNumber = 1;
#else
        public const string VersionChannel = "RDY";
        public const int CycleBuildNumber = 1;
        public const string ChangeLetters = "R";
        public const int SubNumber = 6;
#endif

        public static string BuildToken => $"{VersionChannel}{CycleBuildNumber}{ChangeLetters}{SubNumber}";
        public static string Display => $"{ReleaseBase} Build {BuildToken}";
    }
}
