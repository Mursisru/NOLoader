using System.Reflection;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NOLoader.NVGConfig
{
    internal static class NightVisionFields
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        private static FieldInfo? ResolveField(NightVision instance, string name) =>
            instance.GetType().GetField(name, InstanceNonPublic);

        internal static bool GetNightVisActive(NightVision instance)
        {
            FieldInfo? field = ResolveField(instance, "nightVisActive");
            if (field == null)
                return false;
            object? value = field.GetValue(instance);
            return value is bool active && active;
        }

        internal static Volume? GetPostProcessing(NightVision instance)
        {
            FieldInfo? field = ResolveField(instance, "postProcessing");
            if (field == null)
                return null;
            return field.GetValue(instance) as Volume;
        }

        internal static ColorAdjustments? GetColorAdjustments(NightVision instance)
        {
            FieldInfo? field = ResolveField(instance, "colorAdjustments");
            ColorAdjustments? fromField = field?.GetValue(instance) as ColorAdjustments;
            if (fromField != null)
                return fromField;

            Volume? volume = GetPostProcessing(instance);
            if (volume?.profile == null)
                return null;

            return volume.profile.TryGet(out ColorAdjustments fromProfile) ? fromProfile : null;
        }
    }
}
