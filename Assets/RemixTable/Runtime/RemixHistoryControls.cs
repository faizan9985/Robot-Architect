using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // UNDO / REDO buttons. Each step reverts one whole pinch hold. Buttons dim when there is nothing to step to.
    public sealed class RemixHistoryControls : MonoBehaviour
    {
        public RemixTableManager manager;
        public Button undoButton, redoButton;
        int shownVersion = -1;
        // A hold still in progress is finished first, so UNDO during a hold reverts that hold.
        public void Undo() { if (Ready()) { EndHold(); manager.history.Undo(manager.automation); Refresh(); } }
        public void Redo() { if (Ready()) { EndHold(); manager.history.Redo(manager.automation); Refresh(); } }
        void EndHold() { if (manager.interactions != null) manager.interactions.Cancel(); }
        bool Ready() => manager != null && manager.history != null;
        void OnEnable() { shownVersion = -1; Refresh(); }
        void Update() { if (Ready() && shownVersion != manager.history.Version) Refresh(); }
        void Refresh()
        {
            if (!Ready()) return;
            shownVersion = manager.history.Version;
            if (undoButton != null) undoButton.interactable = manager.history.CanUndo;
            if (redoButton != null) redoButton.interactable = manager.history.CanRedo;
        }
    }
}
