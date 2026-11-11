using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    public class AudioAutomationTests
    {
        AutomationStore store;
        [SetUp] public void Setup() => store=new AutomationStore();
        AutomationEdit Gain(float start=2,float end=4,float value=.2f,bool whole=false,string song="a",StemId stem=StemId.Bass) =>
            new AutomationEdit{songId=song,stem=stem,parameter=ParameterType.Gain,start=start,end=end,value=value,wholeStem=whole};
        float At(float t, string song="a",StemId stem=StemId.Bass) => store.EvaluateGain(song,stem,t,10,.1f);
        [Test] public void NeutralAndRegionStemSongIsolation()
        {
            Assert.AreEqual(1,At(3));store.Upsert(Gain());
            Assert.AreEqual(.2f,At(3));Assert.AreEqual(1,At(1));Assert.AreEqual(1,At(5));
            Assert.AreEqual(1,At(3,"b"));Assert.AreEqual(1,At(3,"a",StemId.Drums));
            Assert.AreEqual(.2f,At(23));
        }
        [Test] public void WholeGainMultipliesRegionAndLimitsFinalGain()
        {
            store.Upsert(Gain(value:2,whole:true));store.Upsert(Gain(value:.25f));
            Assert.AreEqual(.5f,At(3));Assert.AreEqual(2,At(5));
            store.Upsert(Gain(value:2));Assert.AreEqual(2.5f,At(3));
        }
        [Test] public void NewestRegionOverridesAndBlendsUnderlyingRegion()
        {
            var older=Gain(1,7,.4f);store.Upsert(older);store.Upsert(Gain(2,5,2));
            Assert.AreEqual(2,At(3));Assert.That(At(2.05f),Is.EqualTo(1.2f).Within(.001));
            Assert.AreEqual(.4f,At(2));Assert.AreEqual(.4f,At(5));
            older.value=.7f;store.Upsert(older);Assert.AreEqual(.7f,At(3));Assert.AreEqual(2,store.edits.Count);
        }
        [Test] public void BoundaryFadeIsContinuousAtBothEdges()
        {
            store.Upsert(Gain(value:0));
            Assert.AreEqual(1,At(2));Assert.That(At(2.05f),Is.EqualTo(.5f).Within(.001));Assert.AreEqual(0,At(2.1f));
            Assert.That(At(3.95f),Is.EqualTo(.5f).Within(.001));Assert.AreEqual(1,At(4));
            Assert.Less(Mathf.Abs(At(2.00001f)-At(1.99999f)),.001f);
        }
        [Test] public void WrappedAndBeginningAndEndingRegions()
        {
            store.Upsert(Gain(9,1,.3f));
            Assert.AreEqual(.3f,At(9.5f));Assert.AreEqual(.3f,At(0));Assert.AreEqual(.3f,At(10));Assert.AreEqual(.3f,At(.5f));
            Assert.AreEqual(1,At(5));Assert.AreEqual(1,At(9));Assert.AreEqual(1,At(1));
            store.ResetSong("a");store.Upsert(Gain(0,1,0));Assert.AreEqual(1,At(0));Assert.AreEqual(0,At(.5f));
            store.ResetSong("a");store.Upsert(Gain(9,10,0));Assert.AreEqual(0,At(9.5f));Assert.That(At(9.99999f),Is.EqualTo(1).Within(.001));
        }
        [TestCase(float.NaN,1)] [TestCase(float.PositiveInfinity,1)] [TestCase(-5,0)] [TestCase(100,2.5f)]
        public void InvalidGainIsSanitized(float input,float expected)
        {
            var edit=Gain(value:input);store.Upsert(edit);Assert.AreEqual(expected,edit.value);Assert.AreEqual(expected,At(3));
        }
        [Test] public void MatchingRegionUpdateKeepsIdentityAndOtherRegions()
        {
            var edit=Gain();store.Upsert(edit);store.Upsert(Gain(6,7));
            var found=store.FindGainEdit("a",StemId.Bass,false,2,4);Assert.AreSame(edit,found);
            found.value=.8f;store.Upsert(found);Assert.AreEqual(2,store.edits.Count);Assert.AreEqual(.8f,At(3));
            Assert.IsNull(store.FindGainEdit("b",StemId.Bass,false,2,4));
        }
        [Test] public void GestureMappingCanMuteBoostAndRecoverFromZero()
        {
            var design=ScriptableObject.CreateInstance<RemixDesign>();
            Assert.AreEqual(1,design.MapGain(1,.1f,.1f));Assert.AreEqual(0,design.MapGain(1,0,.1f));
            Assert.AreEqual(2,design.MapGain(1,.2f,.1f));Assert.AreEqual(1,design.MapGain(0,.2f,.1f));
            Assert.AreEqual(1,design.MapGain(1,float.NaN,.1f));Assert.AreEqual(2.5f,design.MapGain(1,100,.1f));
            Object.DestroyImmediate(design);
        }
        [Test] public void LegacyCurvesAndShortRegionsRemainFiniteAndLoopRelative()
        {
            var whole=Gain(whole:true);whole.curve=AnimationCurve.Linear(0,1,10,2);store.Upsert(whole);
            Assert.AreEqual(At(3),At(13));
            var region=Gain(9,1);region.curve=AnimationCurve.Linear(0,1,2,0);store.Upsert(region);
            Assert.That(At(0),Is.EqualTo(.5f).Within(.001));
            store.ResetSong("a");store.Upsert(Gain(2,2.01f,0));
            Assert.That(At(2.005f),Is.EqualTo(0).Within(.001));Assert.AreEqual(1,At(2));Assert.AreEqual(1,At(2.01f));
        }
        [Test] public void RegionGainDoesNotOverrideAudioWholeStemGain()
        {
            var store=new AutomationStore();
            store.Upsert(new AutomationEdit{songId="a",stem=StemId.Bass,parameter=ParameterType.Gain,wholeStem=true,value=.4f});
            store.Upsert(new AutomationEdit{songId="a",stem=StemId.Bass,parameter=ParameterType.Gain,start=0,end=5,value=2});
            Assert.AreEqual(.4f,store.EvaluateWholeGain("a",StemId.Bass,2));
            Assert.AreEqual(2,store.Evaluate("a",StemId.Bass,ParameterType.Gain,2));
            Assert.AreEqual(1,store.EvaluateWholeGain("b",StemId.Bass,2));
        }
    }
}
