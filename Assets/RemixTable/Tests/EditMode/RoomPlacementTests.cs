using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    // ROOM mode placement in rooms we never tested on a headset: desks in front / behind / out of reach /
    // standing height, tight walls, no scan at all, stems hung on walls, orbs sticking to surfaces.
    public class RoomPlacementTests
    {
        static readonly Vector3 Eye = new Vector3(0, 1.2f, 0);
        // Measured from the real layout: rows hang to -0.564 m below the workspace origin, tools reach +0.35 m.
        static RoomPlacementSettings Settings()
        {
            var s = RoomPlacementSettings.For(null, -.564f, .35f);
            s.radius = .8f; s.eyeDrop = .2f;
            return s;
        }
        static RoomSurface Desk(Vector3 center, float halfX = .6f, float halfZ = .3f) => RoomSurface.Make(RoomSurfaceKind.Table, center, Vector3.up, Vector3.right, new Vector2(halfX, halfZ));
        static RoomSurface Floor() => RoomSurface.Make(RoomSurfaceKind.Floor, Vector3.zero, Vector3.up, Vector3.right, new Vector2(3, 3));
        static RoomSurface Wall(Vector3 center, Vector3 inward, float halfWidth = 2.5f, bool invisible = false) =>
            RoomSurface.Make(RoomSurfaceKind.Wall, center, inward, Vector3.Cross(Vector3.up, inward), new Vector2(halfWidth, 1.5f), invisible);
        static RoomModel Room(params RoomSurface[] surfaces) { var r = new RoomModel(); r.surfaces.Add(Floor()); r.surfaces.AddRange(surfaces); return r; }
        static RoomModel Box(float half) => Room(Wall(new Vector3(0, 1.5f, half), Vector3.back), Wall(new Vector3(0, 1.5f, -half), Vector3.forward),
                                                Wall(new Vector3(half, 1.5f, 0), Vector3.left), Wall(new Vector3(-half, 1.5f, 0), Vector3.right));
        static float Bottom(RoomPlan p, RoomPlacementSettings s) => Eye.y + p.lift + (s.contentBottom - s.eyeDrop) * p.scale;

        [Test] public void NoScanKeepsTheNormalFloatingTable()
        {
            var plan = RoomPlacement.Solve(new RoomModel(), Eye, Vector3.forward, Settings());
            Assert.AreEqual(-1, plan.desk); Assert.AreEqual(1, plan.scale); Assert.AreEqual(0, plan.lift);
            var design = ScriptableObject.CreateInstance<RemixDesign>(); design.curvedTimeline = true;
            var expected = design.SeatedPose(Eye, Vector3.forward);
            Assert.That(Vector3.Distance(expected.position, plan.pose.position), Is.LessThan(1e-4f), "same pose the table normally anchors at");
            Object.DestroyImmediate(design);
        }

        [Test] public void DeskInFrontIsUsedAndRowsClearItWithoutRisingAboveTheEyes()
        {
            var s = Settings();
            var plan = RoomPlacement.Solve(Room(Desk(new Vector3(0, .74f, .6f))), Eye, Vector3.forward, s);
            Assert.AreEqual(1, plan.desk);
            Assert.GreaterOrEqual(Bottom(plan, s), .74f + s.deskClearance - 1e-3f, "lowest row sits above the desk");
            Assert.LessOrEqual(plan.lift, s.maxLift + 1e-4f);
            Assert.GreaterOrEqual(plan.scale, s.minDeskScale - 1e-4f);
            float ribbonCenter = Eye.y + plan.lift - s.eyeDrop * plan.scale;
            Assert.LessOrEqual(ribbonCenter, Eye.y + .05f, "ribbons stay at or below eye level");
            Assert.IsFalse(plan.deskTooHigh);
            StringAssert.Contains("DESK", plan.Summary);
        }

        [Test] public void FacesSquarelyAcrossTheDeskEvenWhenGlancingSideways()
        {
            var gaze = Quaternion.AngleAxis(30, Vector3.up) * Vector3.forward;
            var plan = RoomPlacement.Solve(Room(Desk(new Vector3(0, .74f, .6f))), Eye, gaze, Settings());
            Assert.AreEqual(1, plan.desk);
            Assert.Less(Vector3.Angle(plan.forward, Vector3.forward), 1f);
        }

        [Test] public void DeskBehindOrOutOfReachIsIgnored()
        {
            Assert.AreEqual(-1, RoomPlacement.Solve(Room(Desk(new Vector3(0, .74f, -.6f))), Eye, Vector3.forward, Settings()).desk, "behind");
            Assert.AreEqual(-1, RoomPlacement.Solve(Room(Desk(new Vector3(0, .74f, 2.2f))), Eye, Vector3.forward, Settings()).desk, "across the room");
            Assert.AreEqual(-1, RoomPlacement.Solve(Room(Desk(new Vector3(0, .74f, .5f), .08f, .08f)), Eye, Vector3.forward, Settings()).desk, "too small to work at");
            Assert.AreEqual(-1, RoomPlacement.Solve(Room(Desk(new Vector3(0, .1f, .5f))), Eye, Vector3.forward, Settings()).desk, "floor-level");
        }

        [Test] public void StandingDeskWhileSeatedCapsTheLiftAndSaysSo()
        {
            var s = Settings();
            var plan = RoomPlacement.Solve(Room(Desk(new Vector3(0, 1.05f, .6f))), Eye, Vector3.forward, s);
            Assert.AreEqual(1, plan.desk);
            Assert.IsTrue(plan.deskTooHigh);
            Assert.AreEqual(s.maxLift, plan.lift, 1e-4f);
            StringAssert.Contains("HIGH", plan.Summary);
        }

        [Test] public void StandingUserNeedsNoLift()
        {
            var plan = RoomPlacement.Solve(Room(Desk(new Vector3(0, .74f, .6f))), new Vector3(0, 1.65f, 0), Vector3.forward, Settings());
            Assert.AreEqual(1, plan.desk); Assert.AreEqual(0, plan.lift, 1e-4f); Assert.AreEqual(1, plan.scale, 1e-4f);
        }

        [Test] public void DeskVolumeUnderTheRowsDoesNotShrinkTheArc()
        {
            var room = Room(Desk(new Vector3(0, .74f, .6f)));
            room.boxes.Add(new RoomBox { center = new Vector3(0, .37f, .6f), rotation = Quaternion.identity, halfExtents = new Vector3(.6f, .37f, .3f), isTable = true });
            var plan = RoomPlacement.Solve(room, Eye, Vector3.forward, Settings());
            Assert.Greater(plan.clearance, 10, "the desk's own box never counts as a wall");
        }

        [Test] public void CloseWallsShrinkTheArcAndVeryCloseOnesAreReported()
        {
            var s = Settings();
            var near = RoomPlacement.Solve(Room(Wall(new Vector3(0, 1.5f, .85f), Vector3.back)), Eye, Vector3.forward, s);
            Assert.AreEqual((.85f - s.wallMargin) / s.radius, near.scale, .01f);
            Assert.IsFalse(near.tight);
            Assert.LessOrEqual(near.scale * s.radius + s.wallMargin, near.clearance + 1e-3f, "ribbons stay off the wall");
            var tight = RoomPlacement.Solve(Room(Wall(new Vector3(0, 1.5f, .45f), Vector3.back)), Eye, Vector3.forward, s);
            Assert.AreEqual(s.minScale, tight.scale, 1e-4f);
            Assert.IsTrue(tight.tight);
            StringAssert.Contains("TIGHT", tight.Summary);
            var open = RoomPlacement.Solve(Box(3), Eye, Vector3.forward, s);
            Assert.AreEqual(1, open.scale, "a normal room leaves the table full size");
        }

        [Test] public void BadTrackingInputFallsBackSafely()
        {
            var plan = RoomPlacement.Solve(Box(2), new Vector3(float.NaN, 1, 0), Vector3.forward, Settings());
            Assert.AreEqual(-1, plan.desk); Assert.AreEqual(1, plan.scale);
            var flat = RoomPlacement.Solve(Box(2), Eye, Vector3.up, Settings()); // looking straight up
            Assert.IsFalse(float.IsNaN(flat.pose.position.x)); Assert.AreEqual(Vector3.forward, flat.forward);
        }

        [Test] public void StemsHangOnTheWallsAroundTheListener()
        {
            var room = Box(2);
            var spots = RoomPlacement.SpeakerSpots(room, Eye, Vector3.forward, 1.05f);
            for (int i = 0; i < spots.Length; i++)
            {
                float wall = float.MaxValue;
                foreach (var s in room.surfaces) if (s.kind == RoomSurfaceKind.Wall) wall = Mathf.Min(wall, Vector3.Distance(s.ClosestPoint(spots[i]), spots[i]));
                Assert.AreEqual(.14f, wall, .02f, $"stem {(StemId)i} sits just off a wall");
                var flat = spots[i] - Eye; flat.y = 0;
                Assert.GreaterOrEqual(flat.magnitude, 1.05f - 1e-3f, "clear of the ribbon arc");
            }
            Assert.Greater(spots[(int)StemId.Vocals].z, 1.8f, "vocals on the wall ahead");
        }

        [Test] public void WithoutWallsStemsKeepTheirUsualSpots()
        {
            var spots = RoomPlacement.SpeakerSpots(Room(), Eye, Vector3.forward, 1.05f);
            var defaults = StemMix.DefaultPositions();
            for (int i = 0; i < spots.Length; i++) Assert.That(Vector3.Distance(Eye + defaults[i], spots[i]), Is.LessThan(1e-4f));
        }

        [Test] public void OrbsStickToWallsAndTablesButNotFloorsOrVirtualWalls()
        {
            var room = Room(Wall(new Vector3(0, 1.5f, 2), Vector3.back), Wall(new Vector3(2, 1.5f, 0), Vector3.left, 2.5f, invisible: true), Desk(new Vector3(0, .74f, .6f)));
            Assert.IsTrue(room.TryClosestSurface(new Vector3(.3f, 1.4f, 1.8f), .3f, out var wall, out var p));
            Assert.AreEqual(RoomSurfaceKind.Wall, wall.kind); Assert.AreEqual(2, p.z, 1e-4f);
            Assert.IsTrue(room.TryClosestSurface(new Vector3(.2f, .9f, .6f), .3f, out var table, out _));
            Assert.AreEqual(RoomSurfaceKind.Table, table.kind);
            Assert.IsFalse(room.TryClosestSurface(new Vector3(0, .1f, -1), .3f, out _, out _), "floor is not a mount");
            Assert.IsFalse(room.TryClosestSurface(new Vector3(1.85f, 1.4f, -1), .3f, out _, out _), "invisible walls are not mounts");
        }

        [Test] public void FurnitureBoxesBlockRaysButNotFromInside()
        {
            var couch = new RoomBox { center = new Vector3(0, .4f, 0), rotation = Quaternion.identity, halfExtents = new Vector3(1, .4f, .5f) };
            Assert.IsFalse(couch.Raycast(new Ray(new Vector3(0, .5f, 0), Vector3.forward), out _), "sitting on the couch");
            Assert.IsTrue(couch.Raycast(new Ray(new Vector3(0, .5f, -2), Vector3.forward), out float d));
            Assert.AreEqual(1.5f, d, 1e-4f);
            var tilted = new RoomBox { center = Vector3.zero, rotation = Quaternion.Euler(90, 0, 0), halfExtents = new Vector3(1, .2f, .5f) };
            Assert.AreEqual(.5f, tilted.Top, 1e-4f);
        }
    }

    // RemixRoomPlacer on a real (headless) table: glide target, scale, stems on walls, and a clean way back.
    public class RoomPlacerTests
    {
        GameObject root; RemixTableManager manager; RemixRoomPlacer placer;
        static readonly Vector3 Eye = new Vector3(0, 1.2f, 0);
        [SetUp] public void Setup()
        {
            root = new GameObject("room fixture"); root.SetActive(false);
            manager = root.AddComponent<RemixTableManager>(); manager.enabled = false;
            manager.design = ScriptableObject.CreateInstance<RemixDesign>(); manager.design.curvedTimeline = true;
            manager.workspace = new GameObject("workspace").transform; manager.workspace.SetParent(root.transform);
            var pose = manager.design.SeatedPose(Eye, Vector3.forward); manager.workspace.SetPositionAndRotation(pose.position, pose.rotation);
            placer = root.AddComponent<RemixRoomPlacer>(); placer.manager = manager; placer.moveSeconds = .05f;
            root.SetActive(true);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(manager.design); Object.DestroyImmediate(root); }

        [Test] public void PlacingInARoomMovesTheTableAndHangsTheStems()
        {
            Assert.That(Vector3.Distance(manager.HeadAnchorWorld, Eye), Is.LessThan(1e-4f), "arc starts centered on the head");
            var room = RemixRoomPlacer.SampleRoom(Eye, Vector3.forward);
            var plan = placer.Place(room, Eye, Vector3.forward);
            Assert.IsTrue(placer.Placed); Assert.AreSame(room, manager.room);
            Assert.GreaterOrEqual(plan.desk, 0, "the sample room's desk is found");
            Assert.AreEqual(plan.scale, manager.workspace.localScale.x, 1e-4f);
            Assert.That(Vector3.Distance(manager.HeadAnchorWorld, Eye + Vector3.up * plan.lift), Is.LessThan(1e-3f), "arc stays centered over the head");
            int onWalls = 0;
            for (int i = 0; i < StemMix.StemCount; i++)
            {
                var world = manager.SpatialWorld(manager.mix.positions[i]);
                if (room.TryClosestSurface(world, .2f, out var s, out _) && s.kind == RoomSurfaceKind.Wall) onWalls++;
            }
            Assert.Greater(onWalls, 0, "at least one stem hangs on a wall");
        }

        [Test] public void LeavingRoomModeRestoresTheTableAndUntouchedStems()
        {
            placer.Place(RemixRoomPlacer.SampleRoom(Eye, Vector3.forward), Eye, Vector3.forward);
            var moved = new Vector3(.3f, .1f, 1.2f);
            manager.mix.SetPosition(StemId.Bass, moved);  // the user re-hangs one stem by hand
            placer.Release(Eye, Vector3.forward);
            Assert.IsNull(manager.room);
            Assert.AreEqual(1, manager.workspace.localScale.x, 1e-4f);
            Assert.That(Vector3.Distance(manager.HeadAnchorWorld, Eye), Is.LessThan(1e-3f));
            var defaults = StemMix.DefaultPositions();
            Assert.That(Vector3.Distance(defaults[(int)StemId.Vocals], manager.mix.positions[(int)StemId.Vocals]), Is.LessThan(1e-4f), "ROOM-placed stems return");
            Assert.That(Vector3.Distance(moved, manager.mix.positions[(int)StemId.Bass]), Is.LessThan(1e-4f), "a stem the user moved stays");
        }

        [Test] public void UserPlacedStemsAreNotPulledOntoWalls()
        {
            var mine = new Vector3(-.4f, 0, .9f);
            manager.mix.SetPosition(StemId.Drums, mine);
            placer.Place(RemixRoomPlacer.SampleRoom(Eye, Vector3.forward), Eye, Vector3.forward);
            Assert.That(Vector3.Distance(mine, manager.mix.positions[(int)StemId.Drums]), Is.LessThan(1e-4f));
        }
    }
}
