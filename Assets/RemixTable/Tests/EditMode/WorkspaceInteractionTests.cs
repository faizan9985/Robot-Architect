using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    public class WorkspaceInteractionTests
    {
        RemixTableManager manager;RemixWorkspaceInteraction control;RibbonInteractionManager gestures;
        static readonly Pose Head=new Pose(new Vector3(0,1.2f,0),Quaternion.identity);
        [SetUp] public void Setup()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");manager=Object.FindFirstObjectByType<RemixTableManager>();control=manager.workspaceInteraction;Assert.NotNull(control);
            manager.enabled=false;gestures=manager.interactions;gestures.enabled=false;control.Initialize();Align(Head);
        }
        void Align(Pose head,int count=0){manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,count,.5f);manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,count,.5f);}
        RibbonInteractionManager.HandSample Hand(float y,bool pinch=true)=>new RibbonInteractionManager.HandSample{valid=true,pinch=pinch,position=manager.workspace.TransformPoint(new Vector3(0,y,-.006f))};
        void Begin()=>gestures.ProcessHands(Hand(-.07f),Hand(.07f),.016f);
        [Test] public void CanceledTimelineZoomCannotBecomeScalingUntilBothHandsRelease()
        {
            var left=Hand(0);left.position=manager.workspace.TransformPoint(new Vector3(-.15f,.3f,-.25f));var right=left;right.position=manager.workspace.TransformPoint(new Vector3(.15f,.3f,-.25f));
            gestures.ProcessHands(left,right,.016f);Assert.AreEqual(RibbonInteractionManager.GestureState.ZoomPending,gestures.State);gestures.Cancel();Assert.IsTrue(gestures.GestureReleaseRequired);
            Begin();Assert.IsFalse(control.OwnsGesture);Assert.IsFalse(manager.ExternalHold);
            gestures.ProcessHands(Hand(-.07f,false),Hand(.07f,false),.016f);Assert.IsFalse(gestures.GestureReleaseRequired);
            Begin();Assert.AreEqual(RemixWorkspaceInteraction.Ownership.VerticalScale,control.State);
        }
        [Test] public void BothHandsAcquirePlayheadBeforeRegionsZoomOrAutomationAndOwnUi()
        {
            Begin();Assert.AreEqual(RemixWorkspaceInteraction.Ownership.VerticalScale,control.State);Assert.IsTrue(manager.ExternalHold);Assert.IsFalse(manager.IsEditing);
            Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);Assert.IsEmpty(manager.automation.edits);
            Assert.IsTrue(control.uiCanvases.All(c=>!c.GetComponent<CanvasGroup>().blocksRaycasts && !c.GetComponent<CanvasGroup>().interactable));
            Assert.IsTrue(manager.ribbons.All(r=>!r.selected && r.ContactGlow.ShaderInput(r,0)==Vector4.zero));
        }
        [Test] public void ScalingChangesHeightSpacingAndPlayheadWithoutHorizontalOrUiScaling()
        {
            var uiScales=control.uiCanvases.Select(c=>c.transform.lossyScale).ToArray();var sphereScales=Object.FindFirstObjectByType<SpatialStemField>().orbs.Select(o=>o.transform.lossyScale).ToArray();
            var local=manager.ribbons.Select(r=>r.transform.localPosition).ToArray();var labels=control.waveformRoot.GetComponentsInChildren<TextMesh>(true);var labelScale=labels.Select(l=>l.transform.lossyScale).ToArray();
            float zoom=manager.zoom.VisibleSeconds;control.SetHeight(control.DefaultHeight*1.5f);
            Assert.That(control.Height,Is.EqualTo(control.DefaultHeight*1.5f).Within(.0001));
            for(int i=0;i<4;i++)
            {
                Assert.AreEqual(local[i],manager.ribbons[i].transform.localPosition);var p=manager.workspace.InverseTransformPoint(manager.ribbons[i].transform.position);
                Assert.That(p.y,Is.EqualTo(local[i].y*1.5f).Within(.0001));Assert.That(p.x,Is.EqualTo(local[i].x).Within(.0001));Assert.That(p.z,Is.EqualTo(local[i].z).Within(.0001));
                Assert.IsTrue(manager.ribbons[i].CanSelectAt(manager.ribbons[i].SurfacePoint(.1f),out _));
                Assert.AreEqual(sphereScales[i],Object.FindFirstObjectByType<SpatialStemField>().orbs[i].transform.lossyScale);
            }
            for(int i=0;i<control.uiCanvases.Length;i++)Assert.AreEqual(uiScales[i],control.uiCanvases[i].transform.lossyScale);
            for(int i=0;i<labels.Length;i++)Assert.That(Vector3.Distance(labelScale[i],labels[i].transform.lossyScale),Is.LessThan(.00001));
            var a=control.playhead.transform.TransformPoint(control.playhead.GetPosition(0));var b=control.playhead.transform.TransformPoint(control.playhead.GetPosition(1));
            Assert.That(Vector3.Distance(a,b),Is.EqualTo(control.Height).Within(.0001));Assert.That(manager.workspace.InverseTransformPoint((a+b)*.5f).y,Is.EqualTo(0).Within(.0001));Assert.AreEqual(zoom,manager.zoom.VisibleSeconds);
            control.SetHeight(control.minimumHeight);
            foreach(var ribbon in manager.ribbons)
            {
                var label=ribbon.GetComponentInChildren<TextMesh>(true).GetComponent<Renderer>().bounds;
                Assert.Greater(label.min.y,ribbon.transform.position.y+manager.design.laneHeight*.5f*control.VerticalScale,"fixed-size labels must clear scaled lane bands at minimum height");
            }
        }
        [Test] public void ScaleLeavesLocalAudioGeometryAndAutomationExactlyUnchanged()
        {
            manager.clock.elapsed=65;manager.clock.duration=manager.library.Current.duration;
            foreach(var r in manager.ribbons)
            {
                typeof(RibbonView).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(r,null);r.UpdateWaveform();var vertices=r.WaveformMesh.vertices;var indices=r.WaveformMesh.triangles;
                control.SetHeight(control.DefaultHeight*1.4f);r.UpdateWaveform();CollectionAssert.AreEqual(vertices,r.WaveformMesh.vertices);CollectionAssert.AreEqual(indices,r.WaveformMesh.triangles);
            }
            Assert.IsEmpty(manager.automation.edits);Assert.AreEqual(65,manager.clock.elapsed);
        }
        [Test] public void SmoothScalingClampsAndEitherReleaseKeepsHeightWithoutFollowOnEdits()
        {
            Begin();float before=control.Height;gestures.ProcessHands(Hand(-.3f),Hand(.3f),.016f);Assert.Greater(control.Height,before);Assert.Less(control.Height,control.maximumHeight);
            gestures.ProcessHands(Hand(-1),Hand(1),2);Assert.That(control.Height,Is.EqualTo(control.maximumHeight).Within(.0001));
            float current=control.Height;gestures.ProcessHands(Hand(-.1f,false),Hand(.1f),.016f);Assert.AreEqual(current,control.Height);Assert.IsTrue(control.ReleaseRequired);Assert.IsFalse(manager.ExternalHold);
            gestures.ProcessHands(Hand(-.1f,false),Hand(.1f),1);Assert.IsEmpty(manager.automation.edits);Assert.IsNull(gestures.ActiveRibbon);
            gestures.ProcessHands(Hand(-.1f,false),Hand(.1f,false),.016f);Assert.IsFalse(control.ReleaseRequired);
            control.SetHeight(-5);Assert.AreEqual(control.minimumHeight,control.Height,.0001);control.SetHeight(10);Assert.AreEqual(control.maximumHeight,control.Height,.0001);
        }
        [Test] public void TrackingLossCancellationAndExplicitRecenterClearOwnershipAndResetOnlyExplicitScale()
        {
            Begin();control.SetHeight(.5f);gestures.ProcessHands(default,Hand(.1f),.016f);Assert.IsFalse(control.OwnsGesture);Assert.IsFalse(manager.ExternalHold);Assert.IsTrue(control.ReleaseRequired);
            manager.MoveWorkspace(manager.workspace.position+new Vector3(.2f,.1f,-.15f));var pose=new Pose(new Vector3(.3f,1.3f,0),Quaternion.Euler(0,45,0));manager.RequestWorkspaceAlignment();Align(pose,1);
            var seated=manager.design.SeatedPose(pose.position,pose.rotation*Vector3.forward);
            Assert.That(Vector3.Distance(manager.workspace.position,seated.position+seated.rotation*new Vector3(.2f,.1f,-.15f)),Is.LessThan(.0001));Assert.AreEqual(.5f,control.Height,.0001);
            manager.RecenterWorkspace();Align(pose,1);Assert.That(Vector3.Distance(manager.workspace.position,seated.position),Is.LessThan(.0001));Assert.That(Quaternion.Angle(manager.workspace.rotation,seated.rotation),Is.LessThan(.0001));Assert.AreEqual(1,control.VerticalScale,.0001);
        }
        [Test] public void NavigationHasNoGrabBorderAndAnimationPreservesRecenterTarget()
        {
            var nav=manager.navigationToolbar;nav.ResetClosed();float width=nav.background.rect.width;
            var rect=(RectTransform)control.recenterControl.transform;Assert.AreEqual(new Vector2(80,50),rect.sizeDelta);
            Assert.IsNull(nav.background.Find("Grab Border"));Assert.IsEmpty(nav.GetComponentsInChildren<Collider>(true));Assert.IsEmpty(nav.GetComponentsInChildren<Rigidbody>(true));
            nav.speed.SetOpen(true);nav.AdvanceAnimation(1);Assert.Greater(nav.background.rect.width,width);Assert.AreEqual(new Vector2(80,50),rect.sizeDelta);
            Assert.IsTrue(control.recenterControl.button.targetGraphic.raycastTarget);Assert.AreEqual(1,control.recenterControl.button.onClick.GetPersistentEventCount());
            Assert.AreSame(control.recenterControl,control.recenterControl.button.onClick.GetPersistentTarget(0));Assert.AreEqual("Click",control.recenterControl.button.onClick.GetPersistentMethodName(0));
        }
        [Test] public void TapThresholdAndStickyDragPreserveScaleAndNeverRecenterAfterReturningToStart()
        {
            var start=manager.workspace.position;control.SetHeight(.5f);Assert.IsTrue(control.BeginMove(Vector3.zero));
            control.UpdateMove(Vector3.right*(control.movementThreshold*.8f),1);Assert.IsFalse(control.HasDragged);Assert.AreEqual(start,manager.workspace.position);
            control.FinishMove(.15f,true);Assert.AreEqual(1,control.VerticalScale);Assert.IsTrue(manager.WorkspaceAligning);Align(Head);
            control.SetHeight(.5f);Assert.IsTrue(control.BeginMove(Vector3.zero));control.UpdateMove(new Vector3(.08f,.03f,-.04f),1);Assert.IsTrue(control.HasDragged);Assert.AreEqual(.5f,control.Height,.0001);
            control.UpdateMove(Vector3.zero,1);control.FinishMove(.2f,true);Assert.IsFalse(manager.WorkspaceAligning);Assert.AreEqual(.5f,control.Height,.0001);Assert.IsFalse(manager.ExternalHold);
        }
        [Test] public void LongPressCancellationAndCompetingScaleNeverBecomeTaps()
        {
            control.SetHeight(.5f);Assert.IsTrue(control.BeginMove(Vector3.zero));control.FinishMove(control.maximumTapDuration+.1f,true);Assert.IsFalse(manager.WorkspaceAligning);Assert.AreEqual(.5f,control.Height,.0001);
            Assert.IsTrue(control.BeginMove(Vector3.zero));control.FinishMove(.1f,false);Assert.IsFalse(manager.WorkspaceAligning);Assert.AreEqual(.5f,control.Height,.0001);
            gestures.ProcessHands(Hand(-.07f,false),Hand(.07f,false),.016f);Begin();Assert.AreEqual(RemixWorkspaceInteraction.Ownership.VerticalScale,control.State);Assert.IsFalse(control.BeginMove(Vector3.zero));
        }
        [Test] public void ManualMovementIncludesSpheresAndEveryCanvasWithoutChangingOrientationOrMixCoordinates()
        {
            var field=Object.FindFirstObjectByType<SpatialStemField>();var original=manager.workspace.position;var rotation=manager.workspace.rotation;var before=control.uiCanvases.Select(c=>c.transform.position).ToArray();var mix=manager.mix.positions.ToArray();var worlds=mix.Select(manager.SpatialWorld).ToArray();
            var delta=new Vector3(.13f,-.09f,.22f);manager.MoveWorkspace(original+delta);Assert.AreEqual(rotation,manager.workspace.rotation);
            for(int i=0;i<before.Length;i++)Assert.That(Vector3.Distance(before[i]+delta,control.uiCanvases[i].transform.position),Is.LessThan(.0001));
            for(int i=0;i<4;i++){Assert.AreEqual(mix[i],manager.mix.positions[i]);Assert.That(Vector3.Distance(worlds[i]+delta,manager.SpatialWorld(mix[i])),Is.LessThan(.0001));}
            Assert.IsTrue(field.transform.IsChildOf(manager.workspace));
        }
        [Test] public void SceneRemovalKeepsMasterVolumePassthroughAndRecenterWithNoPalmActivation()
        {
            Assert.IsFalse(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(c=>c.GetType().Name=="HandMenuController"));
            Assert.IsFalse(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(c=>c.name.Contains("Palm") || c.name.Contains("Hand Menu")));
            Assert.NotNull(control.masterVolumeSlider);Assert.IsFalse(control.PanelVisible);control.ToggleMasterVolume();Assert.IsTrue(control.PanelVisible);control.ToggleMasterVolume();Assert.IsFalse(control.PanelVisible);
            Assert.NotNull(manager.workspace.Find("ToolsCanvas/RecenterTool"));Assert.NotNull(manager.workspace.GetComponentInChildren<RemixToolbox>(true).passthroughButton);
            var room=manager.workspace.GetComponentInChildren<RemixToolbox>(true).passthrough;Assert.NotNull(room);
            var passthrough=new SerializedObject(room).FindProperty("passthrough").objectReferenceValue as MonoBehaviour;Assert.NotNull(passthrough);Assert.AreEqual("PassthroughController",passthrough.GetType().Name);Assert.IsTrue(passthrough.isActiveAndEnabled);
            var data=new SerializedObject(passthrough);Assert.AreSame(manager.workspaceCamera,data.FindProperty("xrCamera").objectReferenceValue);Assert.NotNull(data.FindProperty("cameraManager").objectReferenceValue);
            Assert.AreEqual(RemixTransport.WorkspaceGuidance,control.instructions.text);
        }
        [Test] public void FreshSceneRestoresSessionScaleAndStorageHasNoWorkspaceTransforms()
        {
            control.SetHeight(.5f);manager.MoveWorkspace(manager.workspace.position+Vector3.one*.1f);
            string json=JsonUtility.ToJson(RemixSaveStore.Capture(manager.library.Current.id,manager.automation,manager.mix));Assert.IsFalse(json.Contains("VerticalScale"));Assert.IsFalse(json.Contains("sessionOffset"));
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");var fresh=Object.FindFirstObjectByType<RemixTableManager>();fresh.workspaceInteraction.Initialize();Assert.AreEqual(1,fresh.workspaceInteraction.VerticalScale);
        }
        [Test] public void WarmedScalingUpdatesAllocateNoManagedMemory()
        {
            Begin();var a=Hand(-.12f);var b=Hand(.12f);gestures.ProcessHands(a,b,.016f);
            long before=System.GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<30;i++)gestures.ProcessHands(a,b,.016f);long bytes=System.GC.GetAllocatedBytesForCurrentThread()-before;Assert.AreEqual(0,bytes);
        }
    }
}
