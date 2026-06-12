using NOLoader.API;
using UnityEngine;

namespace NOLoader.MechanicsTest
{
    public sealed class MechanicsTestMod : INOMod
    {
        public void OnLoad(ref NOModContext ctx)
        {
            Debug.Log(string.Format(
                "[NOLoader] MechanicsTest loaded — fireRate x{0:F1}, muzzleVelocity x{1:F1}, missileThrust x{2:F1}",
                MechanicsConfig.FireRateScale,
                MechanicsConfig.MuzzleVelocityScale,
                MechanicsConfig.MissileThrustScale));
        }

        public void OnUnload(ref NOModContext ctx) { }
    }
}
