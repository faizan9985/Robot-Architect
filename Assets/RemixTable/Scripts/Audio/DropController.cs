using UnityEngine;
using UnityEngine.Events;

namespace RemixTable
{
    /// <summary>
    /// The drop: Idle → Build (every filter sweeps towards low-pass, riser plays) → Hit (on the next downbeat
    /// at least <see cref="MixTuning.dropBuildBars"/> bars away; filters snap back with a short gain bump) →
    /// Release (the bump fades) → Idle.
    /// <see cref="TriggerDrop"/> is the only entry point: the desktop key now, the desk-slam gesture later.
    /// The timeline is stored in <see cref="MixState.Drop"/>; <see cref="StemPlayer"/> renders the sweep and bump
    /// from it, and this component moves the phase along and raises the events.
    /// The riser and optional hit sample are scheduled sample-accurately; the events fire on the first frame
    /// after each phase starts.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [AddComponentMenu("Remix Table/Drop Controller")]
    public sealed class DropController : MonoBehaviour
    {
        const double ScheduleLeadSeconds = 0.05;

        [SerializeField] StemPlayer player;
        [Tooltip("Plays during the build and ends exactly on the hit. Non-spatial.")]
        [SerializeField] AudioClip riserClip;
        [SerializeField, Range(0f, 1f)] float riserVolume = 0.6f;
        [Tooltip("Optional one-shot that starts exactly on the hit.")]
        [SerializeField] AudioClip hitClip;
        [SerializeField, Range(0f, 1f)] float hitVolume = 0.8f;

        public UnityEvent OnBuildStarted = new UnityEvent();
        public UnityEvent OnHit = new UnityEvent();
        public UnityEvent OnReleased = new UnityEvent();

        AudioSource riserSource;
        AudioSource hitSource;
        MixState mix;
        DropPhase lastPhase;

        public StemPlayer Player => player;
        public DropPhase Phase => mix != null ? mix.DropPhase : DropPhase.Idle;

        public bool CanTrigger =>
            player != null && player.IsPlaying && mix != null && mix.DropPhase == DropPhase.Idle && player.Clock.HasReachedStart;

        /// <summary>0..1 during the build (1 at the hit), for VFX.</summary>
        public float BuildProgress01 =>
            mix != null && player != null && player.Clock != null && mix.DropPhase == DropPhase.Build
                ? DropMath.BuildProgress(mix.Drop, player.Clock.SongTime)
                : 0f;

        void Awake()
        {
            riserSource = CreateSource("Drop Riser");
            hitSource = CreateSource("Drop Hit");
        }

        void Start()
        {
            if (player == null) player = GetComponent<StemPlayer>();
            if (player == null) player = FindFirstObjectByType<StemPlayer>();
            if (player == null)
            {
                RemixLog.Warn("DropController has no StemPlayer.", this);
                enabled = false;
                return;
            }

            player.SongLoaded += Bind;
            player.PausedChanged += HandlePausedChanged;
            Bind();
        }

        void OnDestroy()
        {
            if (mix != null) mix.Changed -= HandleMixChanged;
            if (player != null)
            {
                player.SongLoaded -= Bind;
                player.PausedChanged -= HandlePausedChanged;
            }
        }

        /// <summary>Starts a drop. Ignored (returns false) while one is already running or nothing is playing.</summary>
        public bool TriggerDrop()
        {
            if (!CanTrigger) return false;

            MusicClock clock = player.Clock;
            SongTiming timing = clock.Timing;
            double now = clock.SongTime;
            double hit = DropMath.ComputeHitSongTime(now, timing, player.Tuning.dropBuildBars);

            mix.SetDrop(new DropState
            {
                phase = DropPhase.Build,
                buildStartSongTime = now,
                hitSongTime = hit,
            }, MixChangeSource.Local);

            if (riserClip != null && riserClip.frequency > 0)
            {
                double length = riserClip.samples / (double)riserClip.frequency;
                double startSong = hit - length;
                double earliest = now + ScheduleLeadSeconds;
                int offsetSamples = 0;
                if (startSong < earliest)
                {
                    // Not enough time for the whole riser: skip its start so it still ends on the hit.
                    offsetSamples = (int)((earliest - startSong) * riserClip.frequency);
                    startSong = earliest;
                }

                if (offsetSamples < riserClip.samples)
                {
                    riserSource.Stop();
                    riserSource.clip = riserClip;
                    riserSource.volume = riserVolume;
                    riserSource.timeSamples = offsetSamples;
                    riserSource.PlayScheduled(clock.SongTimeToSourceTime(startSong));
                    riserSource.SetScheduledEndTime(clock.SongTimeToSourceTime(hit));
                }
            }

            if (hitClip != null)
            {
                hitSource.Stop();
                hitSource.clip = hitClip;
                hitSource.volume = hitVolume;
                hitSource.PlayScheduled(clock.SongTimeToSourceTime(hit));
            }

            return true;
        }

        /// <summary>Aborts a running drop (also happens automatically on pause).</summary>
        public void CancelDrop()
        {
            if (riserSource != null) riserSource.Stop();
            if (hitSource != null) hitSource.Stop();
            if (mix != null && mix.DropPhase != DropPhase.Idle) mix.SetDrop(DropState.Idle, MixChangeSource.Local);
        }

        void Update()
        {
            if (mix == null || mix.DropPhase == DropPhase.Idle || !player.IsPlaying) return;

            DropPhase target = DropMath.PhaseAt(mix.Drop, player.Clock.SongTime, player.Clock.Timing, player.Tuning);
            // Step through each phase so no event is skipped after a long frame.
            int guard = 0;
            while (mix.DropPhase != target && guard++ < 4)
            {
                DropPhase next = DropMath.Next(mix.DropPhase);
                mix.SetDrop(next == DropPhase.Idle ? DropState.Idle : mix.Drop.WithPhase(next), MixChangeSource.Local);
            }
        }

        void Bind()
        {
            if (mix != null) mix.Changed -= HandleMixChanged;
            mix = player.Mix;
            if (mix != null) mix.Changed += HandleMixChanged;
            lastPhase = Phase;
        }

        void HandleMixChanged(MixChange change)
        {
            if (change.Field != MixField.Drop) return;
            DropPhase phase = mix.DropPhase;
            if (phase == lastPhase) return;
            lastPhase = phase;

            switch (phase)
            {
                case DropPhase.Build: OnBuildStarted.Invoke(); break;
                case DropPhase.Hit: OnHit.Invoke(); break;
                case DropPhase.Release: OnReleased.Invoke(); break;
            }
        }

        void HandlePausedChanged(bool paused)
        {
            if (paused) CancelDrop();
        }

        AudioSource CreateSource(string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.spatialize = false;
            source.priority = 1;
            return source;
        }

        internal void EditorConfigure(StemPlayer stemPlayer, AudioClip riser, AudioClip hit)
        {
            player = stemPlayer;
            riserClip = riser;
            hitClip = hit;
        }
    }
}
