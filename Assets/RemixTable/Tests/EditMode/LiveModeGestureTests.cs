using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Mode = RemixTable.RibbonInteractionManager.ControlMode;
namespace RemixTable.Tests
{
    // LIVE playback keeps the song running while a ribbon is held, so a one-hand hold's region follows the
    // playhead for as long as the hold lasts. EDIT (default) keeps the region fixed and the song paused.
    [TestFixture(false)] [TestFixture(true)]
    public class LiveModeGestureTests
    {
        readonly bool curved;
        public LiveModeGestureTests(bool curved) { this.curved=curved; }
        GameObject root;
        RemixTableManager manager;
        RemixAudioManager audio;
        RibbonView ribbon;
        RibbonInteractionManager gestures;
        [SetUp] public void Setup()
        {
            root=new GameObject("live fixture");root.SetActive(false);
            manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();
            manager.design.curvedTimeline=curved;manager.workspace=root.transform;
            manager.library=root.AddComponent<SongLibraryManager>();manager.library.songs=new[]{new SongMetadata{id="live",duration=32}};
            audio=root.AddComponent<RemixAudioManager>();audio.manager=manager;audio.enabled=false;manager.audioManager=audio;
            manager.clock.Reset(32);manager.clock.elapsed=10;
            var go=new GameObject("ribbon");go.transform.SetParent(root.transform);
            ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=StemId.Drums;ribbon.visualTime=10;
            manager.ribbons=new[]{ribbon};
            gestures=root.AddComponent<RibbonInteractionManager>();gestures.manager=manager;gestures.enabled=false;
            manager.interactions=gestures;
            root.SetActive(true);
        }
        [TearDown] public void Cleanup() { gestures.Cancel();Object.DestroyImmediate(manager.design);Object.DestroyImmediate(root); }
        RibbonInteractionManager.HandSample Hand(float x,float lift) => new RibbonInteractionManager.HandSample{valid=true,pinch=true,
            position=ribbon.transform.TransformPoint(manager.design.ArcPoint(x,ribbon.HeightAt(x)+lift)),
            wrist=ribbon.transform.TransformPoint(manager.design.ArcPoint(x,ribbon.HeightAt(x)-.03f+lift)+Vector3.back*.12f),wristValid=true};
        // Pinch at x, lift, then let the song play `seconds` while the hand stays lifted.
        void HoldWhilePlaying(float x,float seconds)
        {
            var start=Hand(x,0);gestures.ProcessHands(start,default,.016f);
            var lifted=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=start.position+ribbon.transform.up*.03f,wrist=start.wrist+ribbon.transform.up*.03f,wristValid=true};
            gestures.ProcessHands(lifted,default,.016f);
            for(float t=0;t<seconds;t+=.1f){manager.clock.elapsed+=.1f;gestures.ProcessHands(lifted,default,.016f);}
        }
        AutomationEdit Edit(ParameterType type) => manager.automation.edits.Find(e=>e.parameter==type);
        [Test] public void EditModeIsTheDefault() => Assert.IsFalse(audio.LiveMode);
        [TestCase(false,Mode.Volume,-1)] [TestCase(false,Mode.Volume,1)]
        [TestCase(false,Mode.Filter,-1)] [TestCase(false,Mode.Filter,1)]
        [TestCase(true,Mode.Volume,-1)] [TestCase(true,Mode.Volume,1)]
        [TestCase(true,Mode.Filter,-1)] [TestCase(true,Mode.Filter,1)]
        public void BothModesAcquireSidesResizeAndPreserveTheAuthoredParameter(bool live,Mode mode,int side)
        {
            audio.LiveMode=live;gestures.Mode=mode;
            float x=side*manager.design.width*.45f;
            gestures.ProcessHands(Hand(x,0),default,.016f);
            Assert.AreSame(ribbon,gestures.ActiveRibbon);
            gestures.ProcessHands(Hand(x,.03f),default,.016f);
            var parameter=mode==Mode.Volume?ParameterType.Gain:ParameterType.Effect;
            var edit=Edit(parameter);Assert.NotNull(edit);
            float value=edit.value;
            gestures.ProcessHands(Hand(x,.03f),Hand(side*manager.design.width*.5f,0),.016f);
            Assert.AreEqual(RibbonInteractionManager.GestureState.TwoHandRegion,gestures.State);
            Assert.AreEqual(ribbon.regionStart,edit.start);Assert.AreEqual(ribbon.regionEnd,edit.end);
            Assert.AreEqual(value,edit.value);
            Assert.That(manager.zoom.VisibleSeconds,Is.EqualTo(8));
            Assert.IsNull(Edit(mode==Mode.Volume?ParameterType.Effect:ParameterType.Gain));
            if(edit.curve!=null) Assert.That(edit.Evaluate(Mathf.Repeat(edit.start+AutomationStore.RegionLength(edit.start,edit.end,32)*.5f,32),32),Is.EqualTo(value).Within(.0001));
        }
        [Test] public void EditModeKeepsTheHeldRegionFixed()
        {
            HoldWhilePlaying(0,2);
            Assert.AreEqual(10.3f,ribbon.regionEnd,.01f);
            Assert.AreEqual(10.3f,Edit(ParameterType.Gain).end,.01f);
        }
        [Test] public void LiveVolumeHoldStretchesWithThePlayhead()
        {
            audio.LiveMode=true;
            HoldWhilePlaying(0,2);
            var gain=Edit(ParameterType.Gain);
            Assert.AreEqual(9.7f,gain.start,.01f,"start stays where the pinch began");
            Assert.AreEqual(12f+RibbonInteractionManager.LiveLead,gain.end,.02f,"end stays just ahead of the playhead");
            Assert.AreEqual(1.25f,gain.value,.0001f);
            Assert.AreEqual(1.25f,manager.automation.EvaluateGain("live",StemId.Drums,manager.clock.Time,32,manager.design.gainBoundaryFade),.0001f,"the playing position is edited");
        }
        [Test] public void LiveFilterHoldIsAFlatPlateauThatStretches()
        {
            audio.LiveMode=true;gestures.Mode=Mode.Filter;
            HoldWhilePlaying(0,2);
            var effect=Edit(ParameterType.Effect);
            Assert.IsNull(effect.curve,"a stretching filter edit must not be a peak");
            Assert.AreEqual(12f+RibbonInteractionManager.LiveLead,effect.end,.02f);
            Assert.AreEqual(effect.value,manager.automation.Evaluate("live",StemId.Drums,ParameterType.Effect,manager.clock.Time,32),.0001f);
            Assert.Greater(effect.value,0);
        }
        [Test] public void LiveRegionAheadOfThePlayheadWaitsForIt()
        {
            audio.LiveMode=true;
            float x=manager.design.width*.3f;                  // well right of the playhead (future)
            HoldWhilePlaying(x,1);
            float t=10+x/manager.design.width*manager.design.visibleSeconds;
            Assert.AreEqual(t+.3f,ribbon.regionEnd,.01f,"not reached yet: unchanged");
        }
        [Test] public void LiveRegionWrapsAroundTheLoop()
        {
            audio.LiveMode=true;manager.clock.elapsed=31.5f;ribbon.visualTime=31.5f;
            HoldWhilePlaying(0,1.5f);
            Assert.AreEqual(31.2f,ribbon.regionStart,.01f);
            Assert.AreEqual(1.3f,ribbon.regionEnd,.02f);
            Assert.IsTrue(ribbon.InRegion(manager.clock.Time));
        }
        [Test] public void SelectorButtonsSetPlaybackModeAndHighlightTheActiveOne()
        {
            var ui=new GameObject("playback ui");
            var selector=ui.AddComponent<RemixPlaybackModeSelector>();selector.manager=manager;
            Button MakeButton(string name){var go=new GameObject(name);go.transform.SetParent(ui.transform);var img=go.AddComponent<Image>();var b=go.AddComponent<Button>();b.targetGraphic=img;return b;}
            selector.editButton=MakeButton("edit");selector.liveButton=MakeButton("live");
            selector.SelectLive();
            Assert.IsTrue(audio.LiveMode);
            Assert.AreEqual(selector.selectedColor,selector.liveButton.targetGraphic.color);
            Assert.AreEqual(selector.idleColor,selector.editButton.targetGraphic.color);
            selector.SelectEdit();
            Assert.IsFalse(audio.LiveMode);
            Assert.AreEqual(selector.selectedColor,selector.editButton.targetGraphic.color);
            Object.DestroyImmediate(ui);
        }
    }
}
