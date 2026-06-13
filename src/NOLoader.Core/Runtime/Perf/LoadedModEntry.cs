using NOLoader.API;
using NOLoader.Core.Mods;

namespace NOLoader.Core.Runtime.Perf
{
    internal sealed class LoadedModEntry
    {
        public LoadedMod Mod = null!;
        public INOModTickFast? Fast;
        public INOModTickNormal? Normal;
        public INOModTickSlow? Slow;
        public INOModBackgroundWork? Background;
        public int DemoteLevel;
        public ModWorkInput PendingInput;
        public ModWorkOutput PendingOutput;
        public bool HasPendingApply;
        public bool OffloadActive;
    }
}
