using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Mode = RemixTable.RibbonInteractionManager.ControlMode;
namespace RemixTable.Tests
{
    // The VOLUME / FILTER buttons pick what a hold edits. Both use the same up/down motion; only the
    // selected control moves, and it can be pushed one way and brought back (or past neutral) in one hold.
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
        // `wristLift` moves the whole hand; `fingertipWobble` moves only the pinch point (wrist stays put).
        RibbonInteractionManager.HandSample Hand(float wristLift=0,float fingertipWobble=0) =>
            new RibbonInteractionManager.HandSample{valid=true,pinch=true,
                position=ribbon.transform.TransformPoint(new Vector3(0,pinchY+wristLift+fingertipWobble,0)),
                wrist=ribbon.transform.TransformPoint(new Vector3(0,pinchY-.03f+wristLift,-.12f)),wristValid=true};
        void Tick(RibbonInteractionManager.HandSample hand) => gestures.ProcessHands(hand,default,.016f);
        float Value(ParameterType type) { var e=manager.automation.edits.Find(x=>x.parameter==type);return e==null?float.NaN:e.value; }
        [Test] public void VolumeIsTheDefaultMode() => Assert.AreEqual(Mode.Volume,gestures.Mode);
        [Test] public void VolumeModeReversesInOneHoldAndNeverTouchesFilter()
        {
            Tick(Hand());
            Tick(Hand(.03f));Tick(Hand(.06f));
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f);
            Tick(Hand(0));Tick(Hand(-.06f));                // back down past the start
            Assert.AreEqual(.5f,Value(ParameterType.Gain),.0001f);
            Tick(Hand(0));
            Assert.AreEqual(1,Value(ParameterType.Gain),.0001f,"returning to the start height restores the volume");
            Assert.IsNaN(Value(ParameterType.Effect),"volume mode must never touch the filter");
            Assert.IsFalse(gestures.IsFiltering);
        }
        [Test] public void FilterModeUpThinDownMuffledReversesThroughNeutralInOneHold()
        {
            gestures.Mode=Mode.Filter;
            Tick(Hand());
            Tick(Hand(.06f));
            Assert.AreEqual(.5f,Value(ParameterType.Effect),.0001f,"up = high-pass (thin)");
            Tick(Hand(0));
            Assert.AreEqual(0,Value(ParameterType.Effect),.0001f,"back to the start height = neutral");
            Tick(Hand(-.06f));
            Assert.AreEqual(-.5f,Value(ParameterType.Effect),.0001f,"down = low-pass (muffled)");
            Tick(Hand(-.2f));
            Assert.AreEqual(-1,Value(ParameterType.Effect),.0001f,"clamped at full sweep");
            Tick(Hand(.03f));
            Assert.AreEqual(.25f,Value(ParameterType.Effect),.0001f,"reversing direction mid-hold keeps working");
            Assert.IsNaN(Value(ParameterType.Gain),"filter mode must never touch the volume");
            Assert.IsFalse(gestures.IsLifting);
        }
        [Test] public void SwitchingModeMidHoldKeepsWhatWasSetAndContinuesFromCurrentHeight()
        {
            Tick(Hand());
            Tick(Hand(.06f));                               // volume up to 150%
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f);
            gestures.Mode=Mode.Filter;                      // switch while still holding, hand still raised
            Tick(Hand(.06f));
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f,"switching does not jump the volume");
            Assert.IsNaN(Value(ParameterType.Effect),"switching alone does not change the filter");
            Tick(Hand(0));                                  // 6 cm down from the switch point
            Assert.AreEqual(-.5f,Value(ParameterType.Effect),.0001f);
            Assert.AreEqual(1.5f,Value(ParameterType.Gain),.0001f);
            gestures.Mode=Mode.Volume;                      // back to volume from the lowered hand
            Tick(Hand(0));Tick(Hand(.03f));
            Assert.AreEqual(1.75f,Value(ParameterType.Gain),.0001f,"volume continues relative to the current hand");
            Assert.AreEqual(-.5f,Value(ParameterType.Effect),.0001f);
        }
        [Test] public void FingertipWobbleAndSmallJitterChangeNothingInEitherMode()
        {
            foreach(var mode in new[]{Mode.Volume,Mode.Filter})
            {
                gestures.Mode=mode;
                Tick(Hand());
                Tick(Hand(0,.02f));Tick(Hand(0,-.02f));     // fingertips wobble 2 cm, wrist still
                Tick(Hand(.004f));Tick(Hand(-.004f));       // wrist jitter under the 8 mm dead zone
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
