using UnityEngine;
namespace RemixTable
{
    // Transient rendering state only. Hand identities and selection come from RibbonInteractionManager.
    public sealed class RibbonContactGlow
    {
        // Exact committed spatial-sphere interaction contributions; no changes to the sphere implementation.
        public const float CoreHover=.50f,CoreGrab=6.5f,RimHover=1.25f,RimGrab=8, HaloHover=1.25f,HaloGrab=10;
        public const float AttackSeconds=.06f,ReleaseSeconds=.25f;
        struct Contact
        {
            public float hover,grab,releaseFrom,releaseTime,offset;
            public bool selected,present;
            public Vector3 world;
            public double timestamp;
        }
        readonly Contact[] contacts=new Contact[2];
        public void Clear() { contacts[0]=contacts[1]=default; }
        public void ClearHand(int hand) => contacts[hand]=default;
        static float Window(RibbonView ribbon) => ribbon.manager.design.curvedTimeline?ribbon.manager.zoom.VisibleSeconds:ribbon.manager.design.visibleSeconds;
        public void Advance(RibbonView ribbon,int hand,float dt,bool hovered,bool selected,Vector3 world)
        {
            ref var c=ref contacts[hand];dt=Mathf.Max(0,dt);
            bool continuing=c.present;c.present=hovered || selected;
            if(c.present)
            {
                // Smooth hover jitter; an owned pinch always uses the actual contact without positional lag.
                c.world=continuing && hovered && !selected?Vector3.Lerp(c.world,world,1-Mathf.Exp(-dt/.035f)):world;
                c.offset=ribbon.LocalX(c.world)/ribbon.manager.design.width*Window(ribbon);
                c.timestamp=ribbon.manager.clock.elapsed+c.offset;
            }
            if(selected) {c.grab=Mathf.MoveTowards(c.grab,1,dt/AttackSeconds);c.selected=true;}
            else
            {
                if(c.selected){c.releaseFrom=c.grab;c.releaseTime=0;c.selected=false;}
                c.releaseTime=Mathf.Min(ReleaseSeconds,c.releaseTime+dt);
                c.grab=c.releaseFrom*(1-Mathf.SmoothStep(0,1,c.releaseTime/ReleaseSeconds));
            }
            c.hover=Mathf.MoveTowards(c.hover,hovered?1:0,dt/.10f);
        }
        public Vector4 ShaderInput(RibbonView ribbon,int hand)
        {
            ref var c=ref contacts[hand];var d=ribbon.manager.design;
            if(c.hover==0 && c.grab==0)return Vector4.zero;
            float coordinate;
            if(c.present)coordinate=ribbon.LocalX(c.world);
            else
            {
                // Keep the released spot on its song-time occurrence, including full-song wrap.
                // Retain the nearest previous occurrence when a short song appears repeatedly in the window.
                float offset=(float)(c.timestamp-ribbon.manager.clock.elapsed),duration=ribbon.manager.clock.duration;
                if(duration>0)offset+=Mathf.Round((c.offset-offset)/duration)*duration;
                c.offset=offset;coordinate=offset/Window(ribbon)*d.width;
            }
            float arc=d.curvedTimeline?coordinate/d.width*2*d.HalfAngle*d.Radius:coordinate;
            float scale=Mathf.Max(.0001f,ribbon.transform.lossyScale.x);
            return new Vector4(arc,Mathf.Max(.005f,d.contactGlowRadius)/scale,c.hover,c.grab);
        }
    }
}
