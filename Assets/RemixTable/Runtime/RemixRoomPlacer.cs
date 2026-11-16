using UnityEngine;
namespace RemixTable
{
    // ROOM mode orchestration, independent of where the room data comes from (RemixRoomScanner feeds it the
    // Quest's Space Setup scan; tests and the editor preview feed it synthetic rooms):
    //  - glides the table onto the user's desk (RoomPlacement), resized if walls are close;
    //  - hangs the SPATIAL stems on real walls when they are still at their default spots;
    //  - lights the room (RemixRoomVisuals) and says what happened on the TOOLS status line;
    //  - on Release, glides the table back in front of the user and takes the stems back off the walls.
    public sealed class RemixRoomPlacer : MonoBehaviour
    {
        public RemixTableManager manager;
        public RemixRoomVisuals visuals;
        public RemixToolbox toolbox;
        [Min(.05f)] public float moveSeconds = 1.1f;

        public RoomPlan Plan { get; private set; }
        public bool Placed => manager != null && manager.room != null;
        public bool Moving => moveT < 1;

        Pose fromPose, toPose; float fromScale = 1, toScale = 1, moveT = 1;
        readonly Vector3[] appliedSpots = new Vector3[StemMix.StemCount];
        bool spotsApplied; float radius = .8f;

        // Lowest and highest points of the table (UI rows and ribbons) in workspace-local meters.
        public void ContentExtents(out float bottom, out float top)
        {
            var d = manager.design;
            bottom = d != null ? d.LaneY(3) - d.laneHeight * .5f : -.2f;
            top = d != null ? d.LaneY(0) + d.laneHeight * .5f : .2f;
            if (manager.workspace == null) return;
            var corners = new Vector3[4];
            foreach (var canvas in manager.workspace.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas) continue;
                ((RectTransform)canvas.transform).GetWorldCorners(corners);
                foreach (var c in corners)
                {
                    float y = manager.workspace.InverseTransformPoint(c).y;
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
            }
        }

        public RoomPlan Place(RoomModel room, Vector3 eye, Vector3 gaze, bool announce = true)
        {
            if (manager == null || manager.workspace == null || room == null) return default;
            ContentExtents(out float bottom, out float top);
            var settings = RoomPlacementSettings.For(manager.design, bottom, top);
            var plan = RoomPlacement.Solve(room, eye, gaze, settings);
            Plan = plan; radius = settings.radius;
            bool first = manager.room == null;
            manager.room = room;
            anchor = eye + Vector3.up * plan.lift;
            float reach = Mathf.Clamp(manager.reachScale, .5f, 1);
            MoveTo(RoomPlacement.WorkspacePose(anchor, plan.forward, plan.scale * reach, manager.HeadAnchorLocal), plan.scale * reach);
            HangStems(room, eye, plan);
            // The reveal sweep plays when ROOM mode starts; a refined scan just redraws the surfaces.
            if (visuals != null) visuals.Show(room, plan.desk, eye, reveal: first);
            if (announce && toolbox != null) toolbox.Say(plan.Summary, 5);
            return plan;
        }

        public void Release(Vector3 eye, Vector3 gaze)
        {
            if (manager == null) return;
            bool was = manager.room != null;
            manager.room = null;
            if (visuals != null) visuals.Hide();
            if (spotsApplied)
            {
                // Stems the user left where ROOM put them go back to arm's length; moved ones stay put.
                var defaults = StemMix.DefaultPositions();
                for (int i = 0; i < StemMix.StemCount; i++)
                    if ((manager.mix.positions[i] - appliedSpots[i]).sqrMagnitude < 1e-4f) manager.mix.SetPosition((StemId)i, defaults[i]);
                spotsApplied = false;
            }
            if (was && manager.design != null)
            {
                float reach = Mathf.Clamp(manager.reachScale, .5f, 1);
                MoveTo(RoomPlacement.WorkspacePose(eye, gaze, reach, manager.HeadAnchorLocal), reach);
                if (toolbox != null) toolbox.Say("TABLE BACK IN FRONT OF YOU");
            }
        }

        void HangStems(RoomModel room, Vector3 eye, RoomPlan plan)
        {
            var mix = manager.mix; var defaults = StemMix.DefaultPositions();
            var spots = RoomPlacement.SpeakerSpots(room, eye, plan.forward, plan.scale * radius + .25f);
            // Local to the head anchor in the table's final orientation (the anchor sits at eye + lift).
            var anchor = eye + Vector3.up * plan.lift;
            for (int i = 0; i < StemMix.StemCount; i++)
            {
                // Only stems the user hasn't placed themselves (or that ROOM placed last time) move.
                bool untouched = (mix.positions[i] - defaults[i]).sqrMagnitude < 1e-4f || (spotsApplied && (mix.positions[i] - appliedSpots[i]).sqrMagnitude < 1e-4f);
                if (!untouched) continue;
                var local = Quaternion.Inverse(plan.pose.rotation) * (spots[i] - anchor);
                mix.SetPosition((StemId)i, local);
                appliedSpots[i] = mix.positions[i];
            }
            spotsApplied = true;
        }

        Vector3 anchor;
        // Accessibility CLOSER changed: glide to the same spot at the new size (arc stays centered on the head).
        public void ApplyReach()
        {
            if (manager == null || manager.workspace == null) return;
            float reach = Mathf.Clamp(manager.reachScale, .5f, 1);
            float baseScale = Placed ? Plan.scale : 1;
            var head = Placed ? anchor : manager.HeadAnchorWorld;
            MoveTo(RoomPlacement.WorkspacePose(head, manager.workspace.forward, baseScale * reach, manager.HeadAnchorLocal), baseScale * reach);
        }
        void MoveTo(Pose pose, float scale)
        {
            var w = manager.workspace;
            if (manager.interactions != null) manager.interactions.Cancel(); // Never edit while the table glides.
            fromPose = new Pose(w.position, w.rotation); fromScale = w.localScale.x;
            toPose = pose; toScale = Mathf.Max(.1f, scale); moveT = 0;
            if (!isActiveAndEnabled || moveSeconds <= .051f) Finish();
        }
        void Finish()
        {
            moveT = 1;
            manager.workspace.SetPositionAndRotation(toPose.position, toPose.rotation);
            manager.workspace.localScale = Vector3.one * toScale;
        }
        void Update()
        {
            if (moveT >= 1 || manager == null || manager.workspace == null) return;
            moveT = Mathf.Min(1, moveT + Time.unscaledDeltaTime / moveSeconds);
            if (moveT >= 1) { Finish(); return; }
            float e = moveT * moveT * (3 - 2 * moveT);
            manager.workspace.SetPositionAndRotation(Vector3.Lerp(fromPose.position, toPose.position, e), Quaternion.Slerp(fromPose.rotation, toPose.rotation, e));
            manager.workspace.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, e);
        }

        // Editor / no-headset preview: a typical desk setup around the current camera.
        [ContextMenu("Preview ROOM Mode (sample desk room)")]
        public void PreviewSampleRoom()
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            var eye = cam != null ? cam.position : new Vector3(0, 1.2f, 0);
            var gaze = cam != null ? cam.forward : Vector3.forward;
            Place(SampleRoom(eye, gaze), eye, gaze);
        }
        [ContextMenu("Leave ROOM Mode")]
        public void PreviewRelease()
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            Release(cam != null ? cam.position : new Vector3(0, 1.2f, 0), cam != null ? cam.forward : Vector3.forward);
        }

        // A seated user at a 1.2 x 0.6 m desk against a wall, in a 3.4 x 3.8 m room.
        public static RoomModel SampleRoom(Vector3 eye, Vector3 gaze)
        {
            var f = RoomPlacement.Flat(gaze, Vector3.forward); var r = Vector3.Cross(Vector3.up, f);
            float floor = eye.y - 1.2f;
            var room = new RoomModel();
            var c = new Vector3(eye.x, floor, eye.z) + f * .55f;   // room center a little in front of the user
            room.surfaces.Add(RoomSurface.Make(RoomSurfaceKind.Floor, c, Vector3.up, r, new Vector2(1.7f, 1.9f)));
            room.surfaces.Add(RoomSurface.Make(RoomSurfaceKind.Ceiling, c + Vector3.up * 2.5f, Vector3.down, r, new Vector2(1.7f, 1.9f)));
            room.surfaces.Add(RoomSurface.Make(RoomSurfaceKind.Wall, c + f * 1.9f + Vector3.up * 1.25f, -f, r, new Vector2(1.7f, 1.25f)));
            room.surfaces.Add(RoomSurface.Make(RoomSurfaceKind.Wall, c - f * 1.9f + Vector3.up * 1.25f, f, r, new Vector2(1.7f, 1.25f)));
            room.surfaces.Add(RoomSurface.Make(RoomSurfaceKind.Wall, c + r * 1.7f + Vector3.up * 1.25f, -r, f, new Vector2(1.9f, 1.25f)));
            room.surfaces.Add(RoomSurface.Make(RoomSurfaceKind.Wall, c - r * 1.7f + Vector3.up * 1.25f, r, f, new Vector2(1.9f, 1.25f)));
            // Desk: near edge 0.3 m in front of the eye, pushed against the front wall... of a deep room, so the
            // arc clears. 0.74 m high.
            var deskCenter = new Vector3(eye.x, floor + .74f, eye.z) + f * .6f;
            room.surfaces.Add(RoomSurface.Make(RoomSurfaceKind.Table, deskCenter, Vector3.up, r, new Vector2(.6f, .3f)));
            room.boxes.Add(new RoomBox { center = deskCenter - Vector3.up * .37f, rotation = Quaternion.LookRotation(f), halfExtents = new Vector3(.6f, .37f, .3f), isTable = true });
            return room;
        }
    }
}
