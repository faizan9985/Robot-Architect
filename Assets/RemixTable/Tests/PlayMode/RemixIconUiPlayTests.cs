using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace RemixTable.Tests
{
    public class RemixIconUiPlayTests : RemixSavedSceneTest
    {
        [UnityTest] public IEnumerator SavedSceneIconsPreserveClicksNumbersTooltipsAndAllOriginalTargets()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(1);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var ui=manager.GetComponent<RemixUiPresentation>();Assert.AreEqual(45,ui.controls.Length);
            var filter=ui.controls.Single(c=>c.role=="FilterMode");var bounds=((RectTransform)filter.transform).rect;
            ExecuteEvents.Execute(filter.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            yield return null;Assert.AreEqual(RibbonInteractionManager.ControlMode.Filter,manager.interactions.Mode);Assert.IsTrue(filter.selectedMark.enabled);
            filter.OnPointerEnter(null);ui.tooltip.Advance(.61f);Assert.IsTrue(ui.tooltip.Visible);Assert.AreEqual("Filter",ui.tooltip.label.text);
            filter.OnPointerClick(null);Assert.IsFalse(ui.tooltip.Visible);
            var solo=ui.controls.Single(c=>c.role=="DrumsSolo");solo.button.onClick.Invoke();yield return null;Assert.IsTrue(manager.mix.soloed[(int)StemId.Drums]);Assert.IsTrue(solo.selectedMark.enabled);solo.button.onClick.Invoke();
            var speed=manager.speedSelector;speed.speedButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);Assert.IsTrue(speed.optionsGroup.blocksRaycasts);speed.optionButtons[5].onClick.Invoke();yield return null;Assert.AreEqual("1.5\u00d7",speed.speedLabel.text);
            var zoom=manager.navigationToolbar.zoom;zoom.expand.onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);zoom.wide.onClick.Invoke();yield return new WaitForSecondsRealtime(1.4f);Assert.AreEqual("16s",zoom.buttonLabel.text);
            Assert.AreEqual(bounds,((RectTransform)filter.transform).rect);Assert.IsFalse(ui.tooltip.GetComponent<CanvasGroup>().blocksRaycasts);
            foreach(var c in ui.controls){Assert.NotNull(c.button);if(c.icon!=null)Assert.NotNull(c.icon.sprite);Assert.IsEmpty(c.GetComponentsInChildren<Collider>());}
            Assert.AreEqual(1,Object.FindObjectsByType<RemixUiTooltip>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        }
    }
}
