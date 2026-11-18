using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace RemixTable
{
    // SPATIAL mode: one glowing orb per stem placed around the listener. Grab an orb (near or far, hands or
    // controllers) and move it: that stem's sound follows it left / right and gets quieter as it moves away.
    // Holographic visuals observe XRI state; positions and audio remain owned by the existing spatial path.
    public sealed class SpatialStemField : MonoBehaviour
    {
        public RemixTableManager manager;
        // Indexed by (int)StemId. Each orb carries an XRGrabInteractable and a kinematic Rigidbody.
        public XRGrabInteractable[] orbs = new XRGrabInteractable[4];
        public Renderer[] orbRenderers = new Renderer[4];
        [Min(.01f)] public float orbDiameter = .09f;
        [HideInInspector] public float pulse = .45f; // Retained serialized field; physical-scale pulsing is retired.
        readonly bool[] held = new bool[4];
        readonly SpatialOrbHologram[] visuals = new SpatialOrbHologram[4];
        bool shown;
        void OnEnable()
        {
            InitializeVisuals();
            manager.WorkspaceAlignmentRequested+=CancelGrabs;
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
            manager.WorkspaceAlignmentRequested-=CancelGrabs;
            foreach(var visual in visuals)if(visual!=null)visual.ResetInteraction();
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;
                orbs[i].selectEntered.RemoveAllListeners(); orbs[i].selectExited.RemoveAllListeners();
                if (held[i]) Grab(i, false);
            }
        }
        void CancelGrabs()
        {
            foreach(var visual in visuals)if(visual!=null)visual.ResetInteraction();
            for(int i=0;i<orbs.Length;i++) if(orbs[i]!=null && held[i])
            {
                if(orbs[i].interactionManager!=null) orbs[i].interactionManager.CancelInteractableSelection((IXRSelectInteractable)orbs[i]);
                if(held[i]) Grab(i,false);
            }
        }
        public void InitializeVisuals()
        {
            for(int i=0;i<orbs.Length;i++)
            {
                if(orbs[i]==null || orbRenderers[i]==null)continue;
                var visual=orbs[i].GetComponent<SpatialOrbHologram>();
                if(visual==null)visual=orbs[i].gameObject.AddComponent<SpatialOrbHologram>();
                visual.Initialize((StemId)i,orbRenderers[i]);visuals[i]=visual;
            }
        }
        void Grab(int stem, bool grabbing)
        {
            if(grabbing && manager.workspaceInteraction!=null && manager.workspaceInteraction.OwnsGesture)
            {orbs[stem].interactionManager.CancelInteractableSelection((IXRSelectInteractable)orbs[stem]);return;}
            held[stem] = grabbing;
            if (grabbing) manager.BeginExternalHold(orbs[stem]);
            else
            {
                manager.EndExternalHold(orbs[stem]);
                manager.mix.SetPosition((StemId)stem, manager.SpatialLocal(orbs[stem].transform.position));
            }
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
            }
            if (!spatial || !manager.anchored) return;
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;
                orbs[i].enabled=!manager.WorkspaceAligning && (manager.workspaceInteraction==null || !manager.workspaceInteraction.OwnsGesture);
                var stem = (StemId)i; var t = orbs[i].transform;
                // Held orbs drive the mix; free orbs follow it (a loaded save or a re-anchored table).
                if (held[i]) manager.mix.SetPosition(stem, manager.SpatialLocal(t.position));
                else t.position = manager.SpatialWorld(manager.mix.positions[i]);
                if(visuals[i]!=null)
                    visuals[i].Advance(Time.unscaledDeltaTime,orbs[i].isHovered,orbs[i].isSelected,
                        manager.mix.Audible(stem),manager.mix.soloed[i],
                        manager.WorkspaceAligning || !manager.WorkspaceTracked || !orbs[i].isActiveAndEnabled);
            }
        }
    }
}
