using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Mode = RemixTable.RibbonInteractionManager.ControlMode;
namespace RemixTable.Tests
{
    // The VOLUME / FILTER buttons pick what a hold edits: only that control moves, and it can be pushed
    // one way and brought back (or past neutral) without releasing the pinch.
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
            manager.interactions=gestures;
            root.SetActive(true);
            pinchY=ribbon.HeightAt(0);
        }
        [TearDown] public void Cleanup() { gestures.Cancel();Object.DestroyImmediate(manager.design);Object.DestroyImmediate(root); }
        // Twisting swings the fingertips (pinch point) by `fingertipSwing`; the wrist only moves by `wristLift`.
        RibbonInteractionManager.HandSample Hand(float roll,float wristLift=0,float fingertipSwing=0) =>
            new RibbonInteractionManager.HandSample{valid=true,pinch=true,rollValid=true,roll=roll,
                position=ribbon.transform.TransformPoint(new Vector3(0,pinchY+wristLift+fingertipSwing,0)),
                wrist=ribbon.transform.TransformPoint(new Vector3(0,pinchY-.03f+wristLift,-.12f)),wristValid=true};
        void Tick(RibbonInteractionManager.HandSample hand) => gestures.ProcessHands(hand,default,.016f);
        float Value(ParameterType type) { var e=manager.automation.edits.Find(x=>x.parameter==type);return e==null?float.NaN:e.value; }
        [Test] public void VolumeIsTheDefaultMode() => Assert.AreEqual(Mode.Volume,gestures.Mode);
        [Test] public void VolumeModeIgnoresTwistAndReversesInOneHold()
        {
            Tick(Hand(0));
            Tick(Hand(30,.03f));Tick(Hand(60,.06f));       // lift while (accidentally) twisting hard
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f);
            Tick(Hand(20,0));Tick(Hand(-40,-.06f));         // back down past the start
            Assert.AreEqual(.5f,Value(ParameterType.Gain),.0001f);
            Tick(Hand(0,0));
            Assert.AreEqual(1,Value(ParameterType.Gain),.0001f,"returning to the start height restores the volume");
            Assert.IsNaN(Value(ParameterType.Effect),"twisting in volume mode must never touch the filter");
            Assert.IsFalse(gestures.IsTwisting);
        }
        [Test] public void FilterModeIgnoresLiftAndReversesThroughNeutralInOneHold()
        {
            gestures.Mode=Mode.Filter;
            Tick(Hand(0));
            Tick(Hand(-45,.05f,.025f));                     // anticlockwise while (accidentally) lifting
            Assert.AreEqual(-.5f,Value(ParameterType.Effect),.0001f);
            Tick(Hand(0,-.04f));
            Assert.AreEqual(0,Value(ParameterType.Effect),.0001f,"back to the start angle = neutral");
            Tick(Hand(45,.02f,-.025f));Tick(Hand(90,-.05f,-.04f));
            Assert.AreEqual(1,Value(ParameterType.Effect),.0001f,"reversing direction mid-hold keeps working");
            Assert.IsNaN(Value(ParameterType.Gain),"lifting in filter mode must never touch the volume");
            Assert.IsFalse(gestures.IsLifting);
        }
        [Test] public void SwitchingModeMidHoldKeepsWhatWasSetAndContinuesFromCurrentPose()
        {
            Tick(Hand(0));
            Tick(Hand(0,.06f));                             // volume up to 150%
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f);
            gestures.Mode=Mode.Filter;                      // switch while still holding, hand still raised
            Tick(Hand(0,.06f));
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f,"switching does not jump the volume");
            Tick(Hand(45,0,-.025f));                        // lower the hand while twisting: filter only
            Assert.AreEqual(.5f,Value(ParameterType.Effect),.0001f);
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f);
            gestures.Mode=Mode.Volume;                      // back to volume from the lowered pose
            Tick(Hand(45,0));Tick(Hand(0,.03f));
            Assert.AreEqual(1.75f,Value(ParameterType.Gain),.0001f,"volume continues relative to the current hand");
            Assert.AreEqual(.5f,Value(ParameterType.Effect),.0001f);
        }
        [Test] public void SmallJitterChangesNothingInEitherMode()
        {
            foreach(var mode in new[]{Mode.Volume,Mode.Filter})
            {
                gestures.Mode=mode;
                Tick(Hand(0));Tick(Hand(6,.004f,.01f));Tick(Hand(-6,-.004f,-.01f));
                Assert.IsEmpty(manager.automation.edits,mode.ToString());
                Tick(default);
            }
        }
        [Test] public void SelectorButtonsSetModeAndHighlightTheActiveOne()
        {
            var ui=new GameObject("mode ui");
            var selector=ui.AddComponent<RemixModeSelector>();selector.manager=manager;
            Button MakeButton(string name){var go=new GameObject(name);go.transform.SetParent(ui.transform);var img=go.AddComponent<Image>();var b=go.AddComponent<Button>();b.targetGraphic=img;return b;}
            selector.volumeButton=MakeButton("volume");selector.filterButton=MakeButton("filter");
            selector.SelectFilter();
            Assert.AreEqual(Mode.Filter,gestures.Mode);
            Assert.AreEqual(selector.selectedColor,selector.filterButton.targetGraphic.color);
            Assert.AreEqual(selector.idleColor,selector.volumeButton.targetGraphic.color);
            selector.SelectVolume();
            Assert.AreEqual(Mode.Volume,gestures.Mode);
            Assert.AreEqual(selector.selectedColor,selector.volumeButton.targetGraphic.color);
            Object.DestroyImmediate(ui);
        }
    }
}
