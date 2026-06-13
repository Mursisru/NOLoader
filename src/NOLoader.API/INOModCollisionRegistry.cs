namespace NOLoader.API
{
    public enum ModCollisionProfile
    {
        Projectile,
        Debris,
        VisualOnly
    }

    /// <summary>Opt-in collision layer presets for mod-spawned objects.</summary>
    public interface INOModCollisionRegistry
    {
        void RegisterProjectile(object go, ModCollisionProfile profile);

        void Unregister(object go);
    }

    internal sealed class NOModCollisionRegistryStub : INOModCollisionRegistry
    {
        public static readonly NOModCollisionRegistryStub Instance = new NOModCollisionRegistryStub();

        public void RegisterProjectile(object go, ModCollisionProfile profile) { }

        public void Unregister(object go) { }
    }
}
