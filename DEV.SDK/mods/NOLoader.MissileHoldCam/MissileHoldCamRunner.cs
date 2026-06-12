using UnityEngine;

namespace NOLoader.MissileHoldCam
{
    internal sealed class MissileHoldCamRunner : MonoBehaviour
    {
        private void LateUpdate() => MissileHoldCamController.Tick();

        private void OnDestroy() => MissileHoldCamController.Shutdown();
    }
}
