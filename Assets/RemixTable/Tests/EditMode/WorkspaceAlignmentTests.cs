using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR;
namespace RemixTable.Tests
{
    public class WorkspaceAlignmentTests
    {
        GameObject root,rig;
        RemixTableManager manager;
        RemixDesign design;
        RibbonView ribbon;
        RibbonInteractionManager gestures;
        static readonly Pose Offset=new Pose(new Vector3(0,1.36144f,0),Quaternion.identity);
        static Pose Head(float yaw=0) => new Pose(new Vector3(2,1.25f,-3),Quaternion.Euler(0,yaw,0));
        [SetUp] public void Setup()
        {
            root=new GameObject("alignment fixture");root.SetActive(false);
            manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            design=ScriptableObject.CreateInstance<RemixDesign>();design.curvedTimeline=true;design.spacing=.09f;manager.design=design;
            manager.library=root.AddComponent<SongLibraryManager>();manager.library.songs=new[]{new SongMetadata{id="alignment",duration=32}};
            manager.clock.Reset(32);manager.clock.elapsed=10;
            var workspace=new GameObject("workspace");workspace.transform.SetParent(root.transform);manager.workspace=workspace.transform;
            var go=new GameObject("ribbon");go.transform.SetParent(workspace.transform);
            ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.visualTime=10;manager.ribbons=new[]{ribbon};
            gestures=root.AddComponent<RibbonInteractionManager>();gestures.enabled=false;gestures.manager=manager;manager.interactions=gestures;
            var audio=root.AddComponent<RemixAudioManager>();audio.enabled=false;audio.manager=manager;manager.audioManager=audio;
            rig=new GameObject("test origin");
            root.SetActive(true);
        }
        [TearDown] public void Cleanup()
        {
            gestures.Cancel();Object.DestroyImmediate(root);Object.DestroyImmediate(rig);Object.DestroyImmediate(design);
        }
        void Tick(Pose head,bool tracked=true,int count=0,Pose? offset=null,TrackingOriginModeFlags mode=TrackingOriginModeFlags.Device,float dt=.4f) =>
            manager.ProcessWorkspacePose(head,tracked,offset??Offset,mode,count,dt);
        void Align(Pose head,int count=0) { Tick(head,count:count);Tick(head,count:count); }
        void AssertAligned(Pose head)
        {
            var forward=Vector3.ProjectOnPlane(head.rotation*Vector3.forward,Vector3.up).normalized;
            Assert.That(Vector3.Distance(manager.workspace.position,head.position+forward*design.Radius-Vector3.up*design.workspaceEyeDrop),Is.LessThan(.00001));
            Assert.That(Vector3.Dot(manager.workspace.right,manager.workspace.position-head.position),Is.EqualTo(0).Within(.00001));
            Assert.That(manager.workspace.position.y,Is.EqualTo(head.position.y-design.workspaceEyeDrop).Within(.00001));
            Assert.That(Vector3.Distance(manager.workspace.up,Vector3.up),Is.LessThan(.00001));
            var center=manager.workspace.TransformPoint(new Vector3(0,0,-design.Radius));
            Assert.That(Vector3.Distance(center,head.position-Vector3.up*design.workspaceEyeDrop),Is.LessThan(.00001));
        }
        [TestCase(0)] [TestCase(70)] [TestCase(-130)] public void InitialPlacementUsesHeadYawAndHeight(float yaw)
        {
            var head=Head(yaw);Align(head);Assert.IsTrue(manager.anchored);AssertAligned(head);
        }
        [Test] public void PitchRollAndTranslatedRotatedOriginDoNotAddOffsets()
        {
            rig.transform.SetPositionAndRotation(new Vector3(-4,.5f,8),Quaternion.Euler(0,110,0));
            var local=new Pose(new Vector3(.2f,1.1f,-.3f),Quaternion.Euler(32,-40,28));
            var head=new Pose(rig.transform.TransformPoint(local.position),rig.transform.rotation*local.rotation);
            root.transform.SetPositionAndRotation(new Vector3(4,-.5f,3),Quaternion.Euler(0,-37,0));
            Align(head);AssertAligned(head);
        }
        [Test] public void InitializationWaitsForTrackedStablePose()
        {
            Tick(Head(),false);Tick(Head(),false);Assert.IsFalse(manager.anchored);
            Tick(Head());Assert.IsFalse(manager.anchored);
            Tick(Head(30));Assert.IsFalse(manager.anchored);
            Tick(Head(30),dt:.1f);Assert.IsFalse(manager.anchored);
            Tick(Head(30),false);Tick(Head(30));Assert.IsFalse(manager.anchored);
            Tick(Head(30));Assert.IsTrue(manager.anchored);AssertAligned(Head(30));
        }
        [Test] public void OrdinaryHeadMotionEvenLargeJumpsNeverFollowsOrTriggersRecenter()
        {
            Align(Head());var position=manager.workspace.position;var rotation=manager.workspace.rotation;
            for(int i=0;i<20;i++) Tick(new Pose(new Vector3(i,.8f+i*.1f,-i),Quaternion.Euler(25,i*15,10)));
            Assert.AreEqual(position,manager.workspace.position);Assert.AreEqual(rotation,manager.workspace.rotation);Assert.IsFalse(manager.WorkspaceAligning);
        }
        [Test] public void SequentialNativeRecenterCountsUseLatestSettledHeadPose()
        {
            Align(Head());
            for(int i=1;i<=3;i++)
            {
                var head=Head(i*35);head.position+=new Vector3(i*.2f,-i*.02f,0);
                var previous=manager.workspace.position;
                Tick(head,count:i);Assert.AreEqual(previous,manager.workspace.position);Assert.IsTrue(manager.WorkspaceAligning);
                Tick(head,count:i);AssertAligned(head);
            }
        }
        [Test] public void CameraOffsetAndTrackingModeFallbackWaitsWithoutDoubleHeight()
        {
            Align(Head());var head=Head(45);head.position.y=.95f;
            Tick(head,offset:Pose.identity,mode:TrackingOriginModeFlags.Floor);
            Assert.IsTrue(manager.WorkspaceAligning);
            Tick(head,offset:Pose.identity,mode:TrackingOriginModeFlags.Floor);AssertAligned(head);
            Tick(head,offset:new Pose(Vector3.up*.1f,Quaternion.identity),mode:TrackingOriginModeFlags.Floor);
            Assert.IsTrue(manager.WorkspaceAligning);
            Tick(head,offset:new Pose(Vector3.up*.1f,Quaternion.identity),mode:TrackingOriginModeFlags.Floor);AssertAligned(head);
        }
        [Test] public void NotificationDoesNotMoveUntilSettledAndChildrenKeepTheirLocalPose()
        {
            Align(Head());
            var child=new GameObject("mode controls");child.transform.SetParent(manager.workspace,false);child.transform.localPosition=new Vector3(.1f,-.4f,0);
            var local=child.transform.localPosition;
            manager.RequestWorkspaceAlignment();var previous=manager.workspace.position;
            Assert.AreEqual(previous,manager.workspace.position);
            Align(Head(90));AssertAligned(Head(90));Assert.AreEqual(local,child.transform.localPosition);
            Assert.That(Vector3.Distance(child.transform.position,manager.workspace.TransformPoint(local)),Is.LessThan(.00001));
        }
        [TestCase(false)] [TestCase(true)] public void ActiveEditCancelsWithoutChangingAutomationOrRegionTimes(bool live)
        {
            Align(Head());manager.audioManager.LiveMode=live;
            var hand=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=ribbon.SurfacePoint(design.width*.45f)};
            gestures.ProcessHands(hand,default,.016f);Assert.IsTrue(ribbon.selected);
            hand.position+=ribbon.transform.up*.03f;gestures.ProcessHands(hand,default,.016f);
            var edit=manager.automation.edits[0];float start=edit.start,end=edit.end,value=edit.value;
            Assert.AreEqual(1,manager.audioManager.EditingPauseCount);
            manager.RequestWorkspaceAlignment();Assert.IsFalse(ribbon.selected);Assert.AreEqual(0,manager.audioManager.EditingPauseCount);
            Assert.IsNull(gestures.ActiveRibbon);
            Align(Head(-90));
            Assert.AreEqual(start,edit.start);Assert.AreEqual(end,edit.end);Assert.AreEqual(value,edit.value);
            hand.position=ribbon.SurfacePoint(design.width*.45f);gestures.ProcessHands(hand,default,.016f);Assert.IsNull(gestures.ActiveRibbon);
            var release=hand;release.pinch=false;gestures.ProcessHands(release,release,.016f);
            gestures.ProcessHands(hand,default,.016f);Assert.AreSame(ribbon,gestures.ActiveRibbon);
            foreach(float x in new[]{-.5f,-.45f,.45f,.5f})
            {
                float coordinate=design.width*x;var point=ribbon.SurfacePoint(coordinate);
                Assert.IsTrue(ribbon.CanSelectAt(point,out _));
                Assert.That(ribbon.LocalX(point),Is.EqualTo(coordinate).Within(.00001));
                Assert.That(ribbon.Timestamp(ribbon.LocalX(point)),Is.EqualTo(ribbon.Timestamp(coordinate)).Within(.0001));
            }
        }
    }
}
