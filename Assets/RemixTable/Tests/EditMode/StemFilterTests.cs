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
        // Regression: clockwise (high-pass) felt dead because it stayed under ~170 Hz at a 45° twist.
        // From a 30° twist (1/3 of the default 90° sweep), each direction must reach the band where
        // vocals/drums/melody audibly change: high-pass above ~300 Hz, low-pass below ~5 kHz.
        [TestCase(.334f)] [TestCase(.5f)] [TestCase(.67f)] [TestCase(1f)]
        public void BothDirectionsBecomeAudibleAtTheSameTwist(float amount)
        {
            Assert.GreaterOrEqual(StemFilter.HighpassCutoff(amount),300f,$"high-pass too weak at {amount}");
            Assert.LessOrEqual(StemFilter.LowpassCutoff(-amount),5000f,$"low-pass too weak at {amount}");
        }
        [Test] public void HalfTwistHighpassThinsMoreThanJustSubBass()
        {
            Assert.GreaterOrEqual(StemFilter.HighpassCutoff(.5f),500f);
            Assert.LessOrEqual(StemFilter.LowpassCutoff(-.5f),3000f);
        }
        [Test] public void TransportShowsTwistDirectionAndStrength()
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
        [Test] public void TwistMappingRespectsRangeAndInversion()
        {
            var design=ScriptableObject.CreateInstance<RemixDesign>();
            design.filterTwistDegrees=90;
            Assert.AreEqual(.5f,design.MapTwistEffect(0,45),.0001f);
            Assert.AreEqual(-1,design.MapTwistEffect(0,-200),.0001f);
            design.invertFilterTwist=true;
            Assert.AreEqual(-.5f,design.MapTwistEffect(0,45),.0001f);
            Object.DestroyImmediate(design);
        }
        [TestCase(false)] [TestCase(true)] public void TwistGestureWritesBipolarEffectWithoutGain(bool invert)
        {
            var root=new GameObject("twist fixture");root.SetActive(false);
            var manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();manager.design.invertFilterTwist=invert;
            manager.library=root.AddComponent<SongLibraryManager>();manager.library.songs=new[]{new SongMetadata{id="twist",duration=32}};
            manager.clock.Reset(32);manager.clock.elapsed=10;
            var go=new GameObject("ribbon");go.transform.SetParent(root.transform);
            var ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=StemId.Melody;ribbon.visualTime=10;
            manager.ribbons=new[]{ribbon};
            var gestures=root.AddComponent<RibbonInteractionManager>();gestures.manager=manager;gestures.enabled=false;
            gestures.Mode=RibbonInteractionManager.ControlMode.Filter;
            root.SetActive(true);
            var hand=new RibbonInteractionManager.HandSample{valid=true,pinch=true,rollValid=true,roll=0,
                position=ribbon.transform.TransformPoint(new Vector3(0,ribbon.HeightAt(0),0))};
            gestures.ProcessHands(hand,default,.016f);
            float sign=invert?-1:1;
            hand.roll=-45;gestures.ProcessHands(hand,default,.016f);
            var edit=manager.automation.edits.Find(e=>e.parameter==ParameterType.Effect);
            Assert.IsNotNull(edit);Assert.AreEqual(-.5f*sign,edit.value,.0001f);
            hand.roll=0;gestures.ProcessHands(hand,default,.016f);
            hand.roll=45;gestures.ProcessHands(hand,default,.016f);
            hand.roll=90;gestures.ProcessHands(hand,default,.016f);
            hand.roll=135;gestures.ProcessHands(hand,default,.016f);
            Assert.AreEqual(1*sign,edit.value,.0001f); // Clamped at full sweep.
            Assert.IsFalse(manager.automation.edits.Exists(e=>e.parameter==ParameterType.Gain));
            gestures.Cancel();Object.DestroyImmediate(manager.design);Object.DestroyImmediate(root);
        }
    }
}
