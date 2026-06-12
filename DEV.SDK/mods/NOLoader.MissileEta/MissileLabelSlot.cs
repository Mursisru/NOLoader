using UnityEngine;
using NOLoader.HudCommon;

namespace NOLoader.MissileEta
{
    internal enum MissileLabelMode
    {
        Hidden,
        OwnOnScreen,
        OwnOffScreen,
        IncomingOnScreen,
        IncomingOffScreen,
    }

    internal struct MissileLabelSlot
    {
        internal Missile Missile;
        internal MissileLabelMode Mode;
        internal Vector2 ScreenPosition;
        internal float AngleDeg;
        internal string EtaText;
        internal string ArhText;
        internal bool ShowArh;
        internal ArhDisplayPhase ArhPhase;
    }
}
