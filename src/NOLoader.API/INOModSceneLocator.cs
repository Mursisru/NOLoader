namespace NOLoader.API
{
    /// <summary>Name→GameObject registry for mod-spawned scene objects.</summary>
    public interface INOModSceneLocator
    {
        void Register(string name, object go);

        bool TryGet(string name, out object go);

        void Unregister(string name);
    }

    internal sealed class NOModSceneLocatorStub : INOModSceneLocator
    {
        public static readonly NOModSceneLocatorStub Instance = new NOModSceneLocatorStub();

        public void Register(string name, object go) { }

        public bool TryGet(string name, out object go)
        {
            go = null!;
            return false;
        }

        public void Unregister(string name) { }
    }
}
