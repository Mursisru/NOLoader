using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using NOLoader.API;
using UnityEngine;

namespace NOLoader.GpuRenderVerify
{
    internal struct MechanicsSample
    {
        internal float Throttle;
        internal float Speed;
        internal float DisplayDetail;
        internal float CurrentThrust;
        internal bool Ignition;
        internal bool LocalSim;
    }

    internal static class GpuRenderVerifyProbe
    {
        internal static int CountCombatHudMarkers()
        {
            CombatHUD? hud = CombatHUD.i;
            if (hud == null)
                return -1;

            FieldInfo? markersField = typeof(CombatHUD).GetField("markers", BindingFlags.Instance | BindingFlags.NonPublic);
            if (markersField?.GetValue(hud) is not IList markers)
                return 0;

            int visible = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] is not HUDUnitMarker marker)
                    continue;

                if (marker.image == null || !marker.image.enabled)
                    continue;

                if (marker.image.transform == null)
                    continue;

                visible++;
            }

            return visible;
        }

        internal static bool TrySampleLocalAircraft(out MechanicsSample sample)
        {
            sample = default;
            if (!GameManager.GetLocalAircraft(out Aircraft aircraft) || aircraft == null)
                return false;

            ControlInputs inputs = aircraft.GetInputs();
            sample.Throttle = inputs.throttle;
            sample.Speed = aircraft.speed;
            sample.DisplayDetail = aircraft.displayDetail;
            sample.Ignition = aircraft.Ignition;
            sample.LocalSim = aircraft.LocalSim;

            float thrust = 0f;
            if (aircraft.engineStates != null)
            {
                foreach (IEngine engine in aircraft.engineStates)
                {
                    if (engine is IThrustSource source)
                        thrust += source.GetThrust();
                }
            }

            sample.CurrentThrust = thrust;
            return true;
        }

        internal static string FormatMechanics(in MechanicsSample sample)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "thr={0:F2} spd={1:F1} detail={2:F2} thrust={3:F0} ign={4} localSim={5}",
                sample.Throttle, sample.Speed, sample.DisplayDetail, sample.CurrentThrust,
                sample.Ignition, sample.LocalSim);
        }

        internal static string FormatGpuRuntime()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "gpuRuntime={0}",
                NOModRuntime.Gpu != null ? "bound" : "null");
        }
    }
}
