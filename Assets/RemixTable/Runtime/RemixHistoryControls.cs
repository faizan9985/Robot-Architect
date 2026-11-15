using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // UNDO / REDO icon buttons. Each step reverts one whole pinch hold. Buttons and their icons dim when there
    // is nothing to step to.
    public sealed class RemixHistoryControls : MonoBehaviour
    {
        public RemixTableManager manager;
        public Button undoButton, redoButton;
        public Image undoIcon, redoIcon;
        public Color iconColor = new Color(.8f, .88f, .94f, 1);
        [Range(0, 1)] public float dimmedIconAlpha = .3f;
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
            Paint();
        }
        public void Paint()
        {
            bool canUndo = Ready() && manager.history.CanUndo, canRedo = Ready() && manager.history.CanRedo;
            Paint(undoButton, undoIcon, canUndo);
            Paint(redoButton, redoIcon, canRedo);
        }
        void Paint(Button button, Image icon, bool available)
        {
            if (button != null) button.interactable = available;
            if (icon != null) { var c = iconColor; c.a *= available ? 1 : dimmedIconAlpha; icon.color = c; }
        }
    }
}
