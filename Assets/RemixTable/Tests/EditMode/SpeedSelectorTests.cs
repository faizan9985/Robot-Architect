using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace RemixTable.Tests
{
    public class SpeedSelectorTests
    {
        RemixSpeedSelector selector;
        [SetUp] public void Setup()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");
            selector=Object.FindFirstObjectByType<RemixSpeedSelector>(FindObjectsInactive.Include);
            Assert.NotNull(selector);selector.SetOpen(false);selector.AdvanceAnimation(1);selector.Refresh();
        }
        [Test] public void LaunchStartsAtNormalSpeedAndHasEightBoundPresets()
        {
            Assert.AreEqual(1,selector.manager.audioManager.PlaybackSpeed);Assert.AreEqual("1\u00d7",selector.speedLabel.text);
            Assert.IsFalse(selector.optionsPanel.gameObject.activeSelf);Assert.AreEqual(8,selector.optionButtons.Length);
            for(int i=0;i<8;i++)
            {
                var button=selector.optionButtons[i];
                Assert.AreSame(selector,button.onClick.GetPersistentTarget(0));
                button.onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);
                selector.SetOpen(true);selector.AdvanceAnimation(1);button.onClick.Invoke();
                Assert.AreEqual((i+1)*.25f,selector.manager.audioManager.PlaybackSpeed);
                Assert.AreEqual(RemixSpeedSelector.Label((i+1)*.25f),selector.speedLabel.text);
                Assert.AreEqual(selector.selectedColor,button.targetGraphic.color);Assert.IsFalse(selector.IsOpen);
                Assert.IsFalse(selector.optionsGroup.blocksRaycasts);
                selector.AdvanceAnimation(1);Assert.IsFalse(selector.optionsPanel.gameObject.activeSelf);
            }
        }
        [Test] public void SlideFadeReversesWithoutSnappingAndOnlyOpenOptionsReceiveInput()
        {
            var history=selector.GetComponentInParent<RemixHistoryControls>();
            var undo=history.undoButton.transform.localPosition;var redo=history.redoButton.transform.localPosition;var speed=selector.speedButton.transform.position;
            selector.Toggle();Assert.IsTrue(selector.optionsPanel.gameObject.activeSelf);Assert.AreEqual(0,selector.optionsGroup.alpha);
            selector.AdvanceAnimation(selector.animationSeconds*.5f);
            Assert.That(selector.optionsGroup.alpha,Is.EqualTo(.875f).Within(.0001));
            Assert.Less(selector.optionsPanel.sizeDelta.x,selector.optionsContent.sizeDelta.x);Assert.IsFalse(selector.optionsGroup.blocksRaycasts);
            var position=selector.optionsPanel.anchoredPosition;var alpha=selector.optionsGroup.alpha;
            selector.Toggle();Assert.AreEqual(position,selector.optionsPanel.anchoredPosition);Assert.AreEqual(alpha,selector.optionsGroup.alpha);
            selector.AdvanceAnimation(1);Assert.IsFalse(selector.optionsPanel.gameObject.activeSelf);
            selector.Toggle();selector.AdvanceAnimation(1);
            Assert.AreEqual(selector.optionsContent.sizeDelta.x,selector.optionsPanel.sizeDelta.x);Assert.IsTrue(selector.optionsGroup.blocksRaycasts);
            Assert.AreEqual(undo,history.undoButton.transform.localPosition);Assert.AreEqual(redo,history.redoButton.transform.localPosition);Assert.AreEqual(speed,selector.speedButton.transform.position);
        }
        [Test] public void TargetsStayFullSizeAboveRibbonsAndInsideSeatedWidth()
        {
            var history=selector.GetComponentInParent<RemixHistoryControls>();
            Assert.AreSame(history.GetComponent<Canvas>(),selector.GetComponentInParent<Canvas>());
            Assert.IsEmpty(selector.GetComponentsInParent<RectMask2D>(true));
            Assert.IsEmpty(selector.GetComponentsInParent<Mask>(true));
            Assert.AreEqual(new Vector2(50,40),((RectTransform)selector.speedButton.transform).sizeDelta);
            foreach(var button in selector.optionButtons) Assert.AreEqual(new Vector2(50,40),((RectTransform)button.transform).sizeDelta);
            Assert.AreEqual(.28f,selector.animationSeconds);Assert.AreEqual(.25f,selector.toolbar.closingSeconds);
            selector.SetOpen(true);selector.AdvanceAnimation(1);
            var corners=new Vector3[4];selector.optionsPanel.GetWorldCorners(corners);
            foreach(var corner in corners)
            {
                var point=selector.manager.workspace.InverseTransformPoint(corner);
                Assert.LessOrEqual(point.x,selector.manager.design.panelWidth*.5f);
                Assert.Greater(point.y,1.5f*selector.manager.design.spacing+selector.manager.design.laneHeight*.5f);
            }
        }
        [Test] public void InvalidRatesAndIndicesCannotChangeTheClockOrZoom()
        {
            var audio=selector.manager.audioManager;var clock=selector.manager.clock;clock.elapsed=23;
            foreach(float rate in new[]{float.NaN,float.PositiveInfinity,0,.3f,2.25f}) Assert.IsFalse(audio.SetPlaybackSpeed(rate));
            selector.SelectPreset(-1);selector.SelectPreset(8);Assert.AreEqual(1,audio.PlaybackSpeed);Assert.AreEqual(23,clock.elapsed);
            float zoom=selector.manager.zoom.VisibleSeconds;selector.SelectPreset(0);
            Assert.AreEqual(zoom,selector.manager.zoom.VisibleSeconds);Assert.AreEqual(23,clock.elapsed);
        }
        [Test] public void OpenPopoutCannotAcquireAnEmptySpaceZoom()
        {
            var manager=selector.manager;var gestures=manager.interactions;
            var left=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=manager.workspace.TransformPoint(new Vector3(-.15f,.3f,-.25f))};
            var right=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=manager.workspace.TransformPoint(new Vector3(.15f,.3f,-.25f))};
            selector.SetOpen(true);selector.AdvanceAnimation(1);gestures.ProcessHands(left,right,.016f);
            Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);
            selector.SetOpen(false);selector.AdvanceAnimation(1);gestures.ProcessHands(left,right,.016f);
            Assert.AreEqual(RibbonInteractionManager.GestureState.ZoomPending,gestures.State);
            selector.SetOpen(true);Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);
            Assert.IsTrue(gestures.ZoomReleaseRequired);
        }
    }
}
