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
        SongMetadata cachedSong;
        WaveformCache waveform;
        public int ColumnCount => vertices == null ? 0 : vertices.Length/4;
        public Mesh WaveformMesh => mesh;
        WaveformCache CurrentWaveform()
        {
            var song=manager.library.Current;
            if(!ReferenceEquals(song,cachedSong))
            {
                cachedSong=song;waveform=null;
                if(song.stems!=null) foreach(var data in song.stems) if(data.id==stem) { waveform=data.waveform;break; }
            }
            return waveform;
        }
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
            int n = Mathf.Clamp(manager.design.waveformColumns,128,512);
            mesh = new Mesh { name = stem + " audio waveform" }; mesh.MarkDynamic();
            vertices = new Vector3[n * 4]; colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int k=i*6,v=i*4; triangles[k]=v;triangles[k+1]=v+2;triangles[k+2]=v+1;triangles[k+3]=v+1;triangles[k+4]=v+2;triangles[k+5]=v+3;
                uv[v]=new Vector2(0,0);uv[v+1]=new Vector2(0,1);uv[v+2]=new Vector2(1,0);uv[v+3]=new Vector2(1,1);
            }
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
        // Editing always targets the stable cylindrical lane baseline, including silence.
        public float HeightAt(float localX) => 0;
        public WaveformCache.Envelope EnvelopeAt(float coordinate,float span)
        {
            var cache=CurrentWaveform();
            if(cache==null) return default; // Missing/placeholder audio is flat, never synthesized.
            var result=cache.Query(Timestamp(coordinate),span);
            float gain=manager.automation.EvaluateGain(manager.library.Current.id,stem,Timestamp(coordinate),manager.clock.duration,manager.design.gainBoundaryFade);
            result.minimum*=gain;result.maximum*=gain;result.rms*=gain;
            return result;
        }
        void LateUpdate() => UpdateWaveform();
        public void UpdateWaveform()
        {
            var d = manager.design;
            visualTime = manager.clock.elapsed;
            int n = ColumnCount;
            var song = manager.library.Current;
            float span=(d.curvedTimeline?manager.zoom.VisibleSeconds:d.visibleSeconds)/n;
            float heightLimit=d.laneHeight*.5f-.003f;
            for (int i=0;i<n;i++)
            {
                float x = (i/(float)n-.5f)*d.width, t=Timestamp(x);
                float effect=manager.automation.Evaluate(song.id,stem,ParameterType.Effect,t,manager.clock.duration);
                float gain=manager.automation.EvaluateGain(song.id,stem,t,manager.clock.duration,d.gainBoundaryFade);
                var envelope=EnvelopeAt(x,span);
                float low=Mathf.Max(-heightLimit,d.WaveformHeight(Mathf.Min(0,envelope.minimum))-d.waveformBaseline);
                float high=Mathf.Min(heightLimit,d.WaveformHeight(Mathf.Max(0,envelope.maximum))+d.waveformBaseline);
                int v=i*4;float right=x+d.width/n;
                vertices[v]=d.ArcPoint(x,low); vertices[v+1]=d.ArcPoint(x,high);
                vertices[v+2]=d.ArcPoint(right,low); vertices[v+3]=d.ArcPoint(right,high);
                Color c=Color.Lerp(d.colors[(int)stem],Color.white,effect*.4f);
                c=Color.Lerp(c,Color.white,Mathf.Clamp01(Mathf.Abs(gain-1))*.45f);
                if(d.curvedTimeline && Mathf.Abs(x/d.width*d.arcAngle)>d.interactiveAngle*.5f) c*=.45f;
                c*=d.emission*(selected && InRegion(t)?d.highlight:1);
                colors[v]=colors[v+1]=colors[v+2]=colors[v+3]=c;
            }
            mesh.SetVertices(vertices,0,vertices.Length,UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetColors(colors);
        }
        void OnDestroy() { if(mesh != null) Destroy(mesh); }
    }
}

