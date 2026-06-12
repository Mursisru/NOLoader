using NOLoader.API.World;

namespace NOLoader.API.World
{
    public readonly struct NOWorldUnit
    {
        public readonly int UnitId;
        public readonly NOVec3 Position;
        public readonly NOVec3 Velocity;
        public readonly bool IsLocal;
        public readonly int TeamId;

        public NOWorldUnit(int unitId, NOVec3 position, NOVec3 velocity, bool isLocal, int teamId)
        {
            UnitId = unitId;
            Position = position;
            Velocity = velocity;
            IsLocal = isLocal;
            TeamId = teamId;
        }
    }
}
