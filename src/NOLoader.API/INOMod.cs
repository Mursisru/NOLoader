namespace NOLoader.API
{
    public interface INOMod
    {
        void OnLoad(ref NOModContext ctx);
        void OnUnload(ref NOModContext ctx);
    }
}
