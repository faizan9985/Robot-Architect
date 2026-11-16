using System.Collections.Generic;
using UnityEngine;
namespace RemixTable
{
    // ROOM mode light show on the user's real surfaces (over passthrough):
    //  - a reveal sweep rolls out from the listener across the scanned walls, floor and desk when the room loads;
    //  - every beat of the song sends a ripple out from the desk across the room (stronger on the downbeat,
    //    scaled by how loud the drums are);
    //  - in SPATIAL mode a wall glows in the color of the stem hanging on it, breathing with that stem's level,
    //    and a mount ring marks where each orb is stuck to a wall or table;
    //  - the edge of the desk becomes four LED level meters, one per stem, in lane order.
    // Pure presentation: reads RemixTableManager.room and the last RoomPlan; never moves the table.
    public sealed class RemixRoomVisuals : MonoBehaviour
    {
        public RemixTableManager manager;
        public SpatialStemField spatialField;
        [Tooltip("RemixTable/RoomGlow material (Materials/RoomGlow.mat).")]
        public Material glowMaterial;
        public Color wallColor = new Color(.32f, .78f, 1f, 1), deskColor = new Color(1f, .78f, .45f, 1);
        [Min(.1f)] public float revealSpeed = 3.2f, rippleSpeed = 2.6f;
        [Tooltip("Seconds the room stays fully lit after the reveal before settling to a calm glow.")]
        [Min(0)] public float revealHold = 3f;
        [Range(0, 1)] public float wallStemTint = .85f;

        sealed class Panel { public Renderer renderer; public RoomSurface surface; public bool desk; public Vector2 size; }
        readonly List<Panel> panels = new List<Panel>();
        readonly Renderer[] meters = new Renderer[StemMix.StemCount], rings = new Renderer[StemMix.StemCount];
        readonly float[] meterLevels = new float[StemMix.StemCount];
        Material material; Mesh quad; MaterialPropertyBlock block;
        Vector3 revealOrigin, pulseOrigin; float revealStart = -999; bool showing;
        Vector4 ages = new Vector4(99, 99, 99, 99), strengths;
        int lastBeat = int.MinValue; float lastClock = -1;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), SizeId = Shader.PropertyToID("_Size"), ShapeId = Shader.PropertyToID("_Shape"),
            FillId = Shader.PropertyToID("_Fill"), LevelId = Shader.PropertyToID("_Level"), IntensityId = Shader.PropertyToID("_Intensity"),
            RevealId = Shader.PropertyToID("_RoomReveal"), PulseOriginId = Shader.PropertyToID("_RoomPulseOrigin"),
            PulseAgesId = Shader.PropertyToID("_RoomPulseAges"), PulseStrengthId = Shader.PropertyToID("_RoomPulseStrength"), IdleId = Shader.PropertyToID("_RoomIdle");

        public bool Showing => showing;
        public int PanelCount => panels.Count;
        public int DeskPanel { get { for (int i = 0; i < panels.Count; i++) if (panels[i].desk) return i; return -1; } }
        public bool MeterVisible(int stem) => meters[stem] != null && meters[stem].enabled;

        // Lights up the room. `desk` is the RoomPlan desk index (or -1).
        public void Show(RoomModel room, int desk, Vector3 eye, bool reveal = true)
        {
            float previousReveal = revealStart;
            Clear();
            if (room == null || glowMaterial == null) return;
            if (material == null) material = new Material(glowMaterial) { name = "RoomGlow (runtime)" };
            quad ??= BuildQuad();
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < room.surfaces.Count; i++)
            {
                var s = room.surfaces[i];
                if (s.invisible || s.kind == RoomSurfaceKind.Ceiling || s.kind == RoomSurfaceKind.Other || s.kind == RoomSurfaceKind.Seat) continue;
                var size = s.halfSize * 2;
                if (size.x < .05f || size.y < .05f) continue;
                var r = Make($"{s.kind} {i}", s.center + s.normal * .004f, Quaternion.LookRotation(s.axisV, s.normal), new Vector3(size.x, 1, size.y));
                panels.Add(new Panel { renderer = r, surface = s, desk = i == desk, size = size });
            }
            if (desk >= 0 && desk < room.surfaces.Count) BuildMeters(room.surfaces[desk], eye);
            for (int i = 0; i < rings.Length; i++) { rings[i] = Make("Mount Ring " + i, Vector3.zero, Quaternion.identity, Vector3.one); rings[i].enabled = false; }
            if (reveal || previousReveal < -100) { revealOrigin = eye; revealStart = Time.unscaledTime; }
            else revealStart = Mathf.Min(previousReveal, Time.unscaledTime - revealHold - 2); // already revealed: stay calm
            pulseOrigin = desk >= 0 && desk < room.surfaces.Count ? room.surfaces[desk].center : new Vector3(eye.x, room.FloorY(eye.y - 1.2f), eye.z);
            ages = new Vector4(99, 99, 99, 99); strengths = Vector4.zero; lastBeat = int.MinValue;
            showing = true;
            LateUpdate();
        }

        public void Hide() { Clear(); Shader.SetGlobalVector(RevealId, Vector4.zero); }

        void Clear()
        {
            foreach (var p in panels) if (p.renderer != null) Destroy(p.renderer.gameObject);
            panels.Clear();
            for (int i = 0; i < meters.Length; i++) { if (meters[i] != null) Destroy(meters[i].gameObject); meters[i] = null; meterLevels[i] = 0; }
            for (int i = 0; i < rings.Length; i++) { if (rings[i] != null) Destroy(rings[i].gameObject); rings[i] = null; }
            showing = false;
        }
        void OnDisable() => Hide();
        void OnDestroy() { if (material != null) Destroy(material); if (quad != null) Destroy(quad); }

        // Four strips just inside the desk edge nearest the user, left to right in lane order.
        void BuildMeters(RoomSurface desk, Vector3 eye)
        {
            var p = desk.ToPlane(eye);
            bool alongV = desk.halfSize.x - Mathf.Abs(p.x) < desk.halfSize.y - Mathf.Abs(p.y); // user is past a U edge
            Vector3 edgeCenter, inward; float edgeLength;
            if (alongV) { float side = Mathf.Sign(p.x == 0 ? 1 : p.x); edgeCenter = desk.FromPlane(new Vector2(side * desk.halfSize.x, 0)); inward = -desk.axisU * side; edgeLength = desk.halfSize.y * 2; }
            else { float side = Mathf.Sign(p.y == 0 ? 1 : p.y); edgeCenter = desk.FromPlane(new Vector2(0, side * desk.halfSize.y)); inward = -desk.axisV * side; edgeLength = desk.halfSize.x * 2; }
            float total = Mathf.Min(edgeLength * .8f, .9f), gap = .025f, width = .022f;
            float length = (total - gap * (meters.Length - 1)) / meters.Length;
            var rotation = Quaternion.LookRotation(inward, desk.normal);  // local X runs left to right for the seated user
            var right = rotation * Vector3.right;
            var start = edgeCenter + inward * .05f + desk.normal * .005f - right * (total * .5f - length * .5f);
            StemId[] order = { StemId.Vocals, StemId.Drums, StemId.Bass, StemId.Melody };
            for (int k = 0; k < order.Length; k++)
            {
                int stem = (int)order[k];
                meters[stem] = Make("Level Meter " + order[k], start + right * (k * (length + gap)), rotation, new Vector3(length, 1, width));
            }
        }

        Renderer Make(string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }

        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "RoomGlowQuad" };
            mesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1, .02f, 1));
            return mesh;
        }

        void LateUpdate()
        {
            if (!showing || manager == null) return;
            float dt = Time.unscaledDeltaTime, since = Time.unscaledTime - revealStart;
            float idle = since < revealHold ? 1 : Mathf.Clamp01(1 - (since - revealHold) / 1.5f);
            Shader.SetGlobalVector(RevealId, new Vector4(revealOrigin.x, revealOrigin.y, revealOrigin.z, since * revealSpeed));
            Shader.SetGlobalFloat(IdleId, idle);
            TickBeats(dt);
            Shader.SetGlobalVector(PulseOriginId, new Vector4(pulseOrigin.x, pulseOrigin.y, pulseOrigin.z, rippleSpeed));
            Shader.SetGlobalVector(PulseAgesId, ages);
            Shader.SetGlobalVector(PulseStrengthId, strengths);

            var audio = manager.audioManager; var design = manager.design;
            bool spatial = manager.mix.spatial && spatialField != null;
            foreach (var p in panels)
            {
                if (p.renderer == null) continue;
                var color = p.desk ? deskColor : p.surface.kind == RoomSurfaceKind.Table ? deskColor * .7f : wallColor;
                float level = 0, intensity = p.desk ? 1.1f : p.surface.kind == RoomSurfaceKind.Floor ? .8f : 1;
                if (spatial && p.surface.kind == RoomSurfaceKind.Wall && design != null)
                {
                    // The wall a stem hangs on takes that stem's color and breathes with it.
                    int nearest = -1; float best = .6f;
                    for (int i = 0; i < spatialField.orbs.Length; i++)
                    {
                        var orb = spatialField.orbs[i];
                        if (orb == null || !orb.gameObject.activeInHierarchy) continue;
                        float d = Vector3.Distance(p.surface.ClosestPoint(orb.transform.position), orb.transform.position);
                        if (d < best) { best = d; nearest = i; }
                    }
                    if (nearest >= 0)
                    {
                        bool audible = manager.mix.Audible((StemId)nearest);
                        color = Color.Lerp(wallColor, design.colors[nearest], wallStemTint);
                        level = audible && audio != null ? Mathf.Clamp01(audio.Level((StemId)nearest) * 3f) : 0;
                        intensity *= audible ? 1.15f : .6f;
                    }
                }
                Apply(p.renderer, color, 0, p.size, level, 1, intensity);
            }
            for (int i = 0; i < meters.Length; i++)
            {
                if (meters[i] == null || design == null) continue;
                bool audible = manager.mix.Audible((StemId)i);
                float target = audible && audio != null ? Mathf.Clamp01(audio.Level((StemId)i) * 3.2f) : 0;
                // Fast attack, slow release, like a real meter.
                meterLevels[i] = target > meterLevels[i] ? Mathf.Lerp(meterLevels[i], target, 1 - Mathf.Exp(-30 * dt)) : Mathf.Lerp(meterLevels[i], target, 1 - Mathf.Exp(-5 * dt));
                var s = meters[i].transform.localScale;
                Apply(meters[i], design.colors[i], 2, new Vector2(s.x, s.z), meterLevels[i], meterLevels[i], audible ? 1.3f : .45f);
            }
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] == null) continue;
                RoomSurface surface = default; Vector3 point = default;
                bool mounted = spatial && spatialField.Mounted(i, out surface, out point);
                rings[i].enabled = mounted;
                if (!mounted || design == null) continue;
                float size = spatialField.orbDiameter * 2.8f;
                rings[i].transform.SetPositionAndRotation(point + surface.normal * .003f, Quaternion.LookRotation(surface.axisV, surface.normal));
                rings[i].transform.localScale = new Vector3(size, 1, size);
                bool audible = manager.mix.Audible((StemId)i);
                float level = audible && audio != null ? Mathf.Clamp01(audio.Level((StemId)i) * 3f) : 0;
                Apply(rings[i], design.colors[i], 1, new Vector2(size, size), level, 1, audible ? 1.2f : .4f);
            }
        }

        // One ripple per beat of the current song, from its detected tempo and first downbeat.
        void TickBeats(float dt)
        {
            ages += Vector4.one * dt;
            var song = manager.library != null && manager.library.songs != null && manager.library.songs.Length > 0 ? manager.library.Current : null;
            float now = manager.clock.Time;
            bool advancing = now != lastClock; lastClock = now;
            if (song == null || !BeatGrid.Valid(song.bpm) || !advancing) return;
            int beat = Mathf.FloorToInt((now - song.beatOffset) / BeatGrid.BeatLength(song.bpm));
            if (beat == lastBeat) return;
            bool first = lastBeat == int.MinValue; lastBeat = beat;
            if (first) return;
            float drums = manager.mix.Audible(StemId.Drums) && manager.audioManager != null ? manager.audioManager.Level(StemId.Drums) : 0;
            float strength = Mathf.Clamp(.3f + drums * 2.5f, .3f, 1) * (((beat % BeatGrid.BeatsPerBar) + BeatGrid.BeatsPerBar) % BeatGrid.BeatsPerBar == 0 ? 1.25f : .8f);
            ages = new Vector4(0, ages.x, ages.y, ages.z);
            strengths = new Vector4(strength, strengths.x, strengths.y, strengths.z);
        }

        void Apply(Renderer r, Color color, float shape, Vector2 size, float level, float fill, float intensity)
        {
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetFloat(ShapeId, shape);
            block.SetVector(SizeId, new Vector4(size.x, size.y, 0, 0));
            block.SetFloat(LevelId, level);
            block.SetFloat(FillId, fill);
            block.SetFloat(IntensityId, intensity);
            r.SetPropertyBlock(block);
        }
    }
}
