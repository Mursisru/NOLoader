using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace NOLoader.HudCommon
{
    public static class HudScreenPlacement
    {
        private static readonly Vector3 ScreenScale = new Vector3(1f, 1f, 0f);
        private static string _lastParentLog;

        public static Transform ResolveHudParent()
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud != null && combatHud.iconLayer != null)
            {
                LogParentOnce("iconLayer", combatHud.iconLayer);
                return combatHud.iconLayer;
            }

            if (combatHud != null && combatHud.iconLayer == null)
                HudDiagLog.VerboseLine("ResolveHudParent: CombatHUD exists but iconLayer=null");

            FlightHud fh = SceneSingleton<FlightHud>.i;
            if (fh != null)
            {
                Transform center = fh.GetHUDCenter();
                if (center != null)
                {
                    LogParentOnce("FlightHud.GetHUDCenter", center);
                    return center;
                }

                HudDiagLog.VerboseLine("ResolveHudParent: FlightHud exists but GetHUDCenter=null");
            }
            else
            {
                HudDiagLog.VerboseLine("ResolveHudParent: FlightHud=null");
            }

            HudDiagLog.Warn("ResolveHudParent: no HUD parent found");
            return null;
        }

        public static void LogCanvasChain(Transform t)
        {
            if (!HudDiagLog.Verbose || t == null)
                return;

            Canvas canvas = t.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                HudDiagLog.Warn($"CanvasChain: NO Canvas above '{t.name}'");
                return;
            }

            HudDiagLog.VerboseLine(
                $"CanvasChain: parent='{t.name}' canvas='{canvas.name}' mode={canvas.renderMode} " +
                $"enabled={canvas.enabled} scale={canvas.transform.lossyScale}");
        }

        public static bool TryGetMissileScreenPosition(Camera cam, Missile missile, out Vector2 screen)
        {
            screen = default;
            if (cam == null || missile == null)
                return false;

            Vector3 local = missile.GlobalPosition().ToLocalPosition();
            Vector3 sp = cam.WorldToScreenPoint(local);
            if (sp.z <= 0f)
                return false;

            screen = new Vector2(sp.x, sp.y);
            return true;
        }

        public static Vector3 ToScreenTransformPosition(Camera cam, Vector3 worldOrLocalPoint)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldOrLocalPoint);
            return Vector3.Scale(sp, ScreenScale);
        }

        public static Vector3 ToScreenTransformPosition(Camera cam, GlobalPosition global)
        {
            return ToScreenTransformPosition(cam, global.ToLocalPosition());
        }

        public static void PlaceTransform(Transform t, Vector2 screenPosition)
        {
            if (t == null)
                return;
            t.position = new Vector3(screenPosition.x, screenPosition.y, 0f);
            HudDiagLog.VerboseLine($"PlaceTransform: '{t.name}' -> ({screenPosition.x:F0},{screenPosition.y:F0}) worldPos={t.position}");
        }

        public static void PlaceTransform(Transform t, Camera cam, GlobalPosition global)
        {
            Vector3 sp = cam.WorldToScreenPoint(global.ToLocalPosition());
            t.position = Vector3.Scale(sp, ScreenScale);
        }

        public static Transform ResolveMarkersParent(Transform flightHudCanvasFallback)
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud != null)
            {
                Image marker = FindPrimaryTargetMarkerImage(combatHud, null);
                if (marker != null)
                {
                    Canvas canvas = marker.GetComponentInParent<Canvas>();
                    if (canvas != null)
                        return canvas.transform;
                    if (marker.rectTransform.parent != null)
                        return marker.rectTransform.parent;
                }
            }

            if (combatHud != null && combatHud.targetDesignator != null)
            {
                Canvas canvas = combatHud.targetDesignator.GetComponentInParent<Canvas>();
                if (canvas != null)
                    return canvas.transform;
            }

            return flightHudCanvasFallback;
        }

        public static bool TryGetTargetHudPosition(CombatHUD combatHud, Unit target, Camera worldCamera, out Vector3 screenPosition)
        {
            return TryGetPrimaryTargetHudPosition(combatHud, target, worldCamera, out screenPosition);
        }

        public static bool TryGetPrimaryTargetHudPosition(CombatHUD combatHud, Unit primaryTarget, Camera worldCamera, out Vector3 screenPosition)
        {
            screenPosition = default;
            if (combatHud == null || primaryTarget == null)
                return false;

            Image marker = FindPrimaryTargetMarkerImage(combatHud, primaryTarget);
            if (marker != null && marker.enabled && marker.gameObject.activeInHierarchy)
            {
                screenPosition = marker.rectTransform.position;
                return true;
            }

            if (worldCamera == null)
                return false;

            GlobalPosition gp = primaryTarget.GlobalPosition();
            Vector3 sp = worldCamera.WorldToScreenPoint(gp.ToLocalPosition());
            if (sp.z <= 0f)
                return false;

            screenPosition = new Vector3(sp.x, sp.y, 0f);
            return true;
        }

        private static Image FindPrimaryTargetMarkerImage(CombatHUD combatHud, Unit primaryTarget)
        {
            FieldInfo markersField = typeof(CombatHUD).GetField(
                "markers",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var markersObj = markersField?.GetValue(combatHud);
            var markers = markersObj as IList;
            if (markers == null || markers.Count == 0)
                return null;

            if (primaryTarget != null)
            {
                for (int i = 0; i < markers.Count; i++)
                {
                    var hudMarker = markers[i] as HUDUnitMarker;
                    if (hudMarker == null || hudMarker.unit != primaryTarget)
                        continue;
                    if (hudMarker.image != null)
                        return hudMarker.image;
                }
            }

            for (int i = 0; i < markers.Count; i++)
            {
                var hudMarker = markers[i] as HUDUnitMarker;
                if (hudMarker?.image == null || !hudMarker.image.enabled)
                    continue;
                return hudMarker.image;
            }

            return null;
        }

        private static void LogParentOnce(string source, Transform parent)
        {
            string key = source + ":" + parent.GetInstanceID();
            if (_lastParentLog == key)
                return;
            _lastParentLog = key;
            HudDiagLog.Info($"ResolveHudParent: using {source} '{parent.name}' active={parent.gameObject.activeInHierarchy}");
            LogCanvasChain(parent);
        }
    }
}
