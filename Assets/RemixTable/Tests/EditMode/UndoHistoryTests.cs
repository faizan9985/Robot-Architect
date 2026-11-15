using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable.Tests
{
    // UNDO / REDO revert whole pinch holds. A hold that changes nothing records no step.
    public class UndoHistoryTests
    {
        GameObject root;
        RemixTableManager manager;
        RibbonView ribbon;
        RibbonInteractionManager gestures;
        [SetUp] public void Setup()
        {
            root=new GameObject("undo fixture");root.SetActive(false);
            manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();manager.workspace=root.transform;
            manager.library=root.AddComponent<SongLibraryManager>();manager.library.songs=new[]{new SongMetadata{id="undo",duration=32}};
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
        // Pinch at x, lift by `lift`, release.
        void Hold(float x,float lift)
        {
            gestures.ProcessHands(Hand(x,0),default,.016f);
            gestures.ProcessHands(Hand(x,lift),default,.016f);
            gestures.ProcessHands(default,default,.016f);
        }
        float Gain(float time) => manager.automation.EvaluateGain("undo",StemId.Drums,time,32,manager.design.gainBoundaryFade);

        [Test] public void NothingToUndoAtStart()
        {
            Assert.IsFalse(manager.history.CanUndo);Assert.IsFalse(manager.history.CanRedo);
            Assert.IsFalse(manager.history.Undo(manager.automation));
        }
        [Test] public void AHoldThatChangesNothingRecordsNoStep()
        {
            Hold(0,0);
            Assert.IsEmpty(manager.automation.edits);
            Assert.IsFalse(manager.history.CanUndo);
        }
        [Test] public void UndoRevertsAWholeHoldAndRedoReappliesIt()
        {
            Hold(0,.03f);
            float edited=Gain(10);
            Assert.AreNotEqual(1f,edited,"the hold edited the gain");
            Assert.AreEqual(1,manager.history.UndoCount,"one step per hold, not per frame");
            Assert.IsTrue(manager.history.Undo(manager.automation));
            Assert.IsEmpty(manager.automation.edits);
            Assert.AreEqual(1f,Gain(10),.0001f);
            Assert.IsTrue(manager.history.CanRedo);
            Assert.IsTrue(manager.history.Redo(manager.automation));
            Assert.AreEqual(edited,Gain(10),.0001f);
            Assert.IsFalse(manager.history.CanRedo);
        }
        [Test] public void UndoStepsBackOneHoldAtATime()
        {
            Hold(0,.03f);float first=Gain(10);
            Hold(0,-.03f);float second=Gain(10);
            Assert.AreNotEqual(first,second);
            manager.history.Undo(manager.automation);
            Assert.AreEqual(first,Gain(10),.0001f);
            manager.history.Undo(manager.automation);
            Assert.AreEqual(1f,Gain(10),.0001f);
        }
        [Test] public void FilterHoldsAreUndoneToo()
        {
            gestures.Mode=RibbonInteractionManager.ControlMode.Filter;
            Hold(0,.03f);
            Assert.AreNotEqual(0f,manager.automation.Evaluate("undo",StemId.Drums,ParameterType.Effect,10,32));
            manager.history.Undo(manager.automation);
            Assert.AreEqual(0f,manager.automation.Evaluate("undo",StemId.Drums,ParameterType.Effect,10,32));
        }
        [Test] public void ANewEditClearsRedo()
        {
            Hold(0,.03f);manager.history.Undo(manager.automation);
            Assert.IsTrue(manager.history.CanRedo);
            Hold(0,-.03f);
            Assert.IsFalse(manager.history.CanRedo);
        }
        [Test] public void UndoneStateIsNotChangedByLaterEdits()
        {
            Hold(0,.03f);float first=Gain(10);
            Hold(0,.03f);                       // Continues editing the same region edit object.
            manager.history.Undo(manager.automation);
            Hold(0,.03f);                       // Edits the restored copy again...
            manager.history.Undo(manager.automation);
            Assert.AreEqual(first,Gain(10),.0001f,"...but the stored snapshot stayed intact");
        }
        [Test] public void HistoryIsCappedAtMaxSteps()
        {
            for(int i=0;i<RemixEditHistory.MaxSteps+5;i++) Hold(0,i%2==0?.03f:-.03f);
            Assert.AreEqual(RemixEditHistory.MaxSteps,manager.history.UndoCount);
        }
        [Test] public void ButtonsUndoRedoAndDimWhenUnavailable()
        {
            var ui=new GameObject("history ui");ui.SetActive(false);
            var controls=ui.AddComponent<RemixHistoryControls>();controls.manager=manager;
            Button MakeButton(string name){var go=new GameObject(name);go.transform.SetParent(ui.transform);var img=go.AddComponent<Image>();var b=go.AddComponent<Button>();b.targetGraphic=img;return b;}
            controls.undoButton=MakeButton("undo");controls.redoButton=MakeButton("redo");
            ui.SetActive(true);
            controls.Undo(); // Nothing to undo: a harmless press that refreshes the buttons (EditMode skips OnEnable).
            Assert.IsEmpty(manager.automation.edits);
            Assert.IsFalse(controls.undoButton.interactable);Assert.IsFalse(controls.redoButton.interactable);
            Hold(0,.03f);
            controls.Undo();
            Assert.IsEmpty(manager.automation.edits);
            Assert.IsFalse(controls.undoButton.interactable);Assert.IsTrue(controls.redoButton.interactable);
            controls.Redo();
            Assert.IsNotEmpty(manager.automation.edits);
            Assert.IsTrue(controls.undoButton.interactable);Assert.IsFalse(controls.redoButton.interactable);
            Object.DestroyImmediate(ui);
        }
        [Test] public void UndoDuringAHoldRevertsThatHold()
        {
            var ui=new GameObject("history ui");
            var controls=ui.AddComponent<RemixHistoryControls>();controls.manager=manager;
            gestures.ProcessHands(Hand(0,0),default,.016f);
            gestures.ProcessHands(Hand(0,.03f),default,.016f);
            Assert.IsNotEmpty(manager.automation.edits);
            controls.Undo();
            Assert.IsNull(gestures.ActiveRibbon,"the hold was ended");
            Assert.IsEmpty(manager.automation.edits);
            Object.DestroyImmediate(ui);
        }
    }
}
