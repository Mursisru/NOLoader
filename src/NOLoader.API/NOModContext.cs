using System;

namespace NOLoader.API
{
    public struct NOModContext
    {
        public string GameRoot;
        public string ModRoot;
        public string ModId;
        public string ModVersion;
        public int ModIdHash;
        public LoadStage Stage;
        public IntPtr NativeHandle;
    }

    public enum LoadStage
    {
        PreMenu = 0,
        MainMenu = 1,
        Mission = 2
    }
}
