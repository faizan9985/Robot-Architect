using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace RemixTable.Tests
{
    public class SeatedLayoutTests
    {
        RemixTableManager manager;
        RemixTransport transport;
        [SetUp] public void Setup()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");
            manager=Object.FindFirstObjectByType<RemixTableManager>();transport=Object.FindFirstObjectByType<RemixTransport>();
        }
        [Test] public void ArcCoordinatesWindowLimitsLoopAndAllocationBudget()
        {
            var d=manager.design;Assert.IsTrue(d.curvedTimeline);
            var ribbon=manager.ribbons[0];ribbon.visualTime=10;
            for(int i=0;i<=100;i++)
            {
                float x=(i/100f-.5f)*d.width;var point=d.ArcPoint(x);
                Assert.That(d.ArcCoordinate(point),Is.EqualTo(x).Within(.00001));
                Assert.That(new Vector2(point.x,point.z+d.Radius).magnitude,Is.EqualTo(d.Radius).Within(.00001));
            }
            Assert.AreEqual(Vector3.zero,d.ArcPoint(0));
            Assert.Greater(ribbon.Timestamp(d.width*.2f),10);Assert.Less(ribbon.Timestamp(-d.width*.2f),10);
            Assert.IsFalse(ribbon.CanSelectAt(ribbon.SurfacePoint(d.width*.45f),out _));
            ribbon.visualTime=manager.clock.duration-.1f;
            Assert.That(ribbon.Timestamp(d.width*.5f),Is.EqualTo(d.futureSeconds-.1f).Within(.001));
            // Only arithmetic and value types in the mapping hot path.
            d.ArcCoordinate(d.ArcPoint(.1f));
            long before=System.GC.GetAllocatedBytesForCurrentThread();float sum=0;
            for(int i=0;i<10000;i++)sum+=d.ArcCoordinate(d.ArcPoint(.1f));
            long allocated=System.GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Greater(sum,0);Assert.AreEqual(0,allocated);
            Assert.LessOrEqual(d.segments*2*4,2048);
        }
        [Test] public void FourLanesFitWithoutScrollingOrOverlappingHitZones()
        {
            var d=manager.design;Assert.IsTrue(d.constrainToLanes);
            Assert.That(d.panelWidth,Is.InRange(.65f,.75f));Assert.Less(3*d.spacing+d.laneHeight,.35f);
            Assert.Greater(d.panelHeight,3*d.spacing+d.laneHeight);Assert.Greater(d.panelWidth,d.width);
            var rows=manager.ribbons.OrderByDescending(x=>x.transform.localPosition.y).ToArray();
            CollectionAssert.AreEqual(new[]{StemId.Vocals,StemId.Drums,StemId.Bass,StemId.Melody},rows.Select(x=>x.stem).ToArray());
            for(int i=0;i<4;i++)
            {
                Assert.That(rows[i].transform.localPosition.y,Is.EqualTo(d.LaneY(i)).Within(.0001));
                Assert.IsNull(rows[i].GetComponentInParent<Canvas>());
                Assert.IsEmpty(rows[i].GetComponents<Collider>()); // Analytical hit volumes, not physics colliders.
                var bounds=rows[i].InteractionBounds;bounds.center=rows[i].transform.localPosition;
                for(int j=i+1;j<4;j++){var other=rows[j].InteractionBounds;other.center=rows[j].transform.localPosition;Assert.IsFalse(bounds.Intersects(other));}
            }
            Assert.IsEmpty(manager.workspace.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(manager.workspace.GetComponentsInChildren<ScrollRect>());
        }
        [Test] public void HandMappingSelectsOnlyItsLaneAndPreservesTimestampScale()
        {
            foreach(var ribbon in manager.ribbons)
            {
                ribbon.visualTime=10;float x=manager.design.width*.2f;
                var point=ribbon.SurfacePoint(x);
                Assert.IsTrue(ribbon.CanSelectAt(point,out _));
                foreach(var other in manager.ribbons)if(other!=ribbon)Assert.IsFalse(other.CanSelectAt(point,out _));
                Assert.That(ribbon.LocalX(point),Is.EqualTo(x).Within(.0001));
                Assert.That(ribbon.Timestamp(x),Is.EqualTo(10+manager.design.TimeOffset(x)).Within(.0001));
                Assert.IsFalse(ribbon.CanSelectAt(ribbon.transform.TransformPoint(new Vector3(0,manager.design.laneHeight,0)),out _));
            }
        }
        [Test] public void LaneLabelsDoNotCoverEditableWaveBands()
        {
            foreach(var ribbon in manager.ribbons)
            {
                var label=ribbon.GetComponentInChildren<TextMesh>();var bounds=label.GetComponent<Renderer>().bounds;
                float center=ribbon.transform.InverseTransformPoint(bounds.center).y, half=bounds.extents.y;
                Assert.Greater(center-half,manager.design.laneHeight*.5f);
                Assert.Less(center+half,manager.design.spacing-manager.design.laneHeight*.5f);
            }
        }
        [Test] public void ExtremeStoredDeformationsStayInsideLaneWithoutChangingAudioGain()
        {
            var d=manager.design;
            foreach(var ribbon in manager.ribbons)
            {
                manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=ribbon.stem,parameter=ParameterType.Gain,wholeStem=true,value=2.5f});
                manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=ribbon.stem,parameter=ParameterType.Pitch,wholeStem=true,value=12});
                for(int i=0;i<=100;i++)Assert.LessOrEqual(Mathf.Abs(ribbon.HeightAt((i/100f-.5f)*d.width))+d.thickness*1.8f+.003f,d.laneHeight*.5f+.00001f);
                Assert.AreEqual(2.5f,manager.automation.EvaluateGain(manager.library.Current.id,ribbon.stem,1,manager.clock.duration,d.gainBoundaryFade));
            }
        }
        [Test] public void TransportHasPersistentActionsComfortableTargetsAndExistingXriUI()
        {
            Assert.NotNull(transport);Assert.AreEqual(RenderMode.WorldSpace,transport.GetComponent<Canvas>().renderMode);
            Assert.NotNull(transport.GetComponent<TrackedDeviceGraphicRaycaster>());
            var buttons=new[]{transport.previousSong,transport.playPause,transport.nextSong};
            string[] actions={"PreviousSong","TogglePlayback","NextSong"};
            for(int i=0;i<3;i++)
            {
                Assert.AreEqual(1,buttons[i].onClick.GetPersistentEventCount());Assert.AreSame(transport,buttons[i].onClick.GetPersistentTarget(0));
                Assert.AreEqual(actions[i],buttons[i].onClick.GetPersistentMethodName(0));
                Assert.GreaterOrEqual(((RectTransform)buttons[i].transform).rect.height*transport.transform.localScale.y,.049f);
            }
            Assert.Less(transport.transform.localPosition.y+manager.design.transportHeight*.5f,-manager.design.panelHeight*.5f);
            Assert.IsFalse(Object.FindObjectsByType<SpatialSongPicker>(FindObjectsSortMode.None).Any(x=>x.isActiveAndEnabled));
            Assert.IsEmpty(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None)); // XRI creates its one runtime module.
        }
        [Test] public void PalmGuidanceFollowsExistingMenuVisibilityAndDoesNotCoverControls()
        {
            var instructions=transport.palmInstructions;Assert.AreEqual(RemixTransport.Guidance,instructions.text);
            Assert.IsFalse(instructions.raycastTarget);Assert.IsFalse(instructions.transform.IsChildOf(manager.workspace));
            var canvas=instructions.GetComponentInParent<Canvas>(true);Assert.AreEqual("Hand Menu Canvas",canvas.name);
            canvas.gameObject.SetActive(true);Assert.IsTrue(instructions.gameObject.activeInHierarchy);
            var rect=(RectTransform)instructions.transform;var own=new Rect(rect.anchoredPosition-rect.sizeDelta*.5f,rect.sizeDelta);
            foreach(var control in canvas.GetComponentsInChildren<Selectable>())
            {
                var r=(RectTransform)control.transform;
                Assert.IsFalse(own.Overlaps(new Rect(r.anchoredPosition-r.sizeDelta*.5f,r.sizeDelta)),control.name);
            }
            canvas.gameObject.SetActive(false);Assert.IsFalse(instructions.gameObject.activeInHierarchy);
            Assert.IsFalse(manager.workspace.GetComponentsInChildren<TextMesh>().Any(x=>x.text.Contains("PINCH + LIFT")));
        }
        [Test] public void ConfigurablePoseUsesInitialEyeHeightAndHorizontalGaze()
        {
            var d=manager.design;var eye=new Vector3(2,1.2f,-3);var pose=d.SeatedPose(eye,new Vector3(0,-.8f,1));
            Assert.That(pose.position.y,Is.EqualTo(eye.y-d.workspaceEyeDrop).Within(.0001));
            Assert.That(pose.position.z-eye.z,Is.EqualTo(d.curvedTimeline?d.Radius:d.workspaceDistance).Within(.0001));
            Assert.That(Vector3.Dot(pose.rotation*Vector3.up,Vector3.up),Is.EqualTo(1).Within(.0001));
            if(d.curvedTimeline)
            {
                Assert.That(pose.position+pose.rotation*new Vector3(0,0,-d.Radius),Is.EqualTo(eye-Vector3.up*d.workspaceEyeDrop));
                return;
            }
            // Reach assumption: a shoulder midpoint 20 cm below the eyes, not an anthropometric guarantee.
            var shoulder=eye-Vector3.up*.2f;
            foreach(float x in new[]{-d.width*.5f,d.width*.5f})foreach(float y in new[]{d.LaneY(0),d.LaneY(3),d.TransportY-.04f})
                Assert.Less(Vector3.Distance(shoulder,pose.position+pose.rotation*new Vector3(x,y,0)),.80f);
        }
    }
}
