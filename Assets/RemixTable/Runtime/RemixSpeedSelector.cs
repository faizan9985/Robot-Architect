using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    public sealed class RemixSpeedSelector : MonoBehaviour
    {
        public RemixTableManager manager;
        public Button speedButton;
        public TMP_Text speedLabel;
        public RectTransform optionsPanel;
        public CanvasGroup optionsGroup;
        public Button[] optionButtons;
        public RectTransform optionsContent;
        public RemixNavigationToolbar toolbar;
        public float animationSeconds => toolbar!=null?toolbar.openingSeconds:.28f;
        public Color idleColor=new Color(.07f,.11f,.15f,1),selectedColor=new Color(.13f,.42f,.62f,1);
        float shownSpeed=-1;
        public bool IsOpen => toolbar!=null && toolbar.ActiveMenu==RemixNavigationToolbar.Menu.Speed;
        public bool OptionsVisible => optionsPanel!=null && optionsPanel.gameObject.activeInHierarchy;
        public float AnimationProgress => toolbar!=null?toolbar.SpeedReveal:0;
        public static string Label(float speed) => speed.ToString("0.##",CultureInfo.InvariantCulture)+"\u00d7";
        public void Toggle() => toolbar?.Toggle(RemixNavigationToolbar.Menu.Speed);
        public void SetOpen(bool value) => toolbar?.SetMenu(RemixNavigationToolbar.Menu.Speed,value);
        public void SelectPreset(int index)
        {
            if(index<0 || index>=RemixAudioManager.SpeedPresetCount || manager==null || manager.audioManager==null) return;
            if(!manager.audioManager.SetPlaybackSpeed(RemixAudioManager.SpeedPreset(index))) return;
            Refresh();SetOpen(false);
        }
        void OnEnable() { shownSpeed=-1;Refresh(); }
        void OnDisable() => SetOpen(false);
        void Update()
        {
            if(manager!=null && manager.audioManager!=null && shownSpeed!=manager.audioManager.PlaybackSpeed) Refresh();
        }
        public void AdvanceAnimation(float deltaTime) => toolbar?.AdvanceAnimation(deltaTime);
        public void Refresh()
        {
            shownSpeed=manager!=null && manager.audioManager!=null ? manager.audioManager.PlaybackSpeed : 1;
            if(speedLabel!=null) speedLabel.text=Label(shownSpeed);
            if(optionButtons!=null) for(int i=0;i<optionButtons.Length;i++)
                if(optionButtons[i]!=null && optionButtons[i].targetGraphic!=null)
                    optionButtons[i].targetGraphic.color=Mathf.Approximately(shownSpeed,RemixAudioManager.SpeedPreset(i))?selectedColor:idleColor;
        }
    }
}
