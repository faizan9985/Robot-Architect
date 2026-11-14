using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // EDIT / LIVE buttons: whether holding a ribbon pauses the song (EDIT) or it keeps playing (LIVE).
    public sealed class RemixPlaybackModeSelector : MonoBehaviour
    {
        public RemixTableManager manager;
        public Button editButton, liveButton;
        public Color idleColor = new Color(.07f, .11f, .15f, 1), selectedColor = new Color(.13f, .42f, .62f, 1);
        bool? shown;
        public void SelectEdit() => Select(false);
        public void SelectLive() => Select(true);
        public void Select(bool live)
        {
            if (manager == null || manager.audioManager == null) return;
            manager.audioManager.LiveMode = live;
            Refresh();
        }
        void OnEnable() { shown = null; Refresh(); }
        void Update() { if (manager != null && manager.audioManager != null && shown != manager.audioManager.LiveMode) Refresh(); }
        void Refresh()
        {
            if (manager == null || manager.audioManager == null) return;
            bool live = manager.audioManager.LiveMode;
            shown = live;
            Paint(editButton, !live);
            Paint(liveButton, live);
        }
        void Paint(Button button, bool selected)
        {
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = selected ? selectedColor : idleColor;
        }
    }
}
