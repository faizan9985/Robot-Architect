using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    public sealed class ContactUiTestModule : XRUIInputModule
    {
        public int BlockedPointer=-1;
        public override bool IsPointerOverGameObject(int pointerId)=>pointerId==BlockedPointer;
        public void InitializeForTest()=>base.OnEnable();
    }
    [TestFixture(false)] [TestFixture(true)]
    public class RibbonContactGlowTests
    {
        readonly bool curved;public RibbonContactGlowTests(bool curve){curved=curve;}
        GameObject root;RemixTableManager manager;RibbonInteractionManager gestures;RibbonView[] ribbons;
        [SetUp] public void Setup()
        {
            root=new GameObject("contact glow fixture");root.SetActive(false);
            manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();manager.design.curvedTimeline=curved;manager.design.interactiveAngle=180;
            manager.library=root.AddComponent<SongLibraryManager>();manager.library.songs=new[]{new SongMetadata{id="contact",duration=32}};
            manager.clock.Reset(32);manager.clock.elapsed=10;manager.clock.playing=false;
            ribbons=new RibbonView[4];
            for(int i=0;i<4;i++)
            {
                var go=new GameObject(((StemId)i).ToString());go.transform.SetParent(root.transform);go.transform.localPosition=new Vector3(0,i*.15f,0);
                ribbons[i]=go.AddComponent<RibbonView>();ribbons[i].manager=manager;ribbons[i].stem=(StemId)i;
            }
            manager.ribbons=ribbons;gestures=root.AddComponent<RibbonInteractionManager>();gestures.manager=manager;gestures.enabled=false;manager.interactions=gestures;
            root.SetActive(true);foreach(var r in ribbons)
            {
                typeof(RibbonView).GetMethod("Awake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(r,null);r.UpdateWaveform();
            }
        }
        [TearDown] public void Cleanup(){gestures.Cancel();Object.DestroyImmediate(manager.design);Object.DestroyImmediate(root);}
        RibbonInteractionManager.HandSample Hand(int stem,float x=0,bool pinch=false,float y=0)=>new RibbonInteractionManager.HandSample{
            valid=true,pinch=pinch,position=ribbons[stem].transform.TransformPoint(manager.design.ArcPoint(x,y))};
        RibbonInteractionManager.HandSample Away=>new RibbonInteractionManager.HandSample{valid=true,position=new Vector3(5,5,5)};
        Vector4 Input(int stem,int hand=0)=>ribbons[stem].ContactGlow.ShaderInput(ribbons[stem],hand);
        void Tick(RibbonInteractionManager.HandSample a,RibbonInteractionManager.HandSample b=default,float dt=.1f)=>gestures.ProcessHands(a,b,dt);
        [Test] public void AllStemsHaveZeroIdleAndUseExactApprovedSphereContributions()
        {
            Assert.AreEqual(.5f,RibbonContactGlow.CoreHover);Assert.AreEqual(6.5f,RibbonContactGlow.CoreGrab);
            Assert.AreEqual(1.25f,RibbonContactGlow.RimHover);Assert.AreEqual(8,RibbonContactGlow.RimGrab);
            Assert.AreEqual(1.25f,RibbonContactGlow.HaloHover);Assert.AreEqual(10,RibbonContactGlow.HaloGrab);
            foreach(var r in ribbons)
            {
                Assert.AreEqual(Vector4.zero,r.ContactGlow.ShaderInput(r,0));Assert.AreEqual(Vector4.zero,r.ContactGlow.ShaderInput(r,1));
                foreach(var renderer in r.GetComponentsInChildren<Renderer>())
                {
                    var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                    Assert.AreEqual(Vector4.zero,block.GetVector("_Contact0"));Assert.IsFalse(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader));
                    Assert.AreEqual(new Vector4(.5f,6.5f,1.25f,8),block.isEmpty?renderer.sharedMaterial.GetVector("_ContactCoefficients"):block.GetVector("_ContactCoefficients"));
                    Assert.AreEqual(new Vector4(1.25f,10,0,0),block.isEmpty?renderer.sharedMaterial.GetVector("_ContactHaloCoefficients"):block.GetVector("_ContactHaloCoefficients"));
                }
            }
        }
        [Test] public void HoverArbitratesOneStemPerHandAndNeverWritesAutomationOrGeometry()
        {
            for(int i=0;i<4;i++)
            {
                gestures.Cancel();var points=ribbons[i].WaveformMesh.vertices;var indices=ribbons[i].WaveformMesh.triangles;
                Tick(Hand(i,.05f));Assert.AreEqual(1,Input(i).z);Assert.AreEqual(0,Input(i).w);
                for(int j=0;j<4;j++)if(j!=i)Assert.AreEqual(Vector4.zero,Input(j));
                ribbons[i].UpdateWaveform();CollectionAssert.AreEqual(points,ribbons[i].WaveformMesh.vertices);CollectionAssert.AreEqual(indices,ribbons[i].WaveformMesh.triangles);
                Assert.IsNull(gestures.ActiveRibbon);Assert.IsEmpty(manager.automation.edits);Assert.AreEqual(0,root.GetComponentsInChildren<Collider>().Length);
            }
        }
        [Test] public void BothHandsRetainSeparateHoverSlotsAndSingleOwnerRules()
        {
            Tick(Hand(0,-.10f),Hand(3,.10f));Assert.AreEqual(1,Input(0).z);Assert.AreEqual(1,Input(3,1).z);
            Assert.AreEqual(Vector4.zero,Input(0,1));Assert.AreEqual(Vector4.zero,Input(3));
            Tick(Hand(0,-.10f,true),Hand(3,.10f));Assert.AreSame(ribbons[0],gestures.ActiveRibbon);Assert.AreEqual(1,Input(0).w);
            Tick(Hand(0,-.15f,true),Hand(3,.15f,true));Assert.AreEqual(RibbonInteractionManager.GestureState.TwoHandRegion,gestures.State);
            Assert.AreEqual(1,Input(0,1).w);Assert.AreEqual(0,Input(3,1).w);
            Assert.AreNotEqual(Input(0).x,Input(0,1).x);Assert.IsFalse(ribbons[3].selected);
        }
        [Test] public void PinchFollowsOwnerSuppressesOwnHoverAndReleaseRegrabIsContinuous()
        {
            Tick(Hand(2,0));Tick(Hand(2,0,true));var selected=Input(2);Assert.AreEqual(1,selected.w);
            Tick(Hand(2,.12f,true));Assert.AreNotEqual(selected.x,Input(2).x);
            float expected=curved?.12f/manager.design.width*2*manager.design.HalfAngle*manager.design.Radius:.12f;
            Assert.That(Input(2).x,Is.EqualTo(expected).Within(.0001));
            Tick(Away,default,0);Assert.AreEqual(1,Input(2).w);
            Tick(Away,default,.125f);Assert.That(Input(2).w,Is.EqualTo(.5f).Within(.0001));
            Tick(Hand(2,.12f,true),default,0);Assert.That(Input(2).w,Is.EqualTo(.5f).Within(.0001));
            Tick(Hand(2,.12f,true),default,.01f);Assert.Greater(Input(2).w,.5f);
            Tick(Away,default,.25f);Assert.AreEqual(Vector4.zero,Input(2));Assert.IsFalse(ribbons[2].selected);
        }
        [TestCase(.25f,4)] [TestCase(.25f,32)] [TestCase(2f,4)] [TestCase(2f,32)]
        public void WorldRadiusAndContactStayCorrectAcrossZoomSpeedAndSongWrap(float speed,float window)
        {
            manager.zoom.Request(window);manager.zoom.Tick(100,false,.01f);manager.design.visibleSeconds=window;
            manager.clock.elapsed=31.99;Tick(Hand(1,.06f,true));float before=Input(1).x;
            Assert.That(Input(1).y,Is.EqualTo(.09f).Within(.0001));
            manager.clock.elapsed+=speed*.05f;Tick(Hand(1,.06f,true));Assert.That(Input(1).x,Is.EqualTo(before).Within(.0001));
            Tick(Away,default,0);manager.clock.elapsed+=speed*.05f;
            float distance=speed*.05f/window*(curved?2*manager.design.HalfAngle*manager.design.Radius:manager.design.width);
            Assert.That(Input(1).x,Is.EqualTo(before-distance).Within(.0001));
            // DSP can wrap elapsed rather than keeping it monotonic. Keep the same visible occurrence.
            var wrapped=Input(1);manager.clock.elapsed-=32;Assert.That(Input(1).x,Is.EqualTo(wrapped.x).Within(.0001));
        }
        [Test] public void CancellationTrackingMenusExternalHoldAndRecenterClearHighlights()
        {
            Tick(Hand(3,0,true));gestures.Cancel();Assert.AreEqual(Vector4.zero,Input(3));
            Tick(Hand(3,0,true));Tick(default);Assert.AreEqual(Vector4.zero,Input(3));
            Tick(Hand(3));var menu=new GameObject("palm menu",typeof(Canvas));menu.transform.SetParent(root.transform);manager.palmMenuCanvas=menu.GetComponent<Canvas>();
            Tick(Hand(3));Assert.AreEqual(Vector4.zero,Input(3));menu.SetActive(false);
            Tick(Hand(3));manager.BeginExternalHold(root);Assert.AreEqual(Vector4.zero,Input(3));Tick(Hand(3));Assert.AreEqual(Vector4.zero,Input(3));manager.EndExternalHold(root);
            Tick(Hand(3,0,true));manager.RequestWorkspaceAlignment();Assert.AreEqual(Vector4.zero,Input(3));Assert.IsNull(gestures.ActiveRibbon);
        }
        [TestCase(RibbonInteractionManager.ControlMode.Volume,ParameterType.Gain)]
        [TestCase(RibbonInteractionManager.ControlMode.Filter,ParameterType.Effect)]
        [TestCase(RibbonInteractionManager.ControlMode.Pitch,ParameterType.Pitch)]
        public void SculptAndReleasePreservePersistentAutomationAndUndoRedo(RibbonInteractionManager.ControlMode mode,ParameterType parameter)
        {
            gestures.Mode=mode;Tick(Hand(3,0,true));Tick(Hand(3,0,true,.06f));
            Assert.AreEqual(1,Input(3).w);Assert.AreEqual(1,manager.automation.edits.Count);Assert.AreEqual(parameter,manager.automation.edits[0].parameter);
            float value=manager.automation.edits[0].value;Tick(Away,default,.25f);Assert.AreEqual(Vector4.zero,Input(3));Assert.AreEqual(value,manager.automation.edits[0].value);
            Assert.IsTrue(manager.history.Undo(manager.automation));Assert.IsEmpty(manager.automation.edits);
            Assert.IsTrue(manager.history.Redo(manager.automation));Assert.AreEqual(value,manager.automation.edits[0].value);
        }
        [Test] public void WarmedVisualHoverUpdatesAllocateNoManagedMemory()
        {
            var a=Hand(0);var b=Hand(3,.1f);Tick(a,b);Tick(a,b);
            long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<30;i++)Tick(a,b);long bytes=GC.GetAllocatedBytesForCurrentThread()-start;
            Assert.AreEqual(0,bytes);
        }
        [Test] public void AuthoritativeXrUiPointerBlocksOnlyItsHand()
        {
            var previous=EventSystem.current;
            var events=new GameObject("contact UI",typeof(EventSystem),typeof(ContactUiTestModule));events.transform.SetParent(root.transform);
            var eventSystem=events.GetComponent<EventSystem>();var module=events.GetComponent<ContactUiTestModule>();
            typeof(EventSystem).GetMethod("OnEnable",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(eventSystem,null);EventSystem.current=eventSystem;
            module.InitializeForTest();
            root.AddComponent<XRInteractionManager>();
            var rayObject=new GameObject("left UI interactor");rayObject.transform.SetParent(root.transform);
            var ray=rayObject.AddComponent<XRRayInteractor>();ray.handedness=InteractorHandedness.Left;module.RegisterInteractor(ray);
            Assert.IsTrue(module.GetTrackedDeviceModel(ray,out var model));module.BlockedPointer=model.pointerId;
            typeof(EventSystem).GetMethod("ChangeEventModule",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(eventSystem,new object[]{module});
            try
            {
                Assert.AreSame(eventSystem,EventSystem.current,"UI fixture must register its EventSystem");
                Assert.AreSame(module,EventSystem.current.currentInputModule,"UI fixture must activate its input module");
                Assert.IsTrue(ray.isActiveAndEnabled);Assert.IsTrue(ray is IUIInteractor);Assert.IsTrue(module.IsPointerOverGameObject(model.pointerId));
                Assert.IsTrue((bool)typeof(RibbonInteractionManager).GetMethod("VisualUiBlocked",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(gestures,new object[]{0}),"registered left pointer must suppress left ribbon visuals");
                Tick(Hand(0),Hand(3));Assert.AreEqual(Vector4.zero,Input(0));Assert.AreEqual(1,Input(3,1).z);
                module.BlockedPointer=-1;ray.uiHoverExited.Invoke(new UIHoverEventArgs{interactorObject=ray,deviceModel=model});Tick(Hand(0),Hand(3));Assert.AreEqual(1,Input(0).z);
                ray.uiHoverEntered.Invoke(new UIHoverEventArgs{interactorObject=ray,deviceModel=model});Tick(Hand(0),Hand(3));Assert.AreEqual(Vector4.zero,Input(0));
                var a=Hand(0);var b=Hand(3);Tick(a,b);
                long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<30;i++)Tick(a,b);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                Assert.AreEqual(0,bytes,"UI-owned pointers must not cause per-frame SDK model allocations");
            }
            finally
            {
                typeof(EventSystem).GetMethod("OnDisable",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(eventSystem,null);
                if(previous!=null)EventSystem.current=previous;
            }
        }
        [Test] public void RegionLoopJumpsAndWorldScaleKeepTheReleasedSongLocation()
        {
            Tick(Hand(1,0,true));Tick(Away,default,0);manager.clock.elapsed=8;
            float length=curved?2*manager.design.HalfAngle*manager.design.Radius:manager.design.width;
            float window=curved?manager.zoom.VisibleSeconds:manager.design.visibleSeconds;
            Assert.That(Input(1).x,Is.EqualTo(2/window*length).Within(.0001));
            manager.clock.elapsed=10;Assert.That(Input(1).x,Is.EqualTo(0).Within(.0001));
            root.transform.localScale=Vector3.one*2;Assert.That(Input(1).y,Is.EqualTo(.045f).Within(.0001));
        }
    }
}
