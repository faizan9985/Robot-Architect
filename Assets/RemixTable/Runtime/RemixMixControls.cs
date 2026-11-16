using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // MIX row: M (mute), S (solo) and CLR (clear this stem's edits) for each stem.
    public sealed class RemixMixControls : MonoBehaviour
    {
        public RemixTableManager manager;
        // Indexed by (int)StemId.
        public Button[] muteButtons = new Button[4], soloButtons = new Button[4], clearButtons = new Button[4];
        public Color idleColor = new Color(.07f, .11f, .15f, 1), muteColor = new Color(.7f, .2f, .15f, 1), soloColor = new Color(.85f, .65f, .1f, 1);
        int shown = -1;
        public void MuteVocals() => Mute(StemId.Vocals);
        public void MuteDrums() => Mute(StemId.Drums);
        public void MuteBass() => Mute(StemId.Bass);
        public void MuteOther() => Mute(StemId.Melody);
        public void SoloVocals() => Solo(StemId.Vocals);
        public void SoloDrums() => Solo(StemId.Drums);
        public void SoloBass() => Solo(StemId.Bass);
        public void SoloOther() => Solo(StemId.Melody);
        public void ClearVocals() => Clear(StemId.Vocals);
        public void ClearDrums() => Clear(StemId.Drums);
        public void ClearBass() => Clear(StemId.Bass);
        public void ClearOther() => Clear(StemId.Melody);
        public void Mute(StemId stem) { if (manager != null) { manager.mix.ToggleMute(stem); Paint(); } }
        public void Solo(StemId stem) { if (manager != null) { manager.mix.ToggleSolo(stem); Paint(); } }
        public void Clear(StemId stem) { if (manager != null) manager.ResetStem(stem); }
        void OnEnable() { shown = -1; Paint(); }
        void Update() { if (manager != null && shown != manager.mix.Version) Paint(); }
        public void Paint()
        {
            if (manager == null) return;
            shown = manager.mix.Version;
            for (int i = 0; i < 4; i++)
            {
                Tint(muteButtons, i, manager.mix.muted[i] ? muteColor : idleColor);
                Tint(soloButtons, i, manager.mix.soloed[i] ? soloColor : idleColor);
            }
        }
        static void Tint(Button[] buttons, int i, Color color)
        {
            if (buttons != null && i < buttons.Length && buttons[i] != null && buttons[i].targetGraphic != null) buttons[i].targetGraphic.color = color;
        }
    }
}
