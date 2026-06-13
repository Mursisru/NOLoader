namespace NOLoader.Core
{
    public static class AppVersion
    {
        public const string ReleaseBase = "0.1.0";

        // Engine: always DEV. PR-R — only in Desktop\GITHUB local mirror after robocopy.
        public const string VersionChannel = "DEV";
        public const int CycleBuildNumber = 2;
        public const string ChangeLetters = "O";
        public const int SubNumber = 13;

        public static string BuildToken => $"{VersionChannel}{CycleBuildNumber}{ChangeLetters}{SubNumber}";
        public static string Display => $"{ReleaseBase} Build {BuildToken}";
    }
}
