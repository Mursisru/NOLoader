using NOLoader.API;
using NOLoader.ModConfig;
using UnityEngine;

namespace NOLoader.NVGConfig
{
    public sealed class NVGConfigMod : INOMod
    {
        private const string DefaultIni = @"[Settings]
; GreenPhosphor, WhitePhosphor, Monochrome, FullColor, AlienTechnology, Custom
FilterMode=GreenPhosphor

[Custom Filter]
; Saturation and contrast: -100 to 100
Saturation=0
Contrast=0
; Colour filter channels: 0 to 1
ColourFilterR=1
ColourFilterG=1
ColourFilterB=1
";

        public void OnLoad(ref NOModContext ctx)
        {
            ModIniConfig.EnsureDefault(ctx.ModRoot, DefaultIni);
            NVGConfigCache.Init(ctx.ModRoot);
            NVGConfigDriver.Ensure();
#if NOLoader_DEV
            Debug.Log("[NOLoader] NVGConfig loaded");
#endif
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }
    }
}
