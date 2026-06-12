using UnityEngine;
using NOLoader.HudCommon;

namespace NOLoader.MissileEta
{
    internal struct ScreenPlacement
    {
        internal bool Valid;
        internal bool OnScreen;
        internal Vector2 ScreenPosition;
        internal Vector2 DirectionFromCenter;
        internal float AngleDeg;
    }

    internal static class HudWorldPlacement
    {
        internal static bool TryWorldToScreen(Camera cam, Vector3 world, out Vector3 screen)
        {
            screen = default;
            if (cam == null)
                return false;

            screen = cam.WorldToScreenPoint(world);
            return screen.z > 0f;
        }

        internal static bool IsOnScreen(Vector2 screenPos, float marginPx)
        {
            float m = HudScreenScale.Px(marginPx);
            return screenPos.x >= m
                && screenPos.y >= m
                && screenPos.x <= Screen.width - m
                && screenPos.y <= Screen.height - m;
        }

        internal static ScreenPlacement Evaluate(
            Camera cam,
            GlobalPosition globalPos,
            float onScreenMarginPx,
            float edgeMarginPx)
        {
            return Evaluate(cam, globalPos.ToLocalPosition(), onScreenMarginPx, edgeMarginPx);
        }

        internal static ScreenPlacement Evaluate(
            Camera cam,
            Vector3 localWorldPoint,
            float onScreenMarginPx,
            float edgeMarginPx)
        {
            var result = new ScreenPlacement();
            if (cam == null || SceneSingleton<CameraStateManager>.i == null)
                return result;

            Vector3 pinnedPos;
            float arrowAngleRad;
            bool pinnedToEdge = HUDFunctions.PinToScreenEdge(localWorldPoint, out pinnedPos, out arrowAngleRad);

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 screenPos = new Vector2(pinnedPos.x, pinnedPos.y);
            Vector2 dir = new Vector2(Mathf.Cos(arrowAngleRad), Mathf.Sin(arrowAngleRad));
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;

            result.Valid = true;
            result.DirectionFromCenter = dir;
            result.AngleDeg = arrowAngleRad * Mathf.Rad2Deg;

            if (!pinnedToEdge && IsOnScreen(screenPos, onScreenMarginPx))
            {
                result.OnScreen = true;
                result.ScreenPosition = screenPos;
                return result;
            }

            result.OnScreen = false;
            float inset = HudScreenScale.Px(edgeMarginPx);
            result.ScreenPosition = InsetFromEdge(screenPos, center, inset);
            return result;
        }

        internal static Vector2 InsetFromEdge(Vector2 edgePos, Vector2 center, float insetPx)
        {
            if (insetPx <= 0f)
                return edgePos;

            Vector2 toEdge = edgePos - center;
            if (toEdge.sqrMagnitude < 0.0001f)
                return edgePos;

            Vector2 inward = -toEdge.normalized;
            return edgePos + inward * insetPx;
        }

        internal static Vector2 ClampToScreenEdge(Vector2 center, Vector2 dir, float marginPx)
        {
            float m = HudScreenScale.Px(marginPx);
            float maxX = Screen.width - m;
            float maxY = Screen.height - m;
            float minX = m;
            float minY = m;

            if (Mathf.Abs(dir.x) < 0.0001f)
                return new Vector2(center.x, dir.y >= 0f ? maxY : minY);

            if (Mathf.Abs(dir.y) < 0.0001f)
                return new Vector2(dir.x >= 0f ? maxX : minX, center.y);

            float tX = dir.x > 0f ? (maxX - center.x) / dir.x : (minX - center.x) / dir.x;
            float tY = dir.y > 0f ? (maxY - center.y) / dir.y : (minY - center.y) / dir.y;
            float t = Mathf.Min(Mathf.Abs(tX), Mathf.Abs(tY));
            return center + dir * t;
        }
    }
}
