namespace NOLoader.RealWeaponNames
{
    internal static class RealWeaponNamesState
    {
        internal static bool IsEnabled { get; private set; } = true;

        internal static void SetEnabled(bool enabled) => IsEnabled = enabled;

        internal static bool IsSafeForUiPatch()
        {
            return MainMenu.State != MainMenu.LoadingState.Loading;
        }
    }
}
