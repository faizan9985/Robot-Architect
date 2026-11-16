using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    /// <summary>UI adapter only: toolbar presets and gestures share manager.zoom, in song seconds.</summary>
    public sealed class TimelineZoomUI : MonoBehaviour
    {
        public RemixTableManager manager;
        public RemixNavigationToolbar toolbar;
        public TMP_Text buttonLabel,workspaceRange;
        public GameObject options;
        public RectTransform optionsContent;
        public CanvasGroup optionsGroup;
        public Button expand, detail, normal, wide, overview;
        public Color idleColor=new Color(.07f,.11f,.15f,1),selectedColor=new Color(.13f,.42f,.62f,1);
        int displayedTenths=-1,displayedState=-1;
        float displayedExact=-1;
        public void Detail() => Select(4);
        public void Normal() => Select(8);
        public void Wide() => Select(16);
        public void Overview() => Select(32);
        void Select(float seconds)
        {
            manager.zoom.Request(seconds);toolbar?.SetMenu(RemixNavigationToolbar.Menu.Zoom,false);Refresh();
        }
        public void ToggleOptions() => toolbar?.Toggle(RemixNavigationToolbar.Menu.Zoom);
        void OnEnable() => RefreshNow();
        public void RefreshNow() { displayedTenths=-1;displayedExact=-1;Refresh(); }
        void Update() => Refresh();
        public void Refresh()
        {
            if(manager==null) return;
            var zoom=manager.zoom;float seconds=zoom.VisibleSeconds;
            int tenths=Mathf.RoundToInt(seconds*10);
            int state=manager.IsEditing && Mathf.Abs(zoom.TargetSeconds-seconds)>.01f?3:zoom.AtMinimum?1:zoom.AtMaximum?2:0;
            if(tenths!=displayedTenths || state!=displayedState)
            {
                if(buttonLabel!=null) buttonLabel.text=(tenths*.1f).ToString("0.#",CultureInfo.InvariantCulture)+"s";
                if(workspaceRange!=null) workspaceRange.text=seconds.ToString("0.0",CultureInfo.InvariantCulture)+"s visible"+(state==3?" / queued":state==1?" / MIN":state==2?" / MAX":"");
                displayedTenths=tenths;displayedState=state;
            }
            if(displayedExact==seconds) return;
            Paint(detail,4,seconds);Paint(normal,8,seconds);Paint(wide,16,seconds);Paint(overview,32,seconds);displayedExact=seconds;
        }
        void Paint(Button button,float preset,float seconds)
        {
            if(button!=null && button.targetGraphic!=null) button.targetGraphic.color=Mathf.Abs(preset-seconds)<=.025f?selectedColor:idleColor;
        }
    }
}
