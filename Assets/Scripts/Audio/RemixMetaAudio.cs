using RemixTable;
using UnityEngine;

/// <summary>
/// Meta XR Audio for the Remix Table: every stem source (and its loop twin) gets a MetaXRAudioSource, so in
/// SPATIAL mode each stem is rendered with Meta's HRTF from where its orb hangs, with early reflections and
/// reverb. The shoebox room for that reverb is sized from the real room scan in ROOM mode (walls, floor,
/// ceiling from Space Setup), so a remix in a small bedroom sounds like a small bedroom; otherwise a calm
/// default studio. Outside SPATIAL mode the sources stay plain stereo and none of this is heard.
/// </summary>
[DisallowMultipleComponent]
public sealed class RemixMetaAudio : MonoBehaviour
{
    [SerializeField] private RemixTableManager manager;
    [SerializeField] private MetaXRAudioRoomAcousticProperties room;
    [Tooltip("Reverb send per stem (dB). Kept modest so the mix stays clear.")]
    [SerializeField, Range(-30, 10)] private float reverbSendDb = -9;
    [SerializeField] private Vector3 defaultRoom = new Vector3(5f, 2.7f, 4.5f);

    RoomModel shownRoom; float nextScan;
    public Vector3 RoomSize { get; private set; }

    private void Start() { Configure(); ApplyRoom(); }

    private void Update()
    {
        if (manager == null) return;
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1; Configure(); } // loop twins appear later
        if (!ReferenceEquals(shownRoom, manager.room)) ApplyRoom();
        // MetaXRAudioSource writes AudioSource.spatialize every frame: follow SPATIAL mode so stereo stays stereo.
        bool spatial = manager.audioManager != null && manager.audioManager.Spatial3DActive;
        foreach (var meta in metas) if (meta != null && meta.EnableSpatialization != spatial) meta.EnableSpatialization = spatial;
    }
    readonly System.Collections.Generic.List<MetaXRAudioSource> metas = new System.Collections.Generic.List<MetaXRAudioSource>();

    void Configure()
    {
        var audio = manager != null ? manager.audioManager : null;
        if (audio == null || audio.sources == null) return;
        foreach (var source in audio.sources)
        {
            if (source == null) continue;
            foreach (var s in source.GetComponentsInChildren<AudioSource>(true))
            {
                var meta = s.GetComponent<MetaXRAudioSource>();
                if (meta != null) { if (!metas.Contains(meta)) metas.Add(meta); continue; }
                meta = s.gameObject.AddComponent<MetaXRAudioSource>();
                metas.Add(meta);
                meta.EnableSpatialization = manager.audioManager.Spatial3DActive;
                meta.EnableAcoustics = true;
                meta.ReverbSendDb = reverbSendDb;
            }
        }
    }

    void ApplyRoom()
    {
        shownRoom = manager != null ? manager.room : null;
        RoomSize = shownRoom != null && TryMeasure(shownRoom, out var size) ? size : defaultRoom;
        if (room == null) return;
        room.width = RoomSize.x; room.height = RoomSize.y; room.depth = RoomSize.z;
        room.lockPositionToListener = true;
        bool real = shownRoom != null;
        room.floorMaterial = real ? MetaXRAudioRoomAcousticProperties.MaterialPreset.Carpet : MetaXRAudioRoomAcousticProperties.MaterialPreset.CarpetHeavy;
        room.ceilingMaterial = MetaXRAudioRoomAcousticProperties.MaterialPreset.GypsumBoard;
        room.leftMaterial = room.rightMaterial = room.frontMaterial = room.backMaterial = MetaXRAudioRoomAcousticProperties.MaterialPreset.GypsumBoard;
        room.clutterFactor = real ? .55f : .7f; // furnished rooms diffuse more
    }

    // Shoebox from the scanned walls (horizontal extent of every wall corner) and floor-to-ceiling height.
    public static bool TryMeasure(RoomModel room, out Vector3 size)
    {
        size = Vector3.zero;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, floor = float.MaxValue, ceiling = float.MinValue;
        int walls = 0;
        foreach (var s in room.surfaces)
        {
            if (s.kind == RoomSurfaceKind.Floor) floor = Mathf.Min(floor, s.center.y);
            else if (s.kind == RoomSurfaceKind.Ceiling) ceiling = Mathf.Max(ceiling, s.center.y);
            else if (s.kind == RoomSurfaceKind.Wall)
            {
                walls++;
                foreach (var cx in new[] { -1f, 1f })
                {
                    var p = s.center + s.axisU * s.halfSize.x * cx;
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                }
            }
        }
        if (walls < 2) return false;
        float height = floor != float.MaxValue && ceiling != float.MinValue ? ceiling - floor : 2.6f;
        size = new Vector3(Mathf.Clamp(maxX - minX, 1.5f, 30), Mathf.Clamp(height, 2, 8), Mathf.Clamp(maxZ - minZ, 1.5f, 30));
        return true;
    }
}
