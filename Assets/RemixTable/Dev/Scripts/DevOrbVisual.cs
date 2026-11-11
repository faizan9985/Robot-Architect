using UnityEngine;

namespace RemixTable.Dev
{
    /// <summary>
    /// Placeholder orb look for the sandbox: size and brightness pulse with the stem's post-fader level from
    /// <see cref="StemAnalyzer"/>; grey when muted, white-hot when soloed. Uses a MaterialPropertyBlock, so no
    /// materials are created. Faizan's real orbs replace this with a shader driven by the same getters.
    /// </summary>
    [AddComponentMenu("Remix Table/Dev/Dev Orb Visual")]
    public sealed class DevOrbVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] StemAnalyzer analyzer;
        [SerializeField] StemOrb orb;
        [SerializeField] Renderer target;
        [SerializeField] Color baseColor = Color.white;
        [SerializeField] float pulseScale = 0.6f;

        MaterialPropertyBlock block;
        Vector3 baseScale;

        public void Configure(StemAnalyzer stemAnalyzer, StemOrb stemOrb, Renderer renderer, Color color)
        {
            analyzer = stemAnalyzer;
            orb = stemOrb;
            target = renderer;
            baseColor = color;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (target == null) target = GetComponent<Renderer>();
            baseScale = transform.localScale;
        }

        void LateUpdate()
        {
            if (analyzer == null || orb == null || target == null) return;
            float level = analyzer.GetPostFaderLevel01(orb.StemIndex);
            transform.localScale = baseScale * (1f + pulseScale * level);

            Color c;
            if (orb.IsMuted && !orb.IsSoloed) c = Color.Lerp(baseColor, Color.gray, 0.8f) * 0.35f;
            else if (orb.IsSoloed) c = Color.Lerp(baseColor, Color.white, 0.5f) * (0.8f + 0.6f * level);
            else c = baseColor * (0.35f + 1.1f * level);
            if (orb.IsHeld) c = Color.Lerp(c, Color.white, 0.25f);
            c.a = 1f;

            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            target.SetPropertyBlock(block);
        }
    }
}
