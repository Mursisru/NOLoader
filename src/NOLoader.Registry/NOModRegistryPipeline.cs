using NOLoader.API;

namespace NOLoader.Registry
{
    /// <summary>Full publish pipeline — L3 validation, registry insert, optional Encyclopedia inject.</summary>
    public static class NOModRegistryPipeline
    {
        public static bool TryPublishMissile(ref MissileEntry entry, out string? error, bool applyToEncyclopedia = false)
        {
            if (!ScriptableObjectGateL3.ValidateMissile(ref entry, out error))
                return false;

            if (!NOModRegistry.RegisterMissile(ref entry))
            {
                error = "RegisterMissile failed (duplicate key or registry not initialized)";
                return false;
            }

            if (applyToEncyclopedia && !NOModRegistry.ApplyToEncyclopedia())
            {
                error = "ApplyToEncyclopedia failed after missile publish";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryPublishWeaponMount(ref WeaponMountEntry entry, out string? error, bool applyToEncyclopedia = false)
        {
            if (!ScriptableObjectGateL3.ValidateWeaponMount(ref entry, out error))
                return false;

            if (!NOModRegistry.RegisterWeaponMount(ref entry))
            {
                error = "RegisterWeaponMount failed (duplicate key or registry not initialized)";
                return false;
            }

            if (applyToEncyclopedia && !NOModRegistry.ApplyToEncyclopedia())
            {
                error = "ApplyToEncyclopedia failed after weapon publish";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryPublishAircraft(ref AircraftEntry entry, out string? error, bool applyToEncyclopedia = false)
        {
            if (!ScriptableObjectGateL3.ValidateAircraft(ref entry, out error))
                return false;

            if (!NOModRegistry.RegisterAircraft(ref entry))
            {
                error = "RegisterAircraft failed (duplicate key or registry not initialized)";
                return false;
            }

            if (applyToEncyclopedia && !NOModRegistry.ApplyToEncyclopedia())
            {
                error = "ApplyToEncyclopedia failed after aircraft publish";
                return false;
            }

            error = null;
            return true;
        }

        public static bool PublishAliasesFromEncyclopedia(object encyclopedia, out string? error)
        {
            error = null;
            if (encyclopedia == null)
            {
                error = "encyclopedia is null";
                return false;
            }

            RegistryGameBridge.OnEncyclopediaAfterLoad(encyclopedia);
            return true;
        }
    }
}
