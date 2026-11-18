using UnityEngine;
using UnityEngine.Rendering;

namespace RemixTable
{
    // Shared per-stem renderer. Retains its original type/GUID for compatibility.
    // Visual-only derivatives of the final filled mesh; no audio sampling or colliders.
    [DisallowMultipleComponent]
    public sealed class VocalsHologram : MonoBehaviour
    {
        public const string BodyPath = "VocalsHolographicBody";
        public const string HaloPath = "VocalsHolographicHalo";
        public const string StemBodyPath = "StemHolographicBody", StemHaloPath = "StemHolographicHalo";
        public static string BodyFor(StemId id) => id==StemId.Vocals?BodyPath:StemBodyPath;
        public static string HaloFor(StemId id) => id==StemId.Vocals?HaloPath:StemHaloPath;
        public const float Depth = .003f, HaloWidth = .006f;
        Mesh sourceMesh, shell, halo;
        Vector3[] shellVertices, haloVertices;
        Color[] bodyColors, shellColors, haloColors;
        Vector4[] derivedAutomation;
        RemixDesign design;
        StemId stem;
        MaterialPropertyBlock contactBlock;
        Renderer bodyRenderer,depthRenderer,haloRenderer;
        static readonly int Contact0=Shader.PropertyToID("_Contact0"),Contact1=Shader.PropertyToID("_Contact1");
        public StemId Stem => stem;
        public Mesh DepthMesh => shell;
        public Mesh HaloMesh => halo;
        public static string DepthName(StemId id) => (id==StemId.Melody?"Other":id.ToString())+" depth";
        public static string HaloName(StemId id) => (id==StemId.Melody?"Other":id.ToString())+" halo";
        public void Initialize(Mesh source, RemixDesign settings,StemId id=StemId.Vocals)
        {
            design=settings;
            stem=id;
            sourceMesh=source;
            var body=Resources.Load<Material>(BodyFor(stem));
            var glow=Resources.Load<Material>(HaloFor(stem));
            if(body==null || glow==null) throw new System.InvalidOperationException("Vocals hologram materials missing");
            GetComponent<MeshRenderer>().sharedMaterial=body;
            int pairs=source.vertexCount/2;
            shellVertices=new Vector3[pairs*4];haloVertices=new Vector3[pairs*4];
            shellColors=new Color[pairs*4];haloColors=new Color[pairs*4];
            bodyColors=new Color[source.vertexCount];
            derivedAutomation=new Vector4[pairs*4];
            var uv=new Vector2[pairs*4];
            var sides=new int[(pairs-1)*18];var fringe=new int[(pairs-1)*12];
            for(int i=0;i<pairs;i++)
            {
                int v=i*4;
                uv[v]=new Vector2(0,0);uv[v+1]=new Vector2(0,1);
                uv[v+2]=new Vector2(1,0);uv[v+3]=new Vector2(1,1);
                if(i==pairs-1) continue;
                Quad(sides,i*18,v+2,v+3,v+6,v+7); // Rear surface.
                Quad(sides,i*18+6,v,v+2,v+4,v+6);
                Quad(sides,i*18+12,v+1,v+5,v+3,v+7);
                Quad(fringe,i*12,v,v+1,v+4,v+5);
                Quad(fringe,i*12+6,v+2,v+3,v+6,v+7);
            }
            shell=CreateVisual(DepthName(stem),shellVertices,uv,sides,body);
            halo=CreateVisual(HaloName(stem),haloVertices,uv,fringe,glow);
            bodyRenderer=GetComponent<MeshRenderer>();
            depthRenderer=transform.Find(DepthName(stem)).GetComponent<MeshRenderer>();
            haloRenderer=transform.Find(HaloName(stem)).GetComponent<MeshRenderer>();
            contactBlock=new MaterialPropertyBlock();
            if(stem!=StemId.Vocals)StemHologramPalette.For(stem).Apply(contactBlock);
            contactBlock.SetVector("_ContactGeometry",new Vector4(design.Radius,design.curvedTimeline?1:0,0,0));
            contactBlock.SetVector("_ContactCoefficients",new Vector4(RibbonContactGlow.CoreHover,RibbonContactGlow.CoreGrab,RibbonContactGlow.RimHover,RibbonContactGlow.RimGrab));
            contactBlock.SetVector("_ContactHaloCoefficients",new Vector4(RibbonContactGlow.HaloHover,RibbonContactGlow.HaloGrab,0,0));
            SetContacts(Vector4.zero,Vector4.zero);
        }
        public void SetContacts(Vector4 first,Vector4 second)
        {
            if(stem==StemId.Vocals && first==Vector4.zero && second==Vector4.zero)
            {
                // Retain the approved Vocals idle path, including its empty renderer property block.
                bodyRenderer.SetPropertyBlock(null);depthRenderer.SetPropertyBlock(null);haloRenderer.SetPropertyBlock(null);return;
            }
            contactBlock.SetVector(Contact0,first);contactBlock.SetVector(Contact1,second);
            bodyRenderer.SetPropertyBlock(contactBlock);depthRenderer.SetPropertyBlock(contactBlock);haloRenderer.SetPropertyBlock(contactBlock);
        }
        static void Quad(int[] triangles,int k,int a,int b,int c,int d)
        {
            triangles[k]=a;triangles[k+1]=b;triangles[k+2]=c;
            triangles[k+3]=c;triangles[k+4]=b;triangles[k+5]=d;
        }
        Mesh CreateVisual(string label,Vector3[] points,Vector2[] uv,int[] triangles,Material material)
        {
            var child=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));
            child.transform.SetParent(transform,false);
            var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var mesh=new Mesh{name=label};mesh.MarkDynamic();mesh.vertices=points;mesh.uv=uv;mesh.triangles=triangles;
            mesh.bounds=new Bounds(new Vector3(0,0,-design.Radius),new Vector3(4,2,4));
            child.GetComponent<MeshFilter>().sharedMesh=mesh;return mesh;
        }
        // UV2: signed filter, semitones, harmonic lines/meter, line strength.
        // Zero automation encodes exactly zero, preserving the existing shader appearance.
        public static Vector4 EncodeAutomation(float filter,float semitones)
        {
            filter=StemFilter.SafeEffect(filter);
            if(float.IsNaN(semitones) || float.IsInfinity(semitones)) semitones=0;
            semitones=Mathf.Clamp(semitones,-PitchShiftDsp.MaxSemitones,PitchShiftDsp.MaxSemitones);
            if(semitones==0) return new Vector4(filter,0,0,0);
            return new Vector4(filter,semitones,250*Mathf.Pow(2,semitones/12),Mathf.Sqrt(Mathf.Abs(semitones)/12));
        }
        public void UpdateGeometry(Vector3[] points,Color[] colors,Vector4[] automation=null)
        {
            float reference=Mathf.Max(.001f,design.colors[(int)stem].maxColorComponent*design.emission);
            for(int i=0;i<points.Length/2;i++)
            {
                int s=i*2,v=i*4;
                Vector3 low=points[s],high=points[s+1];
                Vector3 back=design.curvedTimeline ? new Vector3(low.x,0,low.z+design.Radius).normalized*Depth : Vector3.forward*Depth;
                shellVertices[v]=low;shellVertices[v+1]=high;
                shellVertices[v+2]=low+back;shellVertices[v+3]=high+back;
                haloVertices[v]=low;haloVertices[v+1]=low-Vector3.up*HaloWidth;
                haloVertices[v+2]=high;haloVertices[v+3]=high+Vector3.up*HaloWidth;
                float level=colors[s].maxColorComponent/reference;
                // Convert existing editing/mute brightness to a neutral intensity; palette stays saturated.
                bodyColors[s]=bodyColors[s+1]=new Color(level,level,level,1);
                for(int j=0;j<4;j++)
                {
                    shellColors[v+j]=new Color(level*.48f,level*.48f,level*.48f,1);
                    haloColors[v+j]=new Color(level,level,level,1);
                    if(automation!=null) derivedAutomation[v+j]=automation[s];
                }
            }
            shell.SetVertices(shellVertices,0,shellVertices.Length,MeshUpdateFlags.DontRecalculateBounds);shell.SetColors(shellColors);
            halo.SetVertices(haloVertices,0,haloVertices.Length,MeshUpdateFlags.DontRecalculateBounds);halo.SetColors(haloColors);
            // The original mesh's vertex positions, UVs and indices remain untouched.
            sourceMesh.SetColors(bodyColors);
            if(automation!=null)
            {
                sourceMesh.SetUVs(2,automation);
                shell.SetUVs(2,derivedAutomation);
                halo.SetUVs(2,derivedAutomation);
            }
        }
        void OnDestroy()
        {
            Release(shell);Release(halo);
        }
        static void Release(Mesh mesh)
        {
            if(mesh==null) return;
            if(Application.isPlaying) Destroy(mesh);else DestroyImmediate(mesh);
        }
    }
}
