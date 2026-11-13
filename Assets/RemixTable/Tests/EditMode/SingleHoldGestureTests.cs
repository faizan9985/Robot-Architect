using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    // One pinch can lift (volume) and twist (filter) freely, in any order, without releasing.
    public class SingleHoldGestureTests
    {
        GameObject root;
        RemixTableManager manager;
        RibbonView ribbon;
        RibbonInteractionManager gestures;
        float pinchY;
        [SetUp] public void Setup()
        {
            root=new GameObject("single hold fixture");root.SetActive(false);
            manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();
            manager.library=root.AddComponent<SongLibraryManager>();manager.library.songs=new[]{new SongMetadata{id="hold",duration=32}};
            manager.clock.Reset(32);manager.clock.elapsed=10;
            var go=new GameObject("ribbon");go.transform.SetParent(root.transform);
            go.transform.SetPositionAndRotation(new Vector3(2,1,-3),Quaternion.Euler(15,40,25));
            ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=StemId.Vocals;ribbon.visualTime=10;
            manager.ribbons=new[]{ribbon};
            gestures=root.AddComponent<RibbonInteractionManager>();gestures.manager=manager;gestures.enabled=false;
            root.SetActive(true);
            pinchY=ribbon.HeightAt(0);
        }
        [TearDown] public void Cleanup() { gestures.Cancel();Object.DestroyImmediate(manager.design);Object.DestroyImmediate(root); }
        // Fingertip offset from the wrist's roll axis: twisting swings the pinch point up/down by `fingertipSwing`
        // while the wrist itself only moves by `wristLift`.
        RibbonInteractionManager.HandSample Hand(float roll,float wristLift=0,float fingertipSwing=0) =>
            new RibbonInteractionManager.HandSample{valid=true,pinch=true,rollValid=true,roll=roll,
                position=ribbon.transform.TransformPoint(new Vector3(0,pinchY+wristLift+fingertipSwing,0)),
                wrist=ribbon.transform.TransformPoint(new Vector3(0,pinchY-.03f+wristLift,-.12f)),wristValid=true};
        void Tick(RibbonInteractionManager.HandSample hand) => gestures.ProcessHands(hand,default,.016f);
        float Value(ParameterType type) { var e=manager.automation.edits.Find(x=>x.parameter==type);return e==null?float.NaN:e.value; }
        [Test] public void TwistBothWaysInOneHoldEvenWhenFingertipsSwingVertically()
        {
            Tick(Hand(0));
            Tick(Hand(-45,0,.025f));                       // anticlockwise: fingertips swing up 2.5 cm
            Assert.AreEqual(-.5f,Value(ParameterType.Effect),.0001f);
            Tick(Hand(0));
            Assert.AreEqual(0,Value(ParameterType.Effect),.0001f,"returning to the start angle restores neutral");
            Tick(Hand(45,0,-.025f));                       // clockwise: fingertips swing down 2.5 cm
            Tick(Hand(90,0,-.04f));
            Assert.AreEqual(1,Value(ParameterType.Effect),.0001f,"reversing direction mid-hold keeps working");
            Assert.IsNaN(Value(ParameterType.Gain),"fingertip swing alone must not touch volume");
            Assert.IsTrue(ribbon.selected);
        }
        [Test] public void LiftAndTwistTogetherInOneHold()
        {
            Tick(Hand(0));
            Tick(Hand(20,.03f,-.01f));
            Tick(Hand(45,.06f,-.02f));                     // lift 6 cm while twisting clockwise
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f);
            Assert.AreEqual(.5f,Value(ParameterType.Effect),.0001f);
            Assert.IsTrue(gestures.IsLifting);Assert.IsTrue(gestures.IsTwisting);
            Tick(Hand(0,.03f));Tick(Hand(-45,0,.02f));     // lower back while twisting anticlockwise
            Assert.AreEqual(1,Value(ParameterType.Gain),.0001f);
            Assert.AreEqual(-.5f,Value(ParameterType.Effect),.0001f);
            Assert.AreEqual(1,manager.automation.edits.FindAll(x=>x.parameter==ParameterType.Gain).Count);
            Assert.AreEqual(1,manager.automation.edits.FindAll(x=>x.parameter==ParameterType.Effect).Count);
        }
        [Test] public void LiftFirstThenTwistStillReachesTheFilter()
        {
            Tick(Hand(0));
            Tick(Hand(0,-.06f));                           // lower volume first
            Assert.AreEqual(.5f,Value(ParameterType.Gain),.0001f);
            Tick(Hand(40,-.06f,-.02f));Tick(Hand(80,-.06f,-.035f));
            Assert.AreEqual(80f/90f,Value(ParameterType.Effect),.0001f);
            Assert.AreEqual(.5f,Value(ParameterType.Gain),.0001f,"twisting does not disturb the volume already set");
        }
        [Test] public void SmallWristJitterChangesNothing()
        {
            Tick(Hand(0));
            Tick(Hand(6,.004f,.01f));Tick(Hand(-6,-.004f,-.01f));
            Assert.IsEmpty(manager.automation.edits);
            Assert.IsFalse(gestures.IsLifting);Assert.IsFalse(gestures.IsTwisting);
        }
    }
}
