namespace NOLoader.API.World
{
    public interface INOModWorldReader
    {
        int FrameId { get; }
        int UnitCount { get; }
        NOWorldUnit GetUnit(int index);
    }
}
