using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    public class StemFilterTests
    {
        [Test] public void NeutralEffectLeavesBothFiltersTransparent()
        {
            Assert.AreEqual(StemFilter.MaxCutoff,StemFilter.LowpassCutoff(0),.01f);
            Assert.AreEqual(StemFilter.MinHighpassCutoff,StemFilter.HighpassCutoff(0),.01f);
            Assert.AreEqual(StemFilter.BaseResonance,StemFilter.Resonance(0),.0001f);
        }
        [Test] public void NegativeSweepsLowpassOnlyAndPositiveSweepsHighpassOnly()
        {
            Assert.AreEqual(StemFilter.MinLowpassCutoff,StemFilter.LowpassCutoff(-1),.5f);
            Assert.AreEqual(StemFilter.MinHighpassCutoff,StemFilter.HighpassCutoff(-1),.01f);
            Assert.AreEqual(StemFilter.MaxHighpassCutoff,StemFilter.HighpassCutoff(1),.5f);
            Assert.AreEqual(StemFilter.MaxCutoff,StemFilter.LowpassCutoff(1),.01f);
            // Exponential: the half-way point is the geometric mean, not the arithmetic one.
            Assert.AreEqual(Mathf.Sqrt(StemFilter.MaxCutoff*StemFilter.MinLowpassCutoff),StemFilter.LowpassCutoff(-.5f),1f);
            Assert.Less(StemFilter.LowpassCutoff(-.6f),StemFilter.LowpassCutoff(-.3f));
            Assert.Greater(StemFilter.HighpassCutoff(.6f),StemFilter.HighpassCutoff(.3f));
        }
        // Regression: the high-pass direction felt dead because it stayed under ~170 Hz at half travel.
        // From 1/3 of the sweep (4 cm of lift), each direction must reach the band where
        // vocals/drums/melody audibly change: high-pass above ~300 Hz, low-pass below ~5 kHz.
        [TestCase(.334f)] [TestCase(.5f)] [TestCase(.67f)] [TestCase(1f)]
        public void BothDirectionsBecomeAudibleAtTheSameTravel(float amount)
        {
            Assert.GreaterOrEqual(StemFilter.HighpassCutoff(amount),300f,$"high-pass too weak at {amount}");
            Assert.LessOrEqual(StemFilter.LowpassCutoff(-amount),5000f,$"low-pass too weak at {amount}");
        }
        [Test] public void HalfTravelHighpassThinsMoreThanJustSubBass()
        {
            Assert.GreaterOrEqual(StemFilter.HighpassCutoff(.5f),500f);
            Assert.LessOrEqual(StemFilter.LowpassCutoff(-.5f),3000f);
        }
        [Test] public void TransportShowsFilterDirectionAndStrength()
        {
            Assert.AreEqual("LOW 50%",RemixTransport.GestureLabel(null,-50));
            Assert.AreEqual("HIGH 40%",RemixTransport.GestureLabel(null,40));
            Assert.AreEqual("FLAT",RemixTransport.GestureLabel(null,0));
            Assert.AreEqual("VOL 120%",RemixTransport.GestureLabel(120,null));
            Assert.AreEqual("VOL 120%  HIGH 40%",RemixTransport.GestureLabel(120,40));
            Assert.AreEqual("",RemixTransport.GestureLabel(null,null));
            Assert.LessOrEqual(RemixTransport.GestureLabel(250,-100).Length,24,"fits the 210px time slot");
        }
        [Test] public void InvalidValuesAreClampedOrNeutral()
        {
            Assert.AreEqual(0,StemFilter.SafeEffect(float.NaN));
            Assert.AreEqual(0,StemFilter.SafeEffect(float.PositiveInfinity));
            Assert.AreEqual(-1,StemFilter.SafeEffect(-7));
            var store=new AutomationStore();
            store.Upsert(new AutomationEdit{songId="s",stem=StemId.Vocals,parameter=ParameterType.Effect,wholeStem=true,value=3});
            Assert.AreEqual(1,store.Evaluate("s",StemId.Vocals,ParameterType.Effect,5,32));
        }
        [Test] public void LiftMappingIsUpThinDownMuffledAndClamped()
        {
            var design=ScriptableObject.CreateInstance<RemixDesign>();
            design.filterDragDistance=.12f;
            Assert.AreEqual(.5f,design.MapDragFilter(0,.06f),.0001f,"up = high-pass");
            Assert.AreEqual(-.5f,design.MapDragFilter(0,-.06f),.0001f,"down = low-pass");
            Assert.AreEqual(0,design.MapDragFilter(0,0),.0001f,"start height = neutral");
            Assert.AreEqual(1,design.MapDragFilter(0,.5f),.0001f);
            Assert.AreEqual(-1,design.MapDragFilter(0,-.5f),.0001f);
            Assert.AreEqual(.25f,design.MapDragFilter(-.25f,.06f),.0001f,"continues from an existing filter value");
            Object.DestroyImmediate(design);
        }
    }
}
