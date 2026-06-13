using System.Globalization;

namespace NOLoader.MechanicsVerify
{
    internal struct MechanicsSample
    {
        internal float Throttle;
        internal float Brake;
        internal float CustomAxis1;
        internal float Speed;
        internal float DisplayDetail;
        internal float CurrentThrust;
        internal float MaxThrust;
        internal bool Ignition;
        internal bool LocalSim;
        internal bool HasMaxThrust;
    }

    internal static class MechanicsVerifyProbe
    {
        internal static bool TrySampleLocalAircraft(out MechanicsSample sample)
        {
            sample = default;
            if (!GameManager.GetLocalAircraft(out Aircraft aircraft) || aircraft == null)
                return false;

            ControlInputs inputs = aircraft.GetInputs();
            sample.Throttle = inputs.throttle;
            sample.Brake = inputs.brake;
            sample.CustomAxis1 = inputs.customAxis1;
            sample.Speed = aircraft.speed;
            sample.DisplayDetail = aircraft.displayDetail;
            sample.Ignition = aircraft.Ignition;
            sample.LocalSim = aircraft.LocalSim;
            sample.HasMaxThrust = aircraft.GetMaxThrust(out float maxThrust);
            sample.MaxThrust = maxThrust;

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

        internal static string FormatSample(in MechanicsSample sample)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "thr={0:F2} brake={1:F2} cax1={2:F2} spd={3:F1} detail={4:F2} thrust={5:F0} max={6:F0} ign={7} localSim={8}",
                sample.Throttle,
                sample.Brake,
                sample.CustomAxis1,
                sample.Speed,
                sample.DisplayDetail,
                sample.CurrentThrust,
                sample.MaxThrust,
                sample.Ignition,
                sample.LocalSim);
        }
    }
}
