using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.Events;
namespace RemixTable.Tests
{
    public class TimelineZoomTests
    {
        RemixDesign design;
        [SetUp] public void Setup() { design=ScriptableObject.CreateInstance<RemixDesign>();design.curvedTimeline=true; }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(design);
        [Test] public void DefaultLimitsSmoothingAndDeferredRequests()
        {
            var zoom=new TimelineZoom();Assert.AreEqual(8,zoom.VisibleSeconds);Assert.AreEqual(8,zoom.TargetSeconds);
            zoom.Request(-100);Assert.AreEqual(4,zoom.TargetSeconds);zoom.Tick(.016f,false,.16f);
            Assert.That(zoom.VisibleSeconds,Is.InRange(4.01f,7.99f));float frozen=zoom.VisibleSeconds;
            zoom.Request(100);zoom.Tick(10,true,.16f);Assert.AreEqual(frozen,zoom.VisibleSeconds);Assert.AreEqual(32,zoom.TargetSeconds);
            zoom.Tick(10,false,.16f);Assert.AreEqual(32,zoom.VisibleSeconds);Assert.IsTrue(zoom.AtMaximum);
            zoom.Request(4);zoom.Tick(10,false,.16f);Assert.IsTrue(zoom.AtMinimum);
            zoom.Request(float.NaN);Assert.AreEqual(4,zoom.TargetSeconds);
        }
        [TestCase(4)] [TestCase(8)] [TestCase(16)] [TestCase(32)]
        public void MappingRoundTripsAtEachPresetWithLoopWrap(float seconds)
        {
            var zoom=new TimelineZoom();zoom.Request(seconds);zoom.Tick(10,false,.16f);
            foreach(double playhead in new[]{20d,229d,460d}) for(int i=-10;i<=10;i++)
            {
                float angle=i/10f*design.HalfAngle;
                float stamp=zoom.AngleToTimestamp(angle,playhead,230,design);
                Assert.That(stamp,Is.EqualTo(Mathf.Repeat((float)(playhead%230)+i/10f*seconds*.5f,230)).Within(.0001));
                Assert.That(zoom.TimestampToAngle(stamp,playhead,230,design),Is.EqualTo(angle).Within(.0001));
            }
            Assert.AreEqual(20,zoom.AngleToTimestamp(0,20,230,design));
            Assert.Greater(zoom.AngleToTimestamp(.2f,20,230,design),20);
            Assert.Less(zoom.AngleToTimestamp(-.2f,20,230,design),20);
            Assert.AreEqual(Vector3.zero,design.ArcPoint(0));
        }
        [Test] public void ZoomAndMappingHotPathDoesNotAllocate()
        {
            var zoom=new TimelineZoom();zoom.Request(16);zoom.Tick(.01f,false,.16f);zoom.Timestamp(.1f,10,230,design);
            long before=System.GC.GetAllocatedBytesForCurrentThread();float sum=0;
            for(int i=0;i<10000;i++) { zoom.Tick(.01f,false,.16f);sum+=zoom.Timestamp(.1f,10,230,design); }
            long allocated=System.GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Greater(sum,0);Assert.AreEqual(0,allocated);
        }
        [Test] public void SavedPalmBindingsAndSharedRibbonsPreservePhysicalArcAndAutomation()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var ui=Object.FindFirstObjectByType<TimelineZoomUI>();
            Assert.AreSame(manager,ui.manager);Assert.NotNull(manager.palmMenuCanvas);
            var buttons=new[]{ui.detail,ui.normal,ui.wide,ui.overview};float[] presets={4,8,16,32};
            var position=manager.workspace.position;var rotation=manager.workspace.rotation;
            var point=manager.design.ArcPoint(.1f);
            var edit=new AutomationEdit{songId=manager.library.Current.id,stem=StemId.Bass,start=22,end=24,value=.4f,parameter=ParameterType.Gain};
            manager.automation.Upsert(edit);
            for(int i=0;i<buttons.Length;i++)
            {
                Assert.AreEqual(1,buttons[i].onClick.GetPersistentEventCount());Assert.AreSame(ui,buttons[i].onClick.GetPersistentTarget(0));
                buttons[i].onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);
                buttons[i].onClick.Invoke();Assert.AreEqual(presets[i],manager.zoom.TargetSeconds);
                manager.zoom.Tick(10,manager.IsEditing,.16f);
                foreach(var ribbon in manager.ribbons)
                {
                    ribbon.visualTime=20;
                    Assert.That(ribbon.Timestamp(manager.design.width*.25f),Is.EqualTo(20+presets[i]*.25f).Within(.0001));
                    Assert.AreEqual(20,ribbon.Timestamp(0));
                }
            }
            Assert.AreEqual(position,manager.workspace.position);Assert.AreEqual(rotation,manager.workspace.rotation);Assert.AreEqual(point,manager.design.ArcPoint(.1f));
            Assert.AreEqual(22,edit.start);Assert.AreEqual(24,edit.end);Assert.AreEqual(.4f,edit.value);
            ui.expand.onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);
            Assert.IsFalse(ui.options.activeSelf);ui.expand.onClick.Invoke();Assert.IsTrue(ui.options.activeSelf);Assert.IsFalse(ui.instructions.activeSelf);
            ui.expand.onClick.Invoke();Assert.IsFalse(ui.options.activeSelf);Assert.IsTrue(ui.instructions.activeSelf);
            manager.ribbons[0].selected=true;float frozen=manager.zoom.VisibleSeconds;
            ui.detail.onClick.Invoke();manager.zoom.Tick(10,manager.IsEditing,.16f);Assert.AreEqual(frozen,manager.zoom.VisibleSeconds);
            manager.ribbons[0].selected=false;manager.zoom.Tick(10,manager.IsEditing,.16f);Assert.AreEqual(4,manager.zoom.VisibleSeconds);
        }
    }
    public class TimelineZoomGestureTests
    {
        GameObject root;
        RemixTableManager manager;
        RibbonInteractionManager gestures;
        RibbonView ribbon;
        RibbonInteractionManager.HandSample Hand(float x,float y=.3f,float z=-.25f,bool pinch=true) =>
            new RibbonInteractionManager.HandSample{valid=true,pinch=pinch,position=root.transform.TransformPoint(new Vector3(x,y,z))};
        void Tick(RibbonInteractionManager.HandSample left,RibbonInteractionManager.HandSample right) => gestures.ProcessHands(left,right,.016f);
        [SetUp] public void Setup()
        {
            root=new GameObject("zoom gesture fixture");root.SetActive(false);
            manager=root.AddComponent<RemixTableManager>();manager.enabled=false;manager.workspace=root.transform;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();manager.design.curvedTimeline=true;
            manager.library=root.AddComponent<SongLibraryManager>();manager.library.songs=new[]{new SongMetadata{id="zoom",duration=230}};
            manager.clock.Reset(230);manager.clock.elapsed=20;
            var go=new GameObject("ribbon");go.transform.SetParent(root.transform);
            ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.visualTime=20;
            manager.ribbons=new[]{ribbon};gestures=root.AddComponent<RibbonInteractionManager>();gestures.manager=manager;gestures.enabled=false;
            root.SetActive(true);
        }
        [TearDown] public void Cleanup() { gestures.Cancel();Object.DestroyImmediate(manager.design);Object.DestroyImmediate(root); }
        [Test] public void EmptySpreadAndContractionUseThresholdAndNeverPause()
        {
            Tick(Hand(-.15f),Hand(.15f));Assert.AreEqual(RibbonInteractionManager.GestureState.ZoomPending,gestures.State);
            Tick(Hand(-.155f),Hand(.155f));Assert.AreEqual(8,manager.zoom.TargetSeconds);
            Tick(Hand(-.3f),Hand(.3f));Assert.AreEqual(4,manager.zoom.TargetSeconds);Assert.AreEqual(RibbonInteractionManager.GestureState.Zooming,gestures.State);
            Tick(Hand(-.075f),Hand(.075f));Assert.AreEqual(16,manager.zoom.TargetSeconds);
            Assert.IsTrue(manager.clock.playing);Assert.IsFalse(ribbon.selected);Assert.IsEmpty(manager.automation.edits);
        }
        [Test] public void ZoomLocksOutRibbonsAndTrackingLossRequiresBothTrackedReleases()
        {
            Tick(Hand(-.15f),Hand(.15f));Tick(Hand(-.3f),Hand(.3f));
            var onRibbon=Hand(0);onRibbon.position=ribbon.SurfacePoint(0);
            Tick(onRibbon,onRibbon);Assert.IsFalse(ribbon.selected);Assert.IsNull(gestures.ActiveRibbon);
            Tick(default,Hand(.3f));Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);Assert.IsTrue(gestures.ZoomReleaseRequired);
            Tick(onRibbon,onRibbon);Assert.IsFalse(ribbon.selected);
            Tick(Hand(-.15f,pinch:false),Hand(.15f));Assert.IsTrue(gestures.ZoomReleaseRequired);
            Tick(Hand(-.15f,pinch:false),Hand(.15f,pinch:false));Assert.IsFalse(gestures.ZoomReleaseRequired);
            Tick(onRibbon,default);Assert.IsTrue(ribbon.selected);
        }
        [Test] public void RibbonRegionWinsAndExternalOwnerPreventsZoom()
        {
            var a=Hand(-.1f);a.position=ribbon.SurfacePoint(-.1f);
            var b=Hand(.1f);b.position=ribbon.SurfacePoint(.1f);
            Tick(a,b);Assert.AreEqual(RibbonInteractionManager.GestureState.TwoHandRegion,gestures.State);
            float start=ribbon.regionStart,end=ribbon.regionEnd;
            manager.zoom.Request(32);manager.zoom.Tick(1,manager.IsEditing,.16f);
            Assert.AreEqual(8,manager.zoom.VisibleSeconds);Assert.AreEqual(start,ribbon.regionStart);Assert.AreEqual(end,ribbon.regionEnd);
            gestures.Cancel();ribbon.selected=true;
            Tick(Hand(-.15f),Hand(.15f));Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);
            ribbon.selected=false;
            Tick(Hand(-.15f),Hand(.15f));gestures.Cancel();Assert.IsTrue(gestures.ZoomReleaseRequired);
        }
        [Test] public void CancelRequiresReleaseAndPalmMenuBlocksAcquisition()
        {
            Tick(Hand(-.15f),Hand(.15f));gestures.Cancel();
            Assert.IsTrue(gestures.ZoomReleaseRequired);Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);
            Tick(Hand(-.15f,pinch:false),Hand(.15f,pinch:false));
            var menu=new GameObject("existing menu",typeof(Canvas));menu.transform.SetParent(root.transform);manager.palmMenuCanvas=menu.GetComponent<Canvas>();
            Tick(Hand(-.15f),Hand(.15f));Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);
        }
    }
}
