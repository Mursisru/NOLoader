using System;
using System.Reflection;
using TMPro;

namespace NOLoader.RealWeaponNames.Services
{
    internal static class WeaponSelectorUiHelper
    {
        internal static void RefreshDropdownLabels(WeaponSelector selector)
        {
            if (selector == null || !RealWeaponNamesState.IsEnabled || !RealWeaponNamesState.IsSafeForUiPatch())
                return;

            var dropdownField = typeof(WeaponSelector).GetField("dropdown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var dropdown = dropdownField?.GetValue(selector) as TMP_Dropdown;
            if (dropdown == null || dropdown.options == null)
                return;

            for (var i = 0; i < dropdown.options.Count; i++)
            {
                var option = dropdown.options[i];
                if (option == null || string.IsNullOrEmpty(option.text))
                    continue;

                option.text = WeaponDisplayNameResolver.ReplaceInComposite(option.text);
            }

            TryRefreshCaption(dropdown);
            SyncDropdownText(selector, dropdown);
        }

        private static void TryRefreshCaption(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
                return;

            var index = dropdown.value;
            if (index < 0 || index >= dropdown.options.Count)
                return;

            dropdown.RefreshShownValue();
        }

        private static void SyncDropdownText(WeaponSelector selector, TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
                return;

            var index = dropdown.value;
            if (index < 0 || index >= dropdown.options.Count)
                return;

            var dropdownTextField = typeof(WeaponSelector).GetField("dropdownText", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var dropdownText = dropdownTextField?.GetValue(selector) as TextMeshProUGUI;
            if (dropdownText == null)
                return;

            dropdownText.text = dropdown.options[index].text;
        }
    }
}
