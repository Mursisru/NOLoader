using NOLoader.HudCommon;
namespace NOLoader.MissileLaunchArcHud
{
    internal enum LaunchArcNezPhase
    {
        /// <summary>No valid target / NEZ not shown by vanilla rules.</summary>
        None,

        /// <summary>Far from NEZ — calm arc only (HUD tint).</summary>
        Calm,

        /// <summary>Within approach band before NEZ — red ring on target, arc stays calm.</summary>
        Approaching,

        /// <summary>Inside NEZ — blinking red target rings + arc (War Thunder style).</summary>
        InsideNez,
    }
}
