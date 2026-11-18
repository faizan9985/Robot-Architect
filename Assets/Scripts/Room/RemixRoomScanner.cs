using System.Collections.Generic;
using RemixTable;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR.Features.Meta;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// ROOM mode's eyes: while passthrough is on, reads the room the user captured in Meta Space Setup (walls,
/// floor, desk, furniture) through AR Foundation's Meta OpenXR provider, which shares the XR Origin's tracking
/// space with the rest of the rig, and hands it to RemixRoomPlacer as a plain RoomModel.
/// Handles the cases a judge's room can throw at it:
///  - scene permission not granted yet  -> asks, explains, and falls back to the floating table if denied;
///  - no room scan on the headset         -> launches Space Setup once, re-reads the room on return;
///  - a scan that changes later           -> re-places the table, never in the middle of an edit;
///  - recentering / tracking updates      -> surfaces move with the scan, the table follows.
/// Turning passthrough off returns the table in front of the user.
/// </summary>
[DisallowMultipleComponent]
public sealed class RemixRoomScanner : MonoBehaviour
{
    const string ScenePermission = "com.oculus.permission.USE_SCENE";

    [SerializeField] private PassthroughController passthrough;
    [SerializeField] private ARSession session;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private ARBoundingBoxManager boxManager;
    [SerializeField] private RemixRoomPlacer placer;
    [SerializeField] private RemixToolbox toolbox;
    [Tooltip("Seconds without new surfaces before the room counts as loaded.")]
    [SerializeField, Min(.1f)] private float settleSeconds = .6f;
    [Tooltip("Seconds to wait for any surface before offering Space Setup.")]
    [SerializeField, Min(.5f)] private float scanTimeout = 4f;

    public enum State { Off, WaitingForPermission, Scanning, Placed, NoRoom, Denied }
    public State Current { get; private set; } = State.Off;

    float scanStarted, lastChange, nextCheck; bool dirty, captureRequested;
    int placedSignature;

    private void OnEnable()
    {
        if (passthrough != null) passthrough.StateChanged += Sync;
        if (planeManager != null) planeManager.trackablesChanged.AddListener(OnPlanes);
        if (boxManager != null) boxManager.trackablesChanged.AddListener(OnBoxes);
        SetManagers(false);
        Sync();
    }

    private void OnDisable()
    {
        if (passthrough != null) passthrough.StateChanged -= Sync;
        if (planeManager != null) planeManager.trackablesChanged.RemoveListener(OnPlanes);
        if (boxManager != null) boxManager.trackablesChanged.RemoveListener(OnBoxes);
        if (Current != State.Off) End();
    }

    void OnPlanes(ARTrackablesChangedEventArgs<ARPlane> _) => MarkChanged();
    void OnBoxes(ARTrackablesChangedEventArgs<ARBoundingBox> _) => MarkChanged();
    void MarkChanged() { dirty = true; lastChange = Time.unscaledTime; }

    void Sync()
    {
        bool on = passthrough != null && passthrough.IsPassthroughEnabled;
        if (on && Current == State.Off) Begin();
        else if (!on && Current != State.Off) End();
    }

    void Begin()
    {
        scanStarted = lastChange = Time.unscaledTime; captureRequested = false; dirty = true; placedSignature = 0;
        if (!HasPermission())
        {
            Current = State.WaitingForPermission;
            Say("ALLOW SPATIAL DATA SO THE TABLE CAN FIT YOUR ROOM", 6);
            RequestPermission();
            return;
        }
        StartScanning();
    }

    void StartScanning()
    {
        Current = State.Scanning;
        scanStarted = lastChange = Time.unscaledTime; dirty = true;
        SetManagers(true);
        Say("READING YOUR ROOM...", 3);
    }

    void End()
    {
        Current = State.Off;
        SetManagers(false);
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (placer != null) placer.Release(cam != null ? cam.position : Vector3.up * 1.2f, cam != null ? cam.forward : Vector3.forward);
    }

    void SetManagers(bool on)
    {
        if (planeManager != null) planeManager.enabled = on;
        if (boxManager != null) boxManager.enabled = on;
    }

    // Space Setup runs as a separate system app; when we get focus back, ask again for the fresh scan.
    private void OnApplicationFocus(bool focus)
    {
        if (!focus || Current == State.Off) return;
        if (Current == State.WaitingForPermission || Current == State.Denied) { if (HasPermission()) StartScanning(); return; }
        if (Current == State.NoRoom || captureRequested) { SetManagers(false); StartScanning(); }
    }

    private void Update()
    {
        if (Current == State.Off) return;
        if (Current == State.WaitingForPermission)
        {
            if (HasPermission()) StartScanning();
            return;
        }
        if (Current == State.Denied) return;
        if (placer == null || placer.manager == null || !placer.manager.anchored) return;

        float now = Time.unscaledTime;
        int surfaces = SurfaceCount();
        if (surfaces == 0)
        {
            if (Current == State.Scanning && now - scanStarted > scanTimeout) NoRoomFound();
            return;
        }
        // Wait for the scan to settle, then (re)place; never move the table under an active edit or orb grab.
        if (!dirty || now - lastChange < settleSeconds || now < nextCheck) return;
        if (placer.manager.IsEditing || placer.manager.ExternalHold) return;
        nextCheck = now + .5f; dirty = false;
        var room = BuildRoom();
        int signature = Signature(room);
        if (Current == State.Placed && signature == placedSignature) return; // tracking jitter, same room
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;
        bool first = Current != State.Placed;
        placer.Place(room, cam.position, cam.forward, announce: first);
        placedSignature = signature;
        Current = State.Placed;
    }

    void NoRoomFound()
    {
        Current = State.NoRoom;
        if (!captureRequested && session != null && session.subsystem is MetaOpenXRSessionSubsystem meta)
        {
            captureRequested = true;
            if (meta.TryRequestSceneCapture()) { Say("NO ROOM SCAN YET: OPENING SPACE SETUP", 6); return; }
        }
        Say("NO ROOM SCAN: SETTINGS > PHYSICAL SPACE > SPACE SETUP, THEN PRESS ROOM", 8);
    }

    int SurfaceCount()
    {
        int n = 0;
        if (planeManager != null && planeManager.enabled) n += planeManager.trackables.count;
        if (boxManager != null && boxManager.enabled) n += boxManager.trackables.count;
        return n;
    }

    // ---- AR Foundation -> RoomModel ------------------------------------------------------------------------
    public RoomModel BuildRoom()
    {
        var room = new RoomModel();
        var cam = Camera.main != null ? Camera.main.transform.position : Vector3.up * 1.2f;
        if (planeManager != null)
        {
            foreach (var plane in planeManager.trackables)
            {
                if (plane == null || plane.subsumedBy != null || plane.trackingState == TrackingState.None) continue;
                var c = plane.classifications;
                RoomSurfaceKind kind; bool invisible = false;
                if ((c & PlaneClassifications.Floor) != 0) kind = RoomSurfaceKind.Floor;
                else if ((c & PlaneClassifications.Ceiling) != 0) kind = RoomSurfaceKind.Ceiling;
                else if ((c & (PlaneClassifications.WallFace | PlaneClassifications.InnerWallFace)) != 0) kind = RoomSurfaceKind.Wall;
                else if ((c & PlaneClassifications.InvisibleWallFace) != 0) { kind = RoomSurfaceKind.Wall; invisible = true; }
                else if ((c & PlaneClassifications.Table) != 0) kind = RoomSurfaceKind.Table;
                else if ((c & PlaneClassifications.SeatOfAnyType) != 0) kind = RoomSurfaceKind.Seat;
                else kind = RoomSurfaceKind.Other;
                var normal = plane.normal; var center = plane.center;
                // Normals into the room: walls face the user, floors / tables face up, ceilings down.
                if (kind == RoomSurfaceKind.Wall && Vector3.Dot(normal, cam - center) < 0) normal = -normal;
                if ((kind == RoomSurfaceKind.Floor || kind == RoomSurfaceKind.Table || kind == RoomSurfaceKind.Seat) && normal.y < 0) normal = -normal;
                if (kind == RoomSurfaceKind.Ceiling && normal.y > 0) normal = -normal;
                room.surfaces.Add(RoomSurface.Make(kind, center, normal, plane.transform.right, plane.extents, invisible));
            }
        }
        if (boxManager != null)
        {
            foreach (var box in boxManager.trackables)
            {
                if (box == null || box.trackingState == TrackingState.None) continue;
                var t = box.transform; var half = box.size * .5f;
                bool table = (box.classifications & BoundingBoxClassifications.Table) != 0;
                room.boxes.Add(new RoomBox { center = t.position, rotation = t.rotation, halfExtents = half, isTable = table });
                if (table) AddTableTop(room, t, half);
            }
        }
        return room;
    }

    // A desk captured as a volume: use its top face, unless a table plane already covers it.
    static void AddTableTop(RoomModel room, Transform t, Vector3 half)
    {
        Vector3[] axes = { t.right, t.up, t.forward };
        int up = 0; for (int i = 1; i < 3; i++) if (Mathf.Abs(axes[i].y) > Mathf.Abs(axes[up].y)) up = i;
        var upAxis = axes[up] * Mathf.Sign(axes[up].y);
        var top = t.position + upAxis * half[up];
        int u = (up + 1) % 3, v = (up + 2) % 3;
        foreach (var s in room.surfaces)
        {
            if (s.kind != RoomSurfaceKind.Table) continue;
            var d = s.center - top;
            if (Mathf.Abs(d.y) < .1f && new Vector2(d.x, d.z).magnitude < .25f) return;
        }
        room.surfaces.Add(RoomSurface.Make(RoomSurfaceKind.Table, top, upAxis, axes[u], new Vector2(half[u], half[v])));
    }

    // Rounded layout fingerprint: tracking noise doesn't count as a new room, a re-scan does.
    static int Signature(RoomModel room)
    {
        unchecked
        {
            int h = 17;
            foreach (var s in room.surfaces)
            {
                h = h * 31 + (int)s.kind;
                h = h * 31 + Mathf.RoundToInt(s.center.x * 10); h = h * 31 + Mathf.RoundToInt(s.center.y * 10); h = h * 31 + Mathf.RoundToInt(s.center.z * 10);
                h = h * 31 + Mathf.RoundToInt(s.halfSize.x * 10); h = h * 31 + Mathf.RoundToInt(s.halfSize.y * 10);
            }
            return h * 31 + room.boxes.Count;
        }
    }

    // ---- Permission ----------------------------------------------------------------------------------------
    static bool HasPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Permission.HasUserAuthorizedPermission(ScenePermission);
#else
        return true;
#endif
    }

    void RequestPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => { if (Current == State.WaitingForPermission) StartScanning(); };
        callbacks.PermissionDenied += _ => Denied();
        Permission.RequestUserPermission(ScenePermission, callbacks);
#endif
    }

    void Denied()
    {
        if (Current != State.WaitingForPermission) return;
        Current = State.Denied;
        Say("NO ROOM ACCESS: TABLE STAYS IN FRONT OF YOU (ALLOW IT IN SETTINGS > PRIVACY)", 7);
    }

    void Say(string text, float seconds) { if (toolbox != null) toolbox.Say(text, seconds); }
}
