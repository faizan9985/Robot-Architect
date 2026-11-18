using UnityEngine;
using UnityEngine.Rendering;
namespace RemixTable
{
    // Visual only: XRI selection/hover are supplied by SpatialStemField. Never moves/scales a grab target.
    [DisallowMultipleComponent]
    public sealed class SpatialOrbHologram : MonoBehaviour
    {
        public const string CorePath="SpatialHolographicCore",HaloPath="SpatialHolographicHalo";
        public const float ReleaseSeconds=.25f,AttackSeconds=.06f;
        public const string HaloName="Holographic Halo";
        Renderer core,halo;
        MaterialPropertyBlock block;
        bool initialized,wasSelected,audible=true,soloed;
        float releaseFrom,releaseTime;
        public StemId Stem {get;private set;}
        public float GrabAmount {get;private set;}
        public float HoverAmount {get;private set;}
        public float CoreIntensity {get;private set;}
        public float RimIntensity {get;private set;}
        public float HaloIntensity {get;private set;}
        public float HaloExpansion {get;private set;}
        public Renderer HaloRenderer => halo;
        static readonly int CoreId=Shader.PropertyToID("_CoreIntensity"),RimId=Shader.PropertyToID("_RimIntensity"),
            HaloId=Shader.PropertyToID("_HaloIntensity"),ExpansionId=Shader.PropertyToID("_HaloExpansion");
        public void Initialize(StemId stem,Renderer renderer)
        {
            if(initialized)return;
            var body=Resources.Load<Material>(CorePath);var aura=Resources.Load<Material>(HaloPath);
            var filter=renderer.GetComponent<MeshFilter>();
            if(body==null || aura==null || filter==null || filter.sharedMesh==null)
                throw new System.InvalidOperationException("Spatial hologram mesh/material references missing");
            Stem=stem;core=renderer;core.sharedMaterial=body;
            var child=new GameObject(HaloName,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(transform,false);
            child.layer=gameObject.layer;child.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            halo=child.GetComponent<MeshRenderer>();halo.sharedMaterial=aura;
            halo.shadowCastingMode=ShadowCastingMode.Off;halo.receiveShadows=false;
            // Rendering-only bounds include shader expansion; the shared sphere mesh and collider stay untouched.
            halo.localBounds=new Bounds(Vector3.zero,Vector3.one*1.5f);
            block=new MaterialPropertyBlock();StemHologramPalette.For(stem).Apply(block);
            initialized=true;ResetInteraction();
        }
        public void Advance(float deltaTime,bool hovered,bool selected,bool isAudible,bool isSoloed,bool reset=false)
        {
            if(!initialized)return;
            audible=isAudible;soloed=isSoloed;
            if(reset){ResetInteraction();return;}
            float dt=Mathf.Max(0,deltaTime);
            if(selected)
            {
                GrabAmount=Mathf.MoveTowards(GrabAmount,1,dt/AttackSeconds);wasSelected=true;
            }
            else
            {
                if(wasSelected){releaseFrom=GrabAmount;releaseTime=0;wasSelected=false;}
                releaseTime=Mathf.Min(ReleaseSeconds,releaseTime+dt);
                GrabAmount=releaseFrom*(1-Mathf.SmoothStep(0,1,releaseTime/ReleaseSeconds));
            }
            HoverAmount=Mathf.MoveTowards(HoverAmount,hovered?1:0,dt/.10f);
            Apply();
        }
        public void ResetInteraction()
        {
            GrabAmount=HoverAmount=releaseFrom=releaseTime=0;wasSelected=false;
            if(initialized)Apply();
        }
        void Apply()
        {
            // Mixer audibility is the ceiling; solo follows StemMix's existing solo-wins-over-mute rule.
            float baseLevel=audible?(soloed?1.08f:1):.22f;
            float hover=HoverAmount*(1-GrabAmount);
            CoreIntensity=baseLevel*(1+6.5f*GrabAmount+.50f*hover);
            RimIntensity=baseLevel*(.70f+8.0f*GrabAmount+1.25f*hover);
            HaloIntensity=(audible?.35f:.10f)*(1+10f*GrabAmount+hover*1.25f)*(soloed && audible?1.08f:1);
            HaloExpansion=1.14f+.08f*GrabAmount+.025f*hover;
            block.SetFloat(CoreId,CoreIntensity);block.SetFloat(RimId,RimIntensity);
            block.SetFloat(HaloId,HaloIntensity);block.SetFloat(ExpansionId,HaloExpansion);
            if(core!=null)core.SetPropertyBlock(block);if(halo!=null)halo.SetPropertyBlock(block);
        }
        void OnDisable() => ResetInteraction();
    }
}
