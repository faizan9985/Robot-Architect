using UnityEngine;
namespace RemixTable
{
    public class RemixTableManager : MonoBehaviour
    {
        public RemixAudioManager audioManager; public RemixDesign design;
        public SongLibraryManager library;
        public RibbonInteractionManager interactions;
        public RibbonView[] ribbons;
        public Transform workspace;
        public PlaybackClock clock = new PlaybackClock();
        public AutomationStore automation = new AutomationStore();
        public bool anchored;
        void OnEnable() { library.SongChanged += ChangeSong; ChangeSong(library.Current); }
        void OnDisable() { library.SongChanged -= ChangeSong; }
        void ChangeSong(SongMetadata song)
        {
            interactions.Cancel(); clock.Reset(song.duration);
            foreach (var r in ribbons) r.ResetPhase();
            if (audioManager != null) audioManager.Load(song);
        }
        void Update()
        {
            if (!anchored && Camera.main != null && Time.timeSinceLevelLoad > 1)
            {
                var cam = Camera.main.transform;
                var pose=design.SeatedPose(cam.position,cam.forward);
                workspace.SetPositionAndRotation(pose.position,pose.rotation);
                anchored = true;
            }
            // The audio manager owns the DSP timeline.
        }
    }
}

