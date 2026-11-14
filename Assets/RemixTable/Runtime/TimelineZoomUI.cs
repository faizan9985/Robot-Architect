using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    /// <summary>UI adapter only: preset buttons and gesture input share manager.zoom.</summary>
    public sealed class TimelineZoomUI : MonoBehaviour
    {
        public RemixTableManager manager;
        public TMP_Text menuRange, workspaceRange;
        public GameObject options, instructions;
        public Button expand, detail, normal, wide, overview;
        int displayedHalfSeconds=-1, displayedState=-1;
        float nextRefresh;
        public void Detail() => manager.zoom.Request(4);
        public void Normal() => manager.zoom.Request(8);
        public void Wide() => manager.zoom.Request(16);
        public void Overview() => manager.zoom.Request(32);
        public void ToggleOptions()
        {
            bool show=!options.activeSelf;
            options.SetActive(show);instructions.SetActive(!show);
        }
        void OnEnable() { displayedHalfSeconds=-1;nextRefresh=0; }
        void Update()
        {
            if(Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.2f;
            var zoom=manager.zoom;
            int halfSeconds=Mathf.RoundToInt(zoom.VisibleSeconds*2);
            int state=manager.IsEditing && Mathf.Abs(zoom.TargetSeconds-zoom.VisibleSeconds)>.01f ? 3 : zoom.AtMinimum?1:zoom.AtMaximum?2:0;
            if(halfSeconds==displayedHalfSeconds && state==displayedState) return;
            float seconds=halfSeconds*.5f;
            string format=state==3?"{0:1}s visible / queued":state==1?"{0:1}s visible / MIN":state==2?"{0:1}s visible / MAX":"{0:1}s visible";
            workspaceRange.SetText(format,seconds);
            menuRange.SetText(state==3?"TIMELINE ZOOM  +\n{0:1}s visible / queued":state==1?"TIMELINE ZOOM  +\n{0:1}s visible / MIN":state==2?"TIMELINE ZOOM  +\n{0:1}s visible / MAX":"TIMELINE ZOOM  +\n{0:1}s visible",seconds);
            displayedHalfSeconds=halfSeconds;displayedState=state;
        }
    }
}
