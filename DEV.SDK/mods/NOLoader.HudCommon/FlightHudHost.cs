using System;
using System.Collections.Generic;
using NOLoader.API;
using UnityEngine;

namespace NOLoader.HudCommon
{
    /// <summary>Single FlightHud.Awake hook — dependent mods register controller attach callbacks.</summary>
    public static class FlightHudHost
    {
        private static readonly List<Action<GameObject>> Registrars = new List<Action<GameObject>>();

        public static void Register(Action<GameObject> attachControllers)
        {
            if (attachControllers == null)
                return;

            Registrars.Add(attachControllers);
            TryAttachExisting(attachControllers);
        }

        internal static void InvokeAll(GameObject flightHudGo)
        {
            if (flightHudGo == null)
                return;

            for (int i = 0; i < Registrars.Count; i++)
            {
                try
                {
                    Registrars[i](flightHudGo);
                }
                catch (Exception ex)
                {
#if NOLoader_DEV
                    Debug.LogError("[NOLoader.HudCommon] FlightHudHost registrar failed: " + ex);
#endif
                }
            }
        }

        private static void TryAttachExisting(Action<GameObject> attachControllers)
        {
            try
            {
                var hud = SceneSingleton<FlightHud>.i;
                if (hud?.gameObject != null)
                    attachControllers(hud.gameObject);
            }
            catch
            {
                // FlightHud not ready yet — Awake postfix will attach later.
            }
        }
    }
}
