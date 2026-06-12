using NOLoader.API;
using UnityEngine;

namespace NOLoader.FaultMission
{
    /// <summary>Gate L4 negative-test — loads at MainMenu, throws from host Start().</summary>
    public sealed class FaultMissionEntry : INOMod
    {
        public void OnLoad(ref NOModContext ctx)
        {
            if (GameObject.Find("NOLoader.FaultMission.Host") != null)
                return;

            var host = new GameObject("NOLoader.FaultMission.Host");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<FaultMissionHost>();
        }

        public void OnUnload(ref NOModContext ctx)
        {
        }
    }

    internal sealed class FaultMissionHost : MonoBehaviour
    {
        private void Start()
        {
            throw new System.InvalidOperationException("Gate L4 negative test fault");
        }
    }
}
