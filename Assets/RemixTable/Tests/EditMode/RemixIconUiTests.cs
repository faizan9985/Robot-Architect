using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    public class RemixIconUiTests
    {
        RemixUiPresentation ui;RemixTableManager manager;
        [SetUp] public void Setup()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");manager=Object.FindFirstObjectByType<RemixTableManager>();ui=manager.GetComponent<RemixUiPresentation>();Assert.NotNull(ui);ui.Initialize();
        }
        RemixIconButton Control(string role)=>ui.controls.Single(c=>c.role==role);
        [Test] public void AllOriginalControlsKeepSemanticNamesReferencesAndPersistentActions()
        {
            Assert.AreEqual(45,ui.controls.Length);Assert.AreEqual(45,ui.controls.Select(c=>c.button).Distinct().Count());
            foreach(var c in ui.controls)
            {
                Assert.IsNotEmpty(c.semanticName);Assert.AreSame(c.button.GetComponent<Image>(),c.button.targetGraphic);
                if(c.role!="Open Menu Button" && c.role!="Close Button")
                {
                    Assert.AreEqual(1,c.button.onClick.GetPersistentEventCount(),c.role);Assert.NotNull(c.button.onClick.GetPersistentTarget(0));Assert.IsNotEmpty(c.button.onClick.GetPersistentMethodName(0));
                }
                if(!c.numeric){Assert.NotNull(c.icon.sprite);Assert.IsFalse(c.icon.raycastTarget);if(c.sourceLabel!=null)Assert.IsFalse(c.sourceLabel.enabled);}
            }
            var mix=manager.workspace.GetComponentInChildren<RemixMixControls>(true);
            foreach(var buttons in new[]{mix.muteButtons,mix.soloButtons,mix.clearButtons}){Assert.AreEqual(4,buttons.Length);Assert.IsTrue(buttons.All(b=>b!=null && b.GetComponent<RemixIconButton>()!=null));}
            Assert.NotNull(manager.palmMenuCanvas.GetComponentInChildren<Slider>(true));Assert.NotNull(manager.palmMenuCanvas.GetComponentInChildren<Toggle>(true));
        }
        [Test] public void NumericLabelsAndAnimatedToolbarArePreserved()
        {
            var nav=manager.navigationToolbar;nav.ResetClosed();nav.speed.Refresh();nav.zoom.RefreshNow();
            Assert.AreEqual("1\u00d7",nav.speed.speedLabel.text);Assert.AreEqual("8s",nav.zoom.buttonLabel.text);
            for(int i=0;i<8;i++){var c=Control("Preset"+i);Assert.IsTrue(c.sourceLabel.enabled);Assert.AreEqual(RemixSpeedSelector.Label(RemixAudioManager.SpeedPreset(i)),c.sourceLabel.text);}
            foreach(var c in new[]{Control("Detail"),Control("Normal"),Control("Wide"),Control("Overview")})Assert.IsTrue(c.sourceLabel.enabled);
            var before=ui.controls.Select(c=>((RectTransform)c.transform).sizeDelta).ToArray();nav.speed.SetOpen(true);nav.AdvanceAnimation(.1f);Assert.That(nav.SpeedReveal,Is.InRange(.01f,.99f));nav.AdvanceAnimation(1);Assert.IsTrue(nav.speed.optionsGroup.blocksRaycasts);
            for(int i=0;i<ui.controls.Length;i++)Assert.AreEqual(before[i],((RectTransform)ui.controls[i].transform).sizeDelta);
        }
        [Test] public void IconsUseSingleAtlasAndRoundedBackgroundsKeepFullRectTargets()
        {
            var sprites=Resources.LoadAll<Sprite>("RemixUiAtlas");Assert.AreEqual(29,sprites.Length);Assert.AreEqual(1,sprites.Select(s=>s.texture).Distinct().Count());
            foreach(var c in ui.controls)
            {
                var image=(Image)c.button.targetGraphic;Assert.AreEqual(Image.Type.Sliced,image.type);Assert.AreEqual(0,image.alphaHitTestMinimumThreshold);Assert.IsTrue(image.raycastTarget);
                var rect=(RectTransform)c.transform;var before=rect.sizeDelta;var scale=rect.localScale;
                c.OnPointerEnter(null);c.Paint();c.OnPointerDown(null);c.Paint();c.OnPointerUp(null);c.OnPointerExit(null);c.Paint();
                Assert.AreEqual(before,rect.sizeDelta);Assert.AreEqual(scale,rect.localScale);
                foreach(var graphic in c.GetComponentsInChildren<Graphic>(true))if(graphic.name.StartsWith("Presentation")){Assert.IsFalse(graphic.raycastTarget);Assert.AreEqual(c.gameObject.layer,graphic.gameObject.layer);}
            }
            Assert.IsTrue(ui.controls.All(c=>c.GetComponentsInChildren<Collider>(true).Length==0));
        }
        [Test] public void IntentionalHoverShowsOneNamedTooltipAndSelectionExitFocusHideIt()
        {
            var filter=Control("FilterMode");var pitch=Control("PitchMode");filter.OnPointerEnter(null);ui.tooltip.Advance(.59f);Assert.IsFalse(ui.tooltip.Visible);ui.tooltip.Advance(.02f);Assert.IsTrue(ui.tooltip.Visible);Assert.AreEqual("Filter",ui.tooltip.label.text);
            pitch.OnSelect(null);Assert.IsFalse(ui.tooltip.Visible);ui.tooltip.Advance(.61f);Assert.AreEqual("Pitch",ui.tooltip.label.text);Assert.AreSame(pitch,ui.tooltip.Owner);
            pitch.OnPointerDown(null);Assert.IsFalse(ui.tooltip.Visible);filter.OnPointerEnter(null);filter.OnPointerExit(null);ui.tooltip.Advance(1);Assert.IsFalse(ui.tooltip.Visible);
            pitch.OnSelect(null);pitch.OnDeselect(null);ui.tooltip.Advance(1);Assert.IsFalse(ui.tooltip.Visible);
            pitch.OnSelect(null);ui.tooltip.Advance(1);pitch.OnSubmit(null);Assert.IsFalse(ui.tooltip.Visible);
            Assert.IsFalse(ui.tooltip.group.blocksRaycasts);Assert.IsFalse(ui.tooltip.group.interactable);Assert.AreEqual(0,ui.tooltip.GetComponentsInChildren<Collider>().Length);
            foreach(var graphic in ui.tooltip.GetComponentsInChildren<Graphic>())Assert.IsFalse(graphic.raycastTarget);
            Assert.AreEqual(1,Object.FindObjectsByType<RemixUiTooltip>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            Assert.Greater(manager.workspace.InverseTransformPoint(ui.tooltip.transform.position).y,manager.ribbons.Max(r=>r.transform.localPosition.y)+manager.design.laneHeight*.5f);
        }
        [Test] public void PointerClicksInvokeOriginalModeMixerAndUndoActionsWhileHoverDoesNot()
        {
            var filter=Control("FilterMode");var original=manager.interactions.Mode;filter.OnPointerEnter(null);ui.tooltip.Advance(1);Assert.AreEqual(original,manager.interactions.Mode);
            filter.button.onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);
            ExecuteEvents.Execute(filter.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);Assert.AreEqual(RibbonInteractionManager.ControlMode.Filter,manager.interactions.Mode);
            var mute=Control("BassMute");mute.button.onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);mute.button.onClick.Invoke();Assert.IsTrue(manager.mix.muted[(int)StemId.Bass]);mute.Paint();Assert.IsTrue(mute.Selected);Assert.IsTrue(mute.selectedMark.enabled);
            var clear=Control("BassClear");clear.button.onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);clear.button.onClick.Invoke();
            var undo=Control("Undo");undo.button.onClick.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);undo.button.onClick.Invoke();
        }
        [Test] public void InitializationIsIdempotentAndDisabledIconsAreVisiblySubdued()
        {
            int graphics=Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            ui.Initialize();Assert.AreEqual(graphics,Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            var control=Control("PitchMode");control.button.interactable=true;control.Paint();float alpha=control.icon.color.a;control.button.interactable=false;control.Paint();Assert.Less(control.icon.color.a,alpha);control.OnPointerEnter(null);ui.tooltip.Advance(1);Assert.IsFalse(ui.tooltip.Visible);
        }
        [Test] public void WarmedPresentationUpdatesAllocateNoManagedMemory()
        {
            foreach(var c in ui.controls)c.Paint();foreach(var c in ui.controls)c.Paint();
            long before=System.GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<30;i++)foreach(var c in ui.controls)c.Paint();long bytes=System.GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.AreEqual(0,bytes);
        }
    }
}
