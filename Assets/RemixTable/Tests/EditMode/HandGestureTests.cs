using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    [TestFixture(false)]
    [TestFixture(true)]
    public class HandGestureTests
    {
        readonly bool curved;
        public HandGestureTests(bool curved) { this.curved=curved; }
        GameObject root;
        RemixTableManager manager;
        RibbonView ribbon;
        RibbonInteractionManager gestures;
        [SetUp] public void Setup()
        {
            root=new GameObject("gesture fixture");root.SetActive(false);
            manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();
            manager.design.curvedTimeline=curved;
            manager.design.interactiveAngle=110;
            manager.library=root.AddComponent<SongLibraryManager>();manager.library.songs=new[]{new SongMetadata{id="hands",duration=32}};
            manager.clock.Reset(32);manager.clock.elapsed=10;
            var go=new GameObject("ribbon");go.transform.SetParent(root.transform);
            go.transform.SetPositionAndRotation(new Vector3(2,1,-3),Quaternion.Euler(15,40,25));
            ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=StemId.Bass;ribbon.visualTime=10;
            manager.ribbons=new[]{ribbon};
            gestures=root.AddComponent<RibbonInteractionManager>();gestures.manager=manager;gestures.enabled=false;
            root.SetActive(true);
        }
        [TearDown] public void Cleanup() { gestures.Cancel();Object.DestroyImmediate(manager.design);Object.DestroyImmediate(root); }
        RibbonInteractionManager.HandSample Hand(float x,float y,float z=0,bool pinch=true) =>
            new RibbonInteractionManager.HandSample{valid=true,pinch=pinch,position=ribbon.transform.TransformPoint(manager.design.ArcPoint(x,y)+Vector3.forward*z)};
        void Tick(RibbonInteractionManager.HandSample left,RibbonInteractionManager.HandSample right=default,float delta=.016f) => gestures.ProcessHands(left,right,delta);
        [Test] public void PinchHysteresisAndTrackingCoordinatesAreIndependentForEachHand()
        {
            var tracking=root.transform;tracking.SetPositionAndRotation(new Vector3(1,2,3),Quaternion.Euler(0,60,0));
            var left=RibbonInteractionManager.SamplePinch(Vector3.zero,new Vector3(.02f,0,0),tracking,false);
            var right=RibbonInteractionManager.SamplePinch(Vector3.zero,new Vector3(.03f,0,0),tracking,false);
            Assert.IsTrue(left.valid && left.pinch);Assert.IsTrue(right.valid);Assert.IsFalse(right.pinch);
            Assert.That(Vector3.Distance(left.position,tracking.TransformPoint(new Vector3(.01f,0,0))),Is.LessThan(.00001));
            right=RibbonInteractionManager.SamplePinch(Vector3.zero,new Vector3(.03f,0,0),tracking,true);Assert.IsTrue(right.pinch);
            left=RibbonInteractionManager.SamplePinch(Vector3.zero,new Vector3(.04f,0,0),tracking,true);Assert.IsFalse(left.pinch);Assert.IsTrue(right.pinch);
        }
        [TestCase(false)] [TestCase(true)] public void EitherHandCanSelectAndLocalVerticalDragWritesGain(bool useRight)
        {
            float y=ribbon.HeightAt(0);var first=Hand(0,y);
            if(useRight)Tick(default,first);else Tick(first);
            Assert.AreEqual(RibbonInteractionManager.GestureState.OneHandGain,gestures.State);
            var up=Hand(0,y+.12f);if(useRight)Tick(default,up);else Tick(up);
            Assert.That(manager.automation.EvaluateGain("hands",StemId.Bass,10,32,.06f),Is.EqualTo(2).Within(.0001));
            var down=Hand(0,y-.12f);if(useRight)Tick(default,down);else Tick(down);
            Assert.That(manager.automation.EvaluateGain("hands",StemId.Bass,10,32,.06f),Is.EqualTo(0).Within(.0001));
            if(useRight)Tick(default,first);else Tick(first);
            Assert.That(manager.automation.edits[0].value,Is.EqualTo(1).Within(.0001)); // Dragging back restores baseline.
            Tick(default);Assert.IsFalse(ribbon.selected);Assert.AreEqual(1,manager.automation.edits.Count);
            Assert.AreEqual(ParameterType.Gain,manager.automation.edits[0].parameter);
        }
        [Test] public void BothHandsContinuouslyAdjustWrappedRegionWithoutWritingGainOrPitch()
        {
            manager.clock.elapsed=31.9;ribbon.visualTime=31.9;
            var a=Hand(-.1f,ribbon.HeightAt(-.1f));Tick(a);
            var b=Hand(.1f,.25f,.4f);Tick(a,b); // Different Y/Z must not select a different gesture mode.
            Assert.AreEqual(RibbonInteractionManager.GestureState.TwoHandRegion,gestures.State);Assert.IsTrue(ribbon.selected);
            a=Hand(-.3f,.2f);b=Hand(.25f,-.1f);Tick(a,b);
            Assert.That(ribbon.regionStart,Is.EqualTo(ribbon.Timestamp(-.3f)).Within(.0001));
            Assert.That(ribbon.regionEnd,Is.EqualTo(ribbon.Timestamp(.25f)).Within(.0001));
            Assert.Greater(ribbon.regionStart,ribbon.regionEnd);Assert.IsTrue(ribbon.InRegion(0));
            Assert.IsEmpty(manager.automation.edits);
            // Move the second boundary again; the previous implementation never updated it.
            b=Hand(.35f,.4f);Tick(a,b);Assert.That(ribbon.regionEnd,Is.EqualTo(ribbon.Timestamp(.35f)).Within(.0001));
        }
        [TestCase(false)] [TestCase(true)] public void LeavingTwoHandStateRebasesGainForEitherRemainingHand(bool keepRight)
        {
            var a=Hand(-.1f,ribbon.HeightAt(-.1f));var b=Hand(.1f,.2f);
            Tick(a);Tick(a,b);float start=ribbon.regionStart,end=ribbon.regionEnd;
            if(keepRight)Tick(default,b);else Tick(a);
            Assert.AreEqual(RibbonInteractionManager.GestureState.OneHandGain,gestures.State);Assert.IsEmpty(manager.automation.edits);
            var remaining=keepRight?b:a;remaining.position+=ribbon.transform.TransformVector(new Vector3(0,-.06f,0));
            if(keepRight)Tick(default,remaining);else Tick(remaining);
            Assert.AreEqual(start,ribbon.regionStart);Assert.AreEqual(end,ribbon.regionEnd);
            Assert.That(manager.automation.edits[0].value,Is.EqualTo(.5f).Within(.0001));
            float midpoint=Mathf.Repeat(start+AutomationStore.RegionLength(start,end,32)*.5f,32);
            Tick(default);Assert.That(manager.automation.EvaluateGain("hands",StemId.Bass,midpoint+32,32,.06f),Is.EqualTo(.5f).Within(.0001));
        }
        [Test] public void ResizeAuthoredRegionPreservesGainAndCancellationClearsSession()
        {
            float y=ribbon.HeightAt(0);var a=Hand(0,y);Tick(a);Tick(Hand(0,y-.06f));
            var edit=manager.automation.edits[0];float value=edit.value;
            Tick(Hand(-.2f,y-.1f),Hand(.2f,y+.3f));
            Assert.AreSame(edit,manager.automation.edits[0]);Assert.AreEqual(value,edit.value);
            Assert.AreEqual(ribbon.regionStart,edit.start);Assert.AreEqual(ribbon.regionEnd,edit.end);
            gestures.Cancel();gestures.Cancel();Assert.IsFalse(ribbon.selected);Assert.IsNull(gestures.ActiveRibbon);
            Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);Assert.AreEqual(1,manager.automation.edits.Count);
        }
        [Test] public void EndpointHoldStillEnablesWholeStemGain()
        {
            float x=manager.design.width*(curved ? .28f : .47f),y=ribbon.HeightAt(x);var a=Hand(x,y);
            Tick(a,default,.7f);Assert.IsTrue(ribbon.wholeStem);
            Tick(Hand(x,y-.12f));Assert.IsTrue(manager.automation.edits[0].wholeStem);
            Assert.That(manager.automation.EvaluateGain("hands",StemId.Bass,20,32,.06f),Is.EqualTo(0).Within(.0001));
        }
    }
}
