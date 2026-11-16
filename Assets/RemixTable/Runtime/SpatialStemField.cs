using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace RemixTable
{
    // SPATIAL mode: one glowing orb per stem placed around the listener. Grab an orb (near or far, hands or
    // controllers) and move it: that stem's sound follows it left / right and gets quieter as it moves away.
    // Orbs pulse with their stem's level and dim when the stem is muted. Positions are saved with the remix.
    public sealed class SpatialStemField : MonoBehaviour
    {
        public RemixTableManager manager;
        // Indexed by (int)StemId. Each orb carries an XRGrabInteractable and a kinematic Rigidbody.
        public XRGrabInteractable[] orbs = new XRGrabInteractable[4];
        public Renderer[] orbRenderers = new Renderer[4];
        [Min(.01f)] public float orbDiameter = .09f;
        [Range(0, 1)] public float pulse = .45f;
        readonly bool[] held = new bool[4];
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), LegacyColor = Shader.PropertyToID("_Color");
        bool shown;
        void OnEnable()
        {
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;
                int stem = i;
                orbs[i].selectEntered.AddListener(_ => Grab(stem, true));
                orbs[i].selectExited.AddListener(_ => Grab(stem, false));
            }
            shown = !manager.mix.spatial; // Force the first refresh.
        }
        void OnDisable()
        {
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;
                orbs[i].selectEntered.RemoveAllListeners(); orbs[i].selectExited.RemoveAllListeners();
                if (held[i]) Grab(i, false);
            }
        }
        void Grab(int stem, bool grabbing)
        {
            held[stem] = grabbing;
            if (grabbing) manager.BeginExternalHold(orbs[stem]);
            else
            {
                manager.EndExternalHold(orbs[stem]);
                var position = orbs[stem].transform.position;
                // ROOM mode: let go near a real wall or table and the stem mounts on it like a speaker.
                if (manager.room != null && manager.room.TryClosestSurface(position, surfaceSnapDistance, out var surface, out var point))
                    position = point + surface.normal * orbDiameter * .65f;
                manager.mix.SetPosition((StemId)stem, manager.SpatialLocal(position));
            }
        }
        [Tooltip("ROOM mode: an orb released this close (meters) to a wall or table sticks to it.")]
        [Min(0)] public float surfaceSnapDistance = .3f;
        // True while the orb sits on a real surface (drawn with a mount ring by the room visuals).
        public bool Mounted(int stem, out RoomSurface surface, out Vector3 point)
        {
            surface = default; point = default;
            if (manager.room == null || stem < 0 || stem >= orbs.Length || orbs[stem] == null || !orbs[stem].gameObject.activeInHierarchy) return false;
            return manager.room.TryClosestSurface(orbs[stem].transform.position, orbDiameter * 1.2f, out surface, out point);
        }
        void LateUpdate()
        {
            bool spatial = manager.mix.spatial;
            if (spatial != shown)
            {
                shown = spatial;
                for (int i = 0; i < orbs.Length; i++)
                {
                    if (orbs[i] == null) continue;
                    if (!spatial && held[i]) Grab(i, false);
                    orbs[i].gameObject.SetActive(spatial);
                }
                snapNextFrame = true;
            }
            if (!spatial || !manager.anchored) return;
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;
                var stem = (StemId)i; var t = orbs[i].transform;
                // Held orbs drive the mix; free orbs follow it (a loaded save or a re-anchored table).
                if (held[i]) manager.mix.SetPosition(stem, manager.SpatialLocal(t.position));
                else
                {
                    // Glide to new spots (room speaker placement, a snapped release, the table moving) rather than teleport.
                    var target = manager.SpatialWorld(manager.mix.positions[i]);
                    t.position = snapNextFrame ? target : Vector3.Lerp(t.position, target, 1 - Mathf.Exp(-glide * Time.unscaledDeltaTime));
                }
                float level = manager.audioManager != null ? manager.audioManager.Level(stem) : 0;
                bool audible = manager.mix.Audible(stem);
                t.localScale = Vector3.one * orbDiameter * (1 + (audible ? level * pulse : 0)) * (held[i] ? 1.15f : 1);
                if (orbRenderers[i] != null && manager.design != null)
                {
                    var color = manager.design.colors[i] * (audible ? 1.4f + level : .25f); color.a = 1;
                    orbRenderers[i].GetPropertyBlock(block);
                    block.SetColor(BaseColor, color); block.SetColor(LegacyColor, color);
                    orbRenderers[i].SetPropertyBlock(block);
                }
            }
            snapNextFrame = false;
        }
        [Tooltip("How quickly free orbs glide to a new spot (1/s).")]
        [Min(.1f)] public float glide = 9;
        bool snapNextFrame = true;
    }
}
