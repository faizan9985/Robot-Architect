using UnityEngine;
namespace RemixTable
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RibbonView : MonoBehaviour
    {
        public RemixTableManager manager;
        public StemId stem;
        public bool selected, wholeStem;
        public float regionStart, regionEnd;
        public double visualTime;
        Mesh mesh;
        Vector3[] vertices;
        Color[] colors;
        public void SetSelected(bool value)
        {
            selected = value;
            if (manager == null || manager.audioManager == null) return;
            if (value) manager.audioManager.BeginEditing(this);
            else manager.audioManager.EndEditing(this);
            visualTime = manager.clock.elapsed;
        }
        public void ResetPhase() { SetSelected(false); visualTime = 0; }
        void OnDisable() => SetSelected(false);
        void Awake()
        {
            int n = manager.design.segments;
            mesh = new Mesh { name = stem + " procedural ribbon" }; mesh.MarkDynamic();
            vertices = new Vector3[(n + 1) * 2]; colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for(int i=0;i<=n;i++) { uv[i*2]=new Vector2(i/(float)n,0);uv[i*2+1]=new Vector2(i/(float)n,1); }
            var triangles = new int[n * 6];
            for (int i = 0; i < n; i++) { int k=i*6,v=i*2; triangles[k]=v;triangles[k+1]=v+2;triangles[k+2]=v+1;triangles[k+3]=v+1;triangles[k+4]=v+2;triangles[k+5]=v+3; }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.uv=uv;
            mesh.bounds = new Bounds(new Vector3(0,0,-manager.design.Radius), new Vector3(4,2,4));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        public float Timestamp(float localX) => manager.design.curvedTimeline ? manager.zoom.Timestamp(localX,visualTime,manager.clock.duration,manager.design) : Mathf.Repeat((float)(visualTime % manager.clock.duration) + manager.design.TimeOffset(localX), manager.clock.duration);
        public Vector3 SurfacePoint(float coordinate) => transform.TransformPoint(manager.design.ArcPoint(coordinate,HeightAt(coordinate)));
        public float LocalX(Vector3 world) => manager.design.ArcCoordinate(transform.InverseTransformPoint(world));
        public Bounds InteractionBounds => manager.design.curvedTimeline ?
            new Bounds(new Vector3(0,0,(Mathf.Cos(manager.design.HalfAngle)-1)*manager.design.Radius*.5f),
                new Vector3(2*manager.design.Radius,manager.design.laneHeight,(1-Mathf.Cos(manager.design.HalfAngle))*manager.design.Radius+manager.design.interactionDepth)) :
            new Bounds(Vector3.zero,new Vector3(manager.design.width+.016f,manager.design.laneHeight,manager.design.interactionDepth));
        public bool CanSelectAt(Vector3 world, out float distance)
        {
            var p=transform.InverseTransformPoint(world);var d=manager.design;
            float coordinate=d.ArcCoordinate(p);
            if(d.curvedTimeline)
            {
                float angle=Mathf.Atan2(p.x,p.z+d.Radius);
                float radial=new Vector2(p.x,p.z+d.Radius).magnitude-d.Radius;
                distance=new Vector2(p.y-HeightAt(coordinate),radial).magnitude;
                return Mathf.Abs(angle)<=Mathf.Min(d.arcAngle,d.interactiveAngle)*Mathf.Deg2Rad*.5f &&
                    Mathf.Abs(p.y)<=d.laneHeight*.5f && Mathf.Abs(radial)<=d.interactionDepth*.5f && distance<d.interactionRadius;
            }
            distance=new Vector2(p.y-HeightAt(p.x),p.z).magnitude;
            return (!d.constrainToLanes || InteractionBounds.Contains(p)) && Mathf.Abs(p.x)<=d.width*.5f+.025f && distance<d.interactionRadius;
        }
        public bool InRegion(float t) => wholeStem || (regionStart <= regionEnd ? t >= regionStart && t <= regionEnd : t >= regionStart || t <= regionEnd);
        public float HeightAt(float localX)
        {
            var d=manager.design;var song=manager.library.Current;float t=Timestamp(localX);
            float pitch=manager.automation.Evaluate(song.id,stem,ParameterType.Pitch,t,manager.clock.duration);
            float gain=manager.automation.EvaluateGain(song.id,stem,t,manager.clock.duration,d.gainBoundaryFade);
            float effect=manager.automation.Evaluate(song.id,stem,ParameterType.Effect,t,manager.clock.duration);
            float phase=(t*song.bpm/60f*d.animationSpeed+(int)stem*.37f+song.seed)*Mathf.PI*2;
            float height=(Mathf.Sin(phase*(1+(int)stem*.5f))*.65f+Mathf.Sin(phase*2.17f)*.25f)*d.amplitude*gain
                +pitch/d.maxSemitones*d.pitchDistance+Mathf.Sin(phase*4)*effect*.008f;
            return d.constrainToLanes ? Mathf.Clamp(height,-d.WaveCenterLimit,d.WaveCenterLimit) : height;
        }
        void LateUpdate()
        {
            var d = manager.design;
            visualTime = manager.clock.elapsed;
            int n = vertices.Length/2-1;
            var song = manager.library.Current;
            for (int i=0;i<=n;i++)
            {
                float x = (i/(float)n-.5f)*d.width, t=Timestamp(x);
                float effect=manager.automation.Evaluate(song.id,stem,ParameterType.Effect,t,manager.clock.duration);
                float gain=manager.automation.EvaluateGain(song.id,stem,t,manager.clock.duration,d.gainBoundaryFade);
                float y=HeightAt(x); if(manager.audioManager != null) y += Mathf.Clamp(Mathf.Sin(i*.35f)*manager.audioManager.Level(stem)*.006f*gain,-.003f,.003f);
                float thick=d.thickness*(selected && InRegion(t)?1.8f:1);
                vertices[i*2]=d.ArcPoint(x,y-thick); vertices[i*2+1]=d.ArcPoint(x,y+thick);
                Color c=Color.Lerp(d.colors[(int)stem],Color.white,effect*.4f);
                c=Color.Lerp(c,Color.white,Mathf.Clamp01(Mathf.Abs(gain-1))*.45f);
                if(d.curvedTimeline && Mathf.Abs(x/d.width*d.arcAngle)>d.interactiveAngle*.5f) c*=.45f;
                c*=d.emission*(selected && InRegion(t)?d.highlight:1);
                colors[i*2]=colors[i*2+1]=c;
            }
            mesh.vertices=vertices;mesh.colors=colors;
        }
        void OnDestroy() { if(mesh != null) Destroy(mesh); }
    }
}

