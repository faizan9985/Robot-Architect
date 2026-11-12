using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    public class AutomationTests
    {
        AutomationStore store;
        [SetUp] public void SetUp() => store=new AutomationStore();
        AutomationEdit Edit(string song="a", bool whole=false) => new AutomationEdit {songId=song,stem=StemId.Bass,start=2,end=4,parameter=ParameterType.Pitch,value=7,wholeStem=whole};
        [Test] public void RegionBoundariesAndLoopReplay()
        {
            store.Upsert(Edit());var clock=new PlaybackClock();clock.Reset(8);clock.Advance(3);
            Assert.AreEqual(7,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,clock.Time));
            clock.Advance(8);Assert.AreEqual(7,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,clock.Time));
            Assert.AreEqual(0,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,1));
            Assert.AreEqual(7,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,4));
        }
        [Test] public void WholeStemAndSongIsolation()
        {
            store.Upsert(Edit("a",true));store.Upsert(Edit("b"));
            Assert.AreEqual(7,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,22));
            Assert.AreEqual(0,store.Evaluate("b",StemId.Bass,ParameterType.Pitch,22));
            Assert.AreEqual(7,store.Evaluate("b",StemId.Bass,ParameterType.Pitch,3));
            store.ResetSong("b");Assert.AreEqual(7,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,3));
        }
        [Test] public void UpdateCurveAndReset()
        {
            var edit=Edit();store.Upsert(edit);edit.value=-5;store.Upsert(edit);
            Assert.AreEqual(1,store.edits.Count);Assert.AreEqual(-5,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,3));
            edit.curve=AnimationCurve.Linear(0,0,2,12);
            Assert.That(store.Evaluate("a",StemId.Bass,ParameterType.Pitch,3),Is.EqualTo(6).Within(.001f));
            store.ResetStem("a",StemId.Bass);Assert.IsEmpty(store.edits);
            Assert.AreEqual(1,store.Evaluate("a",StemId.Bass,ParameterType.Gain,3));
        }
        [Test] public void LatestOverlappingEditWins()
        {
            store.Upsert(Edit("a",true));var region=Edit();region.value=-3;store.Upsert(region);
            Assert.AreEqual(-3,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,3));
            Assert.AreEqual(7,store.Evaluate("a",StemId.Bass,ParameterType.Pitch,8));
        }
        [Test] public void ClockPauseAndDurationGuard()
        {
            var clock=new PlaybackClock();clock.Reset(5);clock.Advance(12);Assert.AreEqual(2,clock.Time);
            clock.playing=false;clock.Advance(3);Assert.AreEqual(2,clock.Time);
            clock.Reset(0);Assert.Greater(clock.duration,0);
        }
        [Test] public void DataCanRoundTripThroughJson()
        {
            store.Upsert(Edit());var restored=JsonUtility.FromJson<AutomationStore>(JsonUtility.ToJson(store));
            Assert.AreEqual(7,restored.Evaluate("a",StemId.Bass,ParameterType.Pitch,3));
        }
    }
}
