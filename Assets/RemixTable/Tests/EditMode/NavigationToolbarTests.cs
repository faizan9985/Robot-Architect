using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace RemixTable.Tests
{
    public class NavigationToolbarTests
    {
        RemixTableManager manager;RemixNavigationToolbar toolbar;RemixSpeedSelector speed;TimelineZoomUI zoom;RemixHistoryControls history;
        [SetUp] public void Setup()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");
            manager=Object.FindFirstObjectByType<RemixTableManager>();toolbar=manager.navigationToolbar;
            Assert.NotNull(toolbar);speed=toolbar.speed;zoom=toolbar.zoom;history=toolbar.GetComponent<RemixHistoryControls>();
            toolbar.ResetClosed();speed.Refresh();zoom.RefreshNow();
        }
        [Test] public void CollapsedOrderAndBindingsUseOneCenteredCanvas()
        {
            var buttons=new[]{history.undoButton,history.redoButton,speed.speedButton,zoom.expand};
            float previous=float.NegativeInfinity;
            foreach(var button in buttons)
            {
                var p=toolbar.transform.InverseTransformPoint(button.transform.position);
                Assert.Greater(p.x,previous);previous=p.x;Assert.AreEqual(0,p.y,.001f);
                Assert.AreEqual(new Vector2(50,40),((RectTransform)button.transform).sizeDelta);
                Assert.AreEqual(1,button.onClick.GetPersistentEventCount());Assert.AreSame(toolbar.GetComponent<Canvas>(),button.GetComponentInParent<Canvas>());
            }
            float left=toolbar.transform.InverseTransformPoint(buttons[0].transform.position).x-25;
            float right=toolbar.transform.InverseTransformPoint(buttons[3].transform.position).x+25;
            Assert.AreEqual(0,(left+right)*.5f,.001f);Assert.AreEqual("1\u00d7",speed.speedLabel.text);Assert.AreEqual("8s",zoom.buttonLabel.text);
            Assert.AreEqual(8,speed.optionButtons.Length);
            var presets=new[]{zoom.detail,zoom.normal,zoom.wide,zoom.overview};var labels=new[]{"4s","8s","16s","32s"};
            for(int i=0;i<4;i++) Assert.AreEqual(labels[i],presets[i].GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.IsEmpty(toolbar.content.GetComponentsInChildren<LayoutGroup>(true));Assert.IsEmpty(toolbar.content.GetComponentsInChildren<ContentSizeFitter>(true));
        }
        [TestCase(RemixNavigationToolbar.Menu.Speed)] [TestCase(RemixNavigationToolbar.Menu.Zoom)]
        public void HorizontalRevealUsesFixedTargetsAndReturnsToCollapsedPositions(RemixNavigationToolbar.Menu menu)
        {
            var collapsed=toolbar.content.anchoredPosition;var zoomPosition=toolbar.zoomRoot.anchoredPosition;float y=toolbar.transform.localPosition.y;
            var viewport=menu==RemixNavigationToolbar.Menu.Speed?speed.optionsPanel:(RectTransform)zoom.options.transform;
            var group=menu==RemixNavigationToolbar.Menu.Speed?speed.optionsGroup:zoom.optionsGroup;
            toolbar.SetMenu(menu,true);Assert.AreEqual(0,viewport.sizeDelta.x);Assert.IsFalse(group.blocksRaycasts);
            toolbar.AdvanceAnimation(.14f);Assert.AreEqual(230*.875f,viewport.sizeDelta.x,.001f);Assert.AreEqual(.875f,group.alpha,.001f);
            Assert.AreEqual(0,viewport.anchoredPosition.y);Assert.AreEqual(31,viewport.anchoredPosition.x);
            if(menu==RemixNavigationToolbar.Menu.Speed) Assert.Greater(toolbar.zoomRoot.anchoredPosition.x,zoomPosition.x);
            toolbar.AdvanceAnimation(.14f);Assert.AreEqual(230,viewport.sizeDelta.x);Assert.IsTrue(group.interactable);
            foreach(var button in speed.optionButtons) Assert.AreEqual(new Vector2(50,40),((RectTransform)button.transform).sizeDelta);
            Assert.AreEqual(y,toolbar.transform.localPosition.y);Assert.AreEqual(0,toolbar.content.anchoredPosition.y);
            toolbar.SetMenu(menu,false);Assert.IsFalse(group.blocksRaycasts);Assert.AreEqual(230,viewport.sizeDelta.x);
            toolbar.AdvanceAnimation(.125f);Assert.AreEqual(115,viewport.sizeDelta.x,.001f);
            toolbar.AdvanceAnimation(.125f);Assert.AreEqual(collapsed,toolbar.content.anchoredPosition);Assert.AreEqual(zoomPosition,toolbar.zoomRoot.anchoredPosition);
            Assert.AreEqual(0,viewport.sizeDelta.x);Assert.IsFalse(viewport.gameObject.activeSelf);
        }
        [Test] public void RapidSwitchesHaveNoSnapsOverlapOrInvisibleInput()
        {
            for(int i=0;i<90;i++)
            {
                var content=toolbar.content.anchoredPosition;var zoomPosition=toolbar.zoomRoot.anchoredPosition;
                if(i%3==0) speed.Toggle();else if(i%3==1) zoom.ToggleOptions();else toolbar.SetMenu(toolbar.ActiveMenu,false);
                Assert.AreEqual(content,toolbar.content.anchoredPosition);Assert.AreEqual(zoomPosition,toolbar.zoomRoot.anchoredPosition);
                toolbar.AdvanceAnimation(.017f);
                float speedRight=84+31+speed.optionsPanel.sizeDelta.x;
                Assert.GreaterOrEqual(toolbar.zoomRoot.anchoredPosition.x-25,speedRight-.001f);
                Assert.IsFalse(speed.optionsGroup.blocksRaycasts && zoom.optionsGroup.blocksRaycasts);
                var corners=new Vector3[4];toolbar.background.GetWorldCorners(corners);
                foreach(var corner in corners) Assert.LessOrEqual(Mathf.Abs(manager.workspace.InverseTransformPoint(corner).x),manager.design.panelWidth*.5f+.0001f);
            }
            toolbar.SetMenu(RemixNavigationToolbar.Menu.Zoom,true);toolbar.AdvanceAnimation(1);
            Assert.AreEqual(0,toolbar.SpeedReveal);Assert.AreEqual(1,toolbar.ZoomReveal);Assert.IsFalse(speed.optionsPanel.gameObject.activeSelf);Assert.IsTrue(zoom.optionsGroup.blocksRaycasts);
            toolbar.ResetClosed();Assert.IsFalse(toolbar.MenusVisible);Assert.IsFalse(zoom.optionsGroup.blocksRaycasts);
        }
        [Test] public void ContinuousAndDeferredZoomReflectsSongTimeWithoutSnappingOrChangingSpeed()
        {
            speed.SelectPreset(7);Assert.AreEqual(2,manager.audioManager.PlaybackSpeed);
            var line=manager.workspace.GetComponentsInChildren<LineRenderer>();var playhead=System.Array.Find(line,l=>l.name=="FixedPlaybackIndicator");
            var first=playhead.GetPosition(0);var last=playhead.GetPosition(1);
            manager.zoom.Request(11.37f);manager.zoom.Tick(10,false,.16f);zoom.Refresh();
            Assert.AreEqual("11.4s",zoom.buttonLabel.text);Assert.AreEqual(11.37f,manager.zoom.TargetSeconds);
            foreach(var button in new[]{zoom.detail,zoom.normal,zoom.wide,zoom.overview}) Assert.AreEqual(zoom.idleColor,button.targetGraphic.color);
            manager.ribbons[0].selected=true;zoom.Overview();manager.zoom.Tick(10,manager.IsEditing,.16f);zoom.Refresh();
            Assert.AreEqual("11.4s",zoom.buttonLabel.text);Assert.That(zoom.workspaceRange.text,Does.Contain("queued"));
            manager.ribbons[0].selected=false;manager.zoom.Tick(10,false,.16f);zoom.Refresh();Assert.AreEqual("32s",zoom.buttonLabel.text);Assert.AreEqual(zoom.selectedColor,zoom.overview.targetGraphic.color);
            Assert.AreEqual(first,playhead.GetPosition(0));Assert.AreEqual(last,playhead.GetPosition(1));Assert.AreEqual(2,manager.audioManager.PlaybackSpeed);
            var ribbon=manager.ribbons[0];ribbon.visualTime=10;
            Assert.AreEqual(16,Mathf.Repeat(ribbon.Timestamp(manager.design.width*.5f)-ribbon.Timestamp(0),manager.clock.duration),.001f);
        }
        [Test] public void PalmZoomControlsAreRemovedButOtherFeaturesAndGuidanceRemain()
        {
            var panel=manager.palmMenuCanvas.transform.Find("Panel");
            Assert.IsNull(panel.Find("TimelineZoom"));Assert.IsNull(panel.Find("TimelineZoomOptions"));
            foreach(string name in new[]{"Title","Passthrough Toggle","Volume Label","Volume Slider","Close Button","RemixGestureInstructions"}) Assert.NotNull(panel.Find(name));
            foreach(var button in panel.GetComponentsInChildren<Button>(true)) for(int i=0;i<button.onClick.GetPersistentEventCount();i++) Assert.IsFalse(button.onClick.GetPersistentTarget(i) is TimelineZoomUI);
            var transport=manager.workspace.GetComponentInChildren<RemixTransport>();Assert.AreEqual(RemixTransport.Guidance,transport.palmInstructions.text);Assert.IsTrue(transport.palmInstructions.gameObject.activeSelf);
            var rect=(RectTransform)transport.palmInstructions.transform;Assert.AreEqual(new Vector2(0,-82),rect.anchoredPosition);Assert.AreEqual(new Vector2(316,142),rect.sizeDelta);
        }
        [Test] public void ReparentedUndoRedoButtonsRemainOperational()
        {
            manager.history.BeginGesture(manager.automation);
            manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=StemId.Bass,parameter=ParameterType.Gain,wholeStem=true,value=.4f});
            manager.history.EndGesture(manager.automation);
            history.undoButton.onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);history.redoButton.onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);
            history.undoButton.onClick.Invoke();Assert.IsEmpty(manager.automation.edits);Assert.IsTrue(manager.history.CanRedo);
            history.redoButton.onClick.Invoke();Assert.AreEqual(.4f,manager.automation.edits[0].value);Assert.AreEqual(20,RemixEditHistory.MaxSteps);
        }
    }
}
