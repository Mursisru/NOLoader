using NOLoader.API.World;

namespace NOLoader.API
{
    /// <summary>Per-frame cached world/camera reads — avoids repeated native transform queries.</summary>
    public interface INOModFrameCache
    {
        int FrameId { get; }
        bool TryGetCameraPosition(out NOVec3 position);
        bool TryGetLocalAircraftPosition(out NOVec3 position);
    }
}
