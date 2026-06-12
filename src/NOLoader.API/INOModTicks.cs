namespace NOLoader.API
{
    public interface INOModTickFast
    {
        void OnFastUpdate(ref NOModContext ctx, float dt);
    }

    public interface INOModTickNormal
    {
        void OnNormalUpdate(ref NOModContext ctx, float dt);
    }

    public interface INOModTickSlow
    {
        void OnSlowUpdate(ref NOModContext ctx, float dt);
    }
}
