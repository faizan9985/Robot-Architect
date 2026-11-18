using UnityEngine;
namespace RemixTable
{
    // Living background for the main menu, spread through the space around the listener: the loaded song's four
    // stems become wide, faint ribbons of light arcing across the field of view at different depths. Shapes come
    // from the baked waveforms (no audio needs to play), the song scrolls by at its real speed and each beat sends
    // a soft pulse through them. Right behind the menu they fade almost away so they never fight the buttons.
    // Placed at the seated head anchor; local +z is forward (the table's direction).
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RemixMenuAura : MonoBehaviour
    {
        public RemixTableManager manager;
        [Range(32, 256)] public int columns = 160;
        [Tooltip("Half of the arc the ribbons span, in degrees.")] public float arcDegrees = 105;
        [Tooltip("Seconds of song spread across the arc.")] public float window = 22;
        [Tooltip("Overall strength (alpha) of the ribbons.")] [Range(0, 1)] public float intensity = .22f;
        [Tooltip("Strength kept right behind the menu (fraction).")] [Range(0, 1)] public float centerStrength = .18f;
        [Tooltip("Half-angle of the faded zone behind the menu, degrees.")] public float centerClear = 24;
        public float maxHalfThickness = .16f;
        // Per ribbon (top to bottom: Vocals, Drums, Bass, Melody): distance, height, tilt (m per radian).
        public Vector3[] lanes = { new Vector3(2.9f, .95f, -.22f), new Vector3(2.4f, .38f, .2f), new Vector3(2.6f, -.42f, -.16f), new Vector3(3.2f, -.98f, .24f) };
        // Multiplier set by the menu (dimmer behind text-heavy pages).
        [Range(0, 1)] public float pageDim = 1;

        static readonly StemId[] Order = { StemId.Vocals, StemId.Drums, StemId.Bass, StemId.Melody };
        Mesh mesh;
        Vector3[] vertices; Color[] colors; Vector2[] uvs;
        float songTime, fade;

        void OnEnable() { fade = 0; Build(); }
        void Build()
        {
            if (mesh == null) { mesh = new Mesh { name = "MenuAura" }; mesh.MarkDynamic(); mesh.hideFlags = HideFlags.DontSave; GetComponent<MeshFilter>().sharedMesh = mesh; }
            int perRibbon = (columns + 1) * 2, count = perRibbon * Order.Length;
            if (vertices != null && vertices.Length == count) return;
            vertices = new Vector3[count]; colors = new Color[count]; uvs = new Vector2[count];
            var triangles = new int[columns * 6 * Order.Length];
            int t = 0;
            for (int r = 0; r < Order.Length; r++)
                for (int i = 0; i < columns; i++)
                {
                    int a = r * perRibbon + i * 2;
                    triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = a + 2;
                    triangles[t++] = a + 2; triangles[t++] = a + 1; triangles[t++] = a + 3;
                }
            mesh.Clear(); mesh.vertices = vertices; mesh.uv = uvs; mesh.colors = colors; mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10);
        }

        void LateUpdate()
        {
            if (manager == null || manager.design == null || manager.library == null || manager.library.songs == null || manager.library.songs.Length == 0) return;
            Build();
            var song = manager.library.Current;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            songTime += dt;
            fade = Mathf.MoveTowards(fade, manager.anchored ? 1 : 0, dt / 1.2f);
            float beat = 60f / Mathf.Max(40, song.bpm);
            float pulse = Mathf.Exp(-Mathf.Repeat(songTime - song.beatOffset, beat) / beat * 5);
            float arc = arcDegrees * Mathf.Deg2Rad, span = window / columns;
            int perRibbon = (columns + 1) * 2;
            for (int r = 0; r < Order.Length; r++)
            {
                WaveformCache cache = null;
                if (song.stems != null) foreach (var s in song.stems) if (s != null && s.id == Order[r]) cache = s.waveform;
                var c = manager.design.colors[(int)Order[r]];
                var lane = lanes[Mathf.Min(r, lanes.Length - 1)];
                for (int i = 0; i <= columns; i++)
                {
                    float u = (float)i / columns, angle = Mathf.Lerp(-arc, arc, u);
                    float level = cache != null
                        ? 1 - Mathf.Exp(-6 * cache.Query(songTime + (u - .5f) * window, span).rms)
                        : .35f + .25f * Mathf.Sin(u * 11 + now * .8f + r * 1.7f) * Mathf.Sin(u * 3.3f - now * .35f + r);
                    level = Mathf.Clamp01(level);
                    float half = Mathf.Max(.012f, maxHalfThickness * level * (1 + .2f * pulse));
                    float y = lane.y + lane.z * angle + .07f * Mathf.Sin(angle * 3.1f + now * .45f + r * 1.3f);
                    float radius = lane.x + .12f * Mathf.Sin(angle * 2.3f - now * .3f + r);
                    var center = new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
                    // Ends fade out; right behind the menu the ribbons nearly vanish.
                    float deg = Mathf.Abs(angle) * Mathf.Rad2Deg;
                    float ends = Mathf.SmoothStep(0, 1, (arcDegrees - deg) / 30f);
                    float clear = Mathf.Lerp(centerStrength, 1, Mathf.SmoothStep(centerClear * .55f, centerClear * 1.4f, deg));
                    float alpha = intensity * pageDim * fade * ends * clear * (.45f + .55f * level + .15f * pulse);
                    int v = r * perRibbon + i * 2;
                    vertices[v] = center + Vector3.up * half; vertices[v + 1] = center - Vector3.up * half;
                    uvs[v] = new Vector2(u, 1); uvs[v + 1] = new Vector2(u, -1);
                    var col = new Color(c.r, c.g, c.b, alpha);
                    colors[v] = col; colors[v + 1] = col;
                }
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uvs;
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
