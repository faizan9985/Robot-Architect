using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // Living background for the main menu: the loaded song's four stems drift across the card as soft glowing
    // ribbons, in the table's stem colors. The shapes come from the baked waveforms (no audio needs to play), the
    // song scrolls by at its real speed, and every beat sends a gentle pulse through them. Songs without a baked
    // waveform fall back to slow procedural waves.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RemixMenuRibbons : MaskableGraphic
    {
        public RemixTableManager manager;
        [Range(16, 160)] public int columns = 96;
        [Tooltip("Seconds of song visible across the card.")] public float window = 12;
        [Tooltip("Max half-thickness of a ribbon, in canvas units.")] public float amplitude = 46;
        [Range(0, 1)] public float coreAlpha = .55f;
        public float spread = 54, sway = 14;

        float songTime, beatPulse;
        readonly UIVertex[] quad = new UIVertex[4];

        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
        void Update()
        {
            if (manager == null || manager.library == null || manager.library.songs == null || manager.library.songs.Length == 0) return;
            var song = manager.library.Current;
            songTime += Time.unscaledDeltaTime;
            float beat = 60f / Mathf.Max(40, song.bpm), phase = Mathf.Repeat(songTime - song.beatOffset, beat) / beat;
            beatPulse = Mathf.Exp(-phase * 6); // Sharp at the beat, fading before the next.
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (manager == null || manager.design == null || manager.library == null || manager.library.songs == null || manager.library.songs.Length == 0) return;
            var song = manager.library.Current;
            var rect = rectTransform.rect;
            float t = Time.unscaledTime;
            // Top to bottom like the table: Vocals, Drums, Bass, Melody.
            var order = new[] { StemId.Vocals, StemId.Drums, StemId.Bass, StemId.Melody };
            for (int row = 0; row < order.Length; row++)
            {
                var stem = order[row];
                WaveformCache cache = null;
                if (song.stems != null) foreach (var s in song.stems) if (s != null && s.id == stem) cache = s.waveform;
                var c = manager.design.colors[(int)stem];
                float baseY = (1.5f - row) * spread, span = window / columns;
                Vector2 prevTop = default, prevBottom = default, prevCenter = default; float prevA = 0;
                for (int i = 0; i <= columns; i++)
                {
                    float u = (float)i / columns, x = Mathf.Lerp(rect.xMin, rect.xMax, u);
                    float level;
                    if (cache != null) level = 1 - Mathf.Exp(-6 * cache.Query(songTime + (u - .5f) * window, span).rms);
                    else level = .35f + .25f * Mathf.Sin(u * 9 + t * .9f + row * 1.7f) * Mathf.Sin(u * 3.1f - t * .4f + row);
                    float half = Mathf.Max(1.5f, amplitude * Mathf.Clamp01(level) * (1 + .25f * beatPulse));
                    float y = baseY + sway * Mathf.Sin(u * 5.2f + t * .55f + row * 1.3f);
                    // Fade at the card edges so the ribbons drift in and out instead of being cut off.
                    float edge = Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) * 6);
                    float a = coreAlpha * edge * (.55f + .45f * Mathf.Clamp01(level * 1.4f) + .2f * beatPulse);
                    var center = new Vector2(x, y); var top = new Vector2(x, y + half); var bottom = new Vector2(x, y - half);
                    if (i > 0)
                    {
                        // Two quads per column: bright core at the center line fading to clear at both edges.
                        Quad(vh, prevCenter, center, top, prevTop, c, prevA, a);
                        Quad(vh, prevBottom, bottom, center, prevCenter, c, prevA, a, bottomEdge: true);
                    }
                    prevTop = top; prevBottom = bottom; prevCenter = center; prevA = a;
                }
            }
        }
        // Vertices in order: a0,b0 on one side, b1,a1 on the other. The center-line side gets alpha, the outer edge 0.
        void Quad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color c, float a0, float a1, bool bottomEdge = false)
        {
            var tint = c * color;
            float inner0 = bottomEdge ? 0 : a0, inner1 = bottomEdge ? 0 : a1, outer0 = bottomEdge ? a0 : 0, outer1 = bottomEdge ? a1 : 0;
            Set(0, p0, tint, inner0); Set(1, p1, tint, inner1); Set(2, p2, tint, outer1); Set(3, p3, tint, outer0);
            vh.AddUIVertexQuad(quad);
        }
        void Set(int i, Vector2 p, Color c, float a) { c.a = a * color.a; quad[i] = new UIVertex { position = p, color = c }; }
    }
}
