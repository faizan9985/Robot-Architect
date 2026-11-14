using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // VOLUME / FILTER buttons that choose what a one-hand hold on a ribbon edits.
    public sealed class RemixModeSelector : MonoBehaviour
    {
        public RemixTableManager manager;
        public Button volumeButton, filterButton;
        public Color idleColor = new Color(.07f, .11f, .15f, 1), selectedColor = new Color(.13f, .42f, .62f, 1);
        RibbonInteractionManager.ControlMode? shown;
        public void SelectVolume() => Select(RibbonInteractionManager.ControlMode.Volume);
        public void SelectFilter() => Select(RibbonInteractionManager.ControlMode.Filter);
        public void Select(RibbonInteractionManager.ControlMode mode)
        {
            if (manager == null || manager.interactions == null) return;
            manager.interactions.Mode = mode;
            Refresh();
        }
        void OnEnable() { shown = null; Refresh(); }
        void Update() { if (manager != null && manager.interactions != null && shown != manager.interactions.Mode) Refresh(); }
        void Refresh()
        {
            if (manager == null || manager.interactions == null) return;
            var mode = manager.interactions.Mode;
            shown = mode;
            Paint(volumeButton, mode == RibbonInteractionManager.ControlMode.Volume);
            Paint(filterButton, mode == RibbonInteractionManager.ControlMode.Filter);
        }
        void Paint(Button button, bool selected)
        {
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = selected ? selectedColor : idleColor;
        }
    }
}
