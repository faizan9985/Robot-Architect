using System;
using System.Collections.Generic;
using UnityEngine;
namespace RemixTable
{
    // What the headset knows about the user's real room (from Meta Space Setup), reduced to plain data so the
    // placement rules can be tested without a headset. Built by RemixRoomScanner from AR Foundation planes and
    // bounding boxes; null on RemixTableManager.room while ROOM mode is off.
    public enum RoomSurfaceKind { Floor, Ceiling, Wall, Table, Seat, Other }

    // A flat rectangle in world space: walls, floor, ceiling, table tops.
    [Serializable] public struct RoomSurface
    {
        public RoomSurfaceKind kind;
        public Vector3 center, normal, axisU, axisV; // normal points into the room (up for floors and tables)
        public Vector2 halfSize;                       // meters along axisU / axisV
        public bool invisible;                         // virtual walls closing an open room: block content, never glow
        public static RoomSurface Make(RoomSurfaceKind kind, Vector3 center, Vector3 normal, Vector3 axisU, Vector2 halfSize, bool invisible = false)
        {
            normal = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            axisU = Vector3.ProjectOnPlane(axisU, normal);
            if (axisU.sqrMagnitude < 1e-8f) axisU = Vector3.ProjectOnPlane(Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.right, normal);
            axisU.Normalize();
            return new RoomSurface { kind = kind, center = center, normal = normal, axisU = axisU, axisV = Vector3.Cross(normal, axisU).normalized,
                halfSize = new Vector2(Mathf.Abs(halfSize.x), Mathf.Abs(halfSize.y)), invisible = invisible };
        }
        public float Area => 4 * halfSize.x * halfSize.y;
        public Vector2 ToPlane(Vector3 world) { var d = world - center; return new Vector2(Vector3.Dot(d, axisU), Vector3.Dot(d, axisV)); }
        public Vector3 FromPlane(Vector2 p) => center + axisU * p.x + axisV * p.y;
        public Vector3 ClosestPoint(Vector3 world)
        {
            var p = ToPlane(world);
            return FromPlane(new Vector2(Mathf.Clamp(p.x, -halfSize.x, halfSize.x), Mathf.Clamp(p.y, -halfSize.y, halfSize.y)));
        }
        public bool Raycast(Ray ray, out float distance)
        {
            distance = 0;
            float denom = Vector3.Dot(ray.direction, normal);
            if (Mathf.Abs(denom) < 1e-5f) return false;
            float t = Vector3.Dot(center - ray.origin, normal) / denom;
            if (t <= 0) return false;
            var p = ToPlane(ray.origin + ray.direction * t);
            if (Mathf.Abs(p.x) > halfSize.x || Mathf.Abs(p.y) > halfSize.y) return false;
            distance = t; return true;
        }
    }

    // Furniture volumes (couch, shelf, screen...). Only used to keep the ribbons and orbs out of them.
    [Serializable] public struct RoomBox
    {
        public Vector3 center; public Quaternion rotation; public Vector3 halfExtents; public bool isTable;
        // Highest point of the (possibly tilted) box.
        public float Top => center.y + Mathf.Abs((rotation * Vector3.right).y) * halfExtents.x
                                     + Mathf.Abs((rotation * Vector3.up).y) * halfExtents.y
                                     + Mathf.Abs((rotation * Vector3.forward).y) * halfExtents.z;
        public bool Contains(Vector3 world, float margin = 0)
        {
            var l = Quaternion.Inverse(rotation) * (world - center);
            return Mathf.Abs(l.x) <= halfExtents.x + margin && Mathf.Abs(l.y) <= halfExtents.y + margin && Mathf.Abs(l.z) <= halfExtents.z + margin;
        }
        public bool Raycast(Ray ray, out float distance)
        {
            distance = 0;
            var inv = Quaternion.Inverse(rotation);
            var o = inv * (ray.origin - center); var d = inv * ray.direction;
            float tMin = 0, tMax = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float oi = o[i], di = d[i], h = halfExtents[i];
                if (Mathf.Abs(di) < 1e-6f) { if (oi < -h || oi > h) return false; continue; }
                float t1 = (-h - oi) / di, t2 = (h - oi) / di;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Mathf.Max(tMin, t1); tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax) return false;
            }
            if (tMin <= 0) return false; // Starting inside a box (sitting on the couch) never blocks.
            distance = tMin; return true;
        }
    }

    [Serializable] public sealed class RoomModel
    {
        public List<RoomSurface> surfaces = new List<RoomSurface>();
        public List<RoomBox> boxes = new List<RoomBox>();
        public bool HasFloor { get { foreach (var s in surfaces) if (s.kind == RoomSurfaceKind.Floor) return true; return false; } }
        public float FloorY(float fallback)
        {
            float y = float.MaxValue;
            foreach (var s in surfaces) if (s.kind == RoomSurfaceKind.Floor) y = Mathf.Min(y, s.center.y);
            return y == float.MaxValue ? fallback : y;
        }
        public int Count(RoomSurfaceKind kind) { int n = 0; foreach (var s in surfaces) if (s.kind == kind) n++; return n; }
        public bool IsEmpty => surfaces.Count == 0 && boxes.Count == 0;

        // Distance to the nearest wall or furniture along a ray (walls include invisible ones). MaxValue if open.
        public float Clearance(Ray ray, float ignoreBoxesBelow = float.MinValue)
        {
            float best = float.MaxValue;
            foreach (var s in surfaces)
                if (s.kind == RoomSurfaceKind.Wall && s.Raycast(ray, out float d)) best = Mathf.Min(best, d);
            foreach (var b in boxes)
                if (b.Top > ignoreBoxesBelow && b.Raycast(ray, out float d)) best = Mathf.Min(best, d);
            return best;
        }
        // Nearest visible wall or table within `maxDistance` of a point: where a released orb sticks.
        public bool TryClosestSurface(Vector3 world, float maxDistance, out RoomSurface surface, out Vector3 point)
        {
            surface = default; point = world; float best = maxDistance; bool found = false;
            foreach (var s in surfaces)
            {
                if (s.invisible || (s.kind != RoomSurfaceKind.Wall && s.kind != RoomSurfaceKind.Table)) continue;
                var p = s.ClosestPoint(world); float d = Vector3.Distance(p, world);
                if (d <= best) { best = d; surface = s; point = p; found = true; }
            }
            return found;
        }
    }

    // Where the remix table goes in a real room. The ribbons wrap around the listener's head at `radius`; the
    // UI rows hang below them. Rules: sit squarely at the nearest desk in front of you, lift the rows just
    // clear of the desk top, turn away from walls that would cut through the ribbons, and shrink the arc only
    // when a wall is closer than the ribbons. Anything missing (no scan, no desk, odd furniture) falls back to
    // the normal floating table in front of you.
    public struct RoomPlacementSettings
    {
        public float radius;          // ribbon arc radius (RemixDesign.Radius)
        public float eyeDrop;         // workspace origin below eye level (RemixDesign.workspaceEyeDrop)
        public float contentBottom;   // lowest UI point in workspace-local y (negative)
        public float contentTop;      // highest ribbon point in workspace-local y
        public float deskClearance, comfortLift, maxLift, minScale, minDeskScale, wallMargin, maxDeskReach;
        public static RoomPlacementSettings For(RemixDesign d, float contentBottom, float contentTop) => new RoomPlacementSettings
        {
            comfortLift = .12f, minDeskScale = .75f,
            // Same distance RemixTableManager.HeadAnchorLocal uses, so the arc stays centered on the head.
            radius = d == null ? .8f : d.curvedTimeline ? d.Radius : d.workspaceDistance, eyeDrop = d != null ? d.workspaceEyeDrop : .2f,
            contentBottom = contentBottom, contentTop = contentTop,
            deskClearance = .03f, maxLift = .18f, minScale = .7f, wallMargin = .1f, maxDeskReach = 1.1f,
        };
    }

    public struct RoomPlan
    {
        public Pose pose;            // workspace pose
        public float scale;          // uniform workspace scale (1 = designed size)
        public float lift;           // meters the table was raised to clear the desk
        public int desk;             // index into RoomModel.surfaces, or -1
        public bool deskTooHigh;     // a desk was found but the UI could not be lifted fully clear of it
        public bool tight;           // walls are closer than even the smallest arc
        public float clearance;      // nearest wall around the arc (meters from the head)
        public Vector3 forward;
        public string Summary
        {
            get
            {
                string where = desk >= 0 ? (deskTooHigh ? "SET AT YOUR DESK (DESK IS HIGH)" : "SET ON YOUR DESK") : "FLOATING IN FRONT OF YOU (NO DESK NEARBY)";
                if (tight) return where + "  ·  TIGHT SPACE";
                return scale < .995f ? $"{where}  ·  RESIZED {Mathf.RoundToInt(scale * 100)}% TO FIT" : where;
            }
        }
    }

    public static class RoomPlacement
    {
        // Workspace pose that centers the ribbon arc on `headAnchor` (the default layout centers it on the eye).
        public static Pose WorkspacePose(Vector3 headAnchor, Vector3 forward, float scale, Vector3 headAnchorLocal)
        {
            var rotation = Quaternion.LookRotation(Flat(forward, Vector3.forward), Vector3.up);
            return new Pose(headAnchor - rotation * (headAnchorLocal * scale), rotation);
        }

        public static RoomPlan Solve(RoomModel room, Vector3 eye, Vector3 gaze, RoomPlacementSettings s)
        {
            var headLocal = new Vector3(0, s.eyeDrop, -s.radius);
            var plan = new RoomPlan { desk = -1, scale = 1, forward = Flat(gaze, Vector3.forward), clearance = float.MaxValue };
            if (room == null || room.IsEmpty || !Valid(eye) || !Valid(gaze))
            {
                plan.pose = WorkspacePose(eye, plan.forward, 1, headLocal);
                return plan;
            }
            float floorY = room.FloorY(eye.y - 1.25f);
            plan.desk = FindDesk(room, eye, plan.forward, floorY, s.maxDeskReach);
            var desired = plan.desk >= 0 ? DeskFacing(room.surfaces[plan.desk], eye, plan.forward) : plan.forward;

            // Turn up to 30 degrees away from walls that would cut the arc; prefer the desired facing.
            float bestScore = float.MinValue; var bestForward = desired; float bestClearance = float.MaxValue;
            for (int step = -6; step <= 6; step++)
            {
                var f = Quaternion.AngleAxis(step * 5, Vector3.up) * desired;
                float c = ArcClearance(room, eye, f, s, plan.desk >= 0 ? room.surfaces[plan.desk].center.y : float.MinValue);
                float score = Mathf.Min(c, s.radius + s.wallMargin) - .004f * Mathf.Abs(step * 5);
                if (score > bestScore + 1e-4f) { bestScore = score; bestForward = f; bestClearance = c; }
            }
            plan.forward = bestForward; plan.clearance = bestClearance;
            if (bestClearance < s.radius + s.wallMargin)
            {
                float fit = (bestClearance - s.wallMargin) / s.radius;
                plan.scale = Mathf.Clamp(fit, s.minScale, 1);
                plan.tight = fit < s.minScale;
            }

            // Lift the rows clear of the desk where they hang over it, within a comfortable limit.
            if (plan.desk >= 0)
            {
                var desk = room.surfaces[plan.desk];
                var front = eye + plan.forward * (s.radius * plan.scale);
                var onDesk = desk.ToPlane(new Vector3(front.x, desk.center.y, front.z));
                bool over = Mathf.Abs(onDesk.x) <= desk.halfSize.x + .06f && Mathf.Abs(onDesk.y) <= desk.halfSize.y + .06f;
                if (over)
                {
                    // Lift needed for the lowest row to clear the desk at scale k: a + k * b.
                    float a = desk.center.y + s.deskClearance - eye.y, b = s.eyeDrop - s.contentBottom;
                    float atScale = a + plan.scale * b;
                    if (atScale > s.comfortLift && b > 1e-4f)
                    {
                        // Raising the whole table that far would put the top rows above the eyes; shrink it
                        // toward the head instead (it scales about the ear-level anchor) until a small lift is enough.
                        float k = (s.comfortLift - a) / b;
                        plan.scale = Mathf.Min(plan.scale, Mathf.Max(s.minDeskScale, k));
                        atScale = a + plan.scale * b;
                    }
                    plan.lift = Mathf.Clamp(atScale, 0, s.maxLift);
                    plan.deskTooHigh = atScale > s.maxLift + 1e-4f;
                }
            }
            plan.pose = WorkspacePose(eye + Vector3.up * plan.lift, plan.forward, plan.scale, headLocal);
            return plan;
        }

        // The table surface the user is sitting at: in front (within ~100 degrees of gaze), within reach,
        // at desk height, and big enough to work at. Plane and box versions of one desk collapse naturally
        // because the closer / better-facing one wins.
        public static int FindDesk(RoomModel room, Vector3 eye, Vector3 gaze, float floorY, float maxReach)
        {
            int best = -1; float bestScore = float.MaxValue; var flatGaze = Flat(gaze, Vector3.forward);
            for (int i = 0; i < room.surfaces.Count; i++)
            {
                var s = room.surfaces[i];
                if (s.kind != RoomSurfaceKind.Table || s.normal.y < .8f) continue;
                float height = s.center.y - floorY;
                if (height < .35f || height > 1.25f || s.center.y > eye.y - .05f) continue;
                if (Mathf.Min(s.halfSize.x, s.halfSize.y) < .12f || s.Area < .12f) continue;
                var near = s.ClosestPoint(new Vector3(eye.x, s.center.y, eye.z));
                var toNear = near - eye; toNear.y = 0; float reach = toNear.magnitude;
                if (reach > maxReach) continue;
                var toCenter = s.center - eye; toCenter.y = 0;
                float angle = toCenter.sqrMagnitude < 1e-4f ? 0 : Vector3.Angle(flatGaze, toCenter);
                if (angle > 100) continue;
                float score = reach + angle / 180f * .8f;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        // Face across the desk from the edge nearest the user, the way you sit at a desk. If the user is above
        // the desk (bed tray, leaning over) keep their gaze.
        public static Vector3 DeskFacing(RoomSurface desk, Vector3 eye, Vector3 gaze)
        {
            var p = desk.ToPlane(eye);
            float dx = desk.halfSize.x - Mathf.Abs(p.x), dy = desk.halfSize.y - Mathf.Abs(p.y);
            if (dx > 0 && dy > 0) return Flat(gaze, Vector3.forward);
            Vector3 inward;
            if (dx <= 0 && (dy > 0 || -dx >= -dy)) inward = -desk.axisU * Mathf.Sign(p.x);
            else inward = -desk.axisV * Mathf.Sign(p.y);
            var f = Flat(inward, gaze);
            // A corner seat toward a long desk: don't swing more than 60 degrees from where the user looks.
            float angle = Vector3.SignedAngle(Flat(gaze, f), f, Vector3.up);
            if (Mathf.Abs(angle) > 60) f = Quaternion.AngleAxis(Mathf.Sign(angle) * 60, Vector3.up) * Flat(gaze, f);
            return f;
        }

        // Nearest obstacle around the ribbon arc (180 degrees, at ribbon heights), measured from the head.
        public static float ArcClearance(RoomModel room, Vector3 eye, Vector3 forward, RoomPlacementSettings s, float deskTop)
        {
            float best = float.MaxValue;
            float low = eye.y - s.eyeDrop + Mathf.Min(0, s.contentBottom) * .5f, high = eye.y - s.eyeDrop + s.contentTop;
            for (int a = -90; a <= 90; a += 15)
            {
                var dir = Quaternion.AngleAxis(a, Vector3.up) * forward;
                foreach (float y in new[] { low, (low + high) * .5f, high })
                {
                    // The user's own desk sits under the rows once they are lifted; it never blocks the arc.
                    float c = room.Clearance(new Ray(new Vector3(eye.x, y, eye.z), dir), deskTop + .02f);
                    best = Mathf.Min(best, c);
                }
            }
            return best;
        }

        // ROOM + SPATIAL: hang each stem on a real wall around the listener instead of at arm's length.
        // Directions follow the default fan (vocals ahead, drums and bass to the sides, melody behind-left).
        // A stem stays at its normal spot when the wall in that direction is too close or too far.
        public static Vector3[] SpeakerSpots(RoomModel room, Vector3 eye, Vector3 forward, float minDistance, float maxDistance = 4f, float offWall = .14f)
        {
            float[] angles = new float[StemMix.StemCount]; float[] heights = new float[StemMix.StemCount];
            var defaults = StemMix.DefaultPositions();
            for (int i = 0; i < angles.Length; i++) { angles[i] = Mathf.Atan2(defaults[i].x, defaults[i].z) * Mathf.Rad2Deg; heights[i] = defaults[i].y; }
            var rotation = Quaternion.LookRotation(Flat(forward, Vector3.forward), Vector3.up);
            var spots = new Vector3[StemMix.StemCount];
            for (int i = 0; i < spots.Length; i++)
            {
                spots[i] = eye + rotation * defaults[i];
                if (room == null) continue;
                // Try the stem's own direction first, then fan out a little to find a usable wall.
                foreach (float nudge in new[] { 0f, 15f, -15f, 30f, -30f })
                {
                    var dir = rotation * (Quaternion.AngleAxis(angles[i] + nudge, Vector3.up) * Vector3.forward);
                    var origin = eye + Vector3.up * heights[i];
                    float wall = float.MaxValue; var normal = Vector3.zero;
                    foreach (var s in room.surfaces)
                        if (s.kind == RoomSurfaceKind.Wall && !s.invisible && s.Raycast(new Ray(origin, dir), out float d) && d < wall) { wall = d; normal = s.normal; }
                    float blocked = room.Clearance(new Ray(origin, dir));
                    if (wall == float.MaxValue || wall > maxDistance || blocked < wall - .05f) continue; // open side, or furniture in the way
                    // Hang it a hand's width off the wall surface (along the wall's normal, not the ray).
                    var spot = origin + dir * wall + normal * offWall;
                    var flat = spot - eye; flat.y = 0;
                    if (flat.magnitude < minDistance) continue;
                    spots[i] = spot; break;
                }
            }
            return spots;
        }

        public static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0;
            if (v.sqrMagnitude < 1e-6f || !Valid(v)) { fallback.y = 0; return fallback.sqrMagnitude < 1e-6f ? Vector3.forward : fallback.normalized; }
            return v.normalized;
        }
        static bool Valid(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
