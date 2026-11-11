using UnityEngine;
using UnityEngine.Events;

namespace RemixTable
{
    /// <summary>
    /// Raises beat and bar events from the <see cref="StemPlayer"/>'s <see cref="MusicClock"/> for VFX.
    /// Put it on the same GameObject as the StemPlayer.
    /// Events fire on the first frame at or after each beat. The main thread sees the audio clock in
    /// DSP-buffer steps, so expect up to about one buffer (~20 ms) of jitter: fine for visuals, never use it to
    /// time audio (use PlayScheduled for that).
    /// </summary>
    [DefaultExecutionOrder(5)]
    [AddComponentMenu("Remix Table/Music Clock Driver")]
    public sealed class MusicClockDriver : MonoBehaviour
    {
        [SerializeField] StemPlayer player;

        [Tooltip("Beat within the bar, 0-based (0 = downbeat).")]
        public UnityEvent<int> OnBeat = new UnityEvent<int>();
        [Tooltip("Bar within the loop, 0-based. Fires right after OnBeat(0).")]
        public UnityEvent<int> OnBar = new UnityEvent<int>();

        long lastBeat = long.MinValue;

        public StemPlayer Player => player;

        void Awake()
        {
            if (player == null) player = GetComponent<StemPlayer>();
            if (player == null) RemixLog.Warn("MusicClockDriver has no StemPlayer.", this);
        }

        void Update()
        {
            MusicClock clock = player != null ? player.Clock : null;
            if (clock == null || !clock.IsRunning || !clock.HasReachedStart) return;

            double songTime = clock.SongTime;
            long beat = clock.Timing.BeatIndex(songTime);
            if (beat == lastBeat) return;
            lastBeat = beat;

            int beatInBar = clock.Timing.BeatInBar(songTime);
            OnBeat.Invoke(beatInBar);
            if (beatInBar == 0) OnBar.Invoke(clock.Timing.BarInLoop(songTime));
        }

        internal void EditorConfigure(StemPlayer stemPlayer) => player = stemPlayer;
    }
}
