using UnityEngine;

namespace RemixTable.Dev
{
    /// <summary>
    /// Hands-free audio test for headset builds (and the desktop sandbox): once playback is running it walks
    /// through every effect on a bar schedule, writing to MixState like any other input, and logs each step with
    /// the [RemixTable] prefix so the tester knows what they should be hearing (watch with
    /// <c>adb logcat -s Unity</c>). It holds every stem while it runs, so orb positions don't fight it, and
    /// resets the mix and lets go when it finishes. Dev only: never leave it in a release scene.
    /// Schedule (bars from the first downbeat after it starts, 2 s each at 120 BPM):
    ///  0-2 neutral · 2-4 low-pass sweep · 4-6 high-pass sweep · 6-8 reverb up · 8 drop (hit at 10) ·
    ///  12.. mute one stem per bar · then solo stem 0 for 2 bars · done.
    /// </summary>
    [AddComponentMenu("Remix Table/Dev/Audio Self Test")]
    public sealed class AudioSelfTest : MonoBehaviour
    {
        const int SelfTestHolderId = 99;

        enum Step { Neutral, LowPass, HighPass, Reverb, Drop, MuteWalk, Solo, Done }

        [SerializeField] StemPlayer player;
        [SerializeField] DropController drop;
        [SerializeField] bool runOnStart = true;
        [Tooltip("Seconds of normal playback before the test starts.")]
        [SerializeField, Min(0f)] float startAfterSeconds = 4f;

        bool running;
        bool waiting;
        float waitUntil;
        double startSongTime;
        Step step;
        int lastMutedStem = -1;

        public bool IsRunning => running;

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<StemPlayer>();
            if (drop == null) drop = FindFirstObjectByType<DropController>();
            if (runOnStart) Run();
        }

        /// <summary>Starts (or restarts) the test after <see cref="startAfterSeconds"/>.</summary>
        public void Run()
        {
            Finish(false);
            waiting = true;
            waitUntil = Time.unscaledTime + startAfterSeconds;
        }

        void Update()
        {
            if (player == null || !player.IsLoaded) return;
            MixState mix = player.Mix;
            MusicClock clock = player.Clock;

            if (waiting)
            {
                if (!player.IsPlaying || !clock.HasReachedStart || Time.unscaledTime < waitUntil) return;
                waiting = false;
                running = true;
                startSongTime = clock.NextDownbeatSongTime(true);
                for (int i = 0; i < mix.StemCount; i++) mix.TryBeginHold(i, SelfTestHolderId);
                step = (Step)(-1);
                RemixLog.Info("Self-test: starting on the next downbeat. Every stem should play together, no flams.", this);
            }

            if (!running) return;
            if (player.IsPaused)
            {
                RemixLog.Info("Self-test: aborted (paused).", this);
                Finish(true);
                return;
            }

            double bars = (clock.SongTime - startSongTime) / clock.Timing.SecondsPerBar;
            if (bars < 0) return;
            int stems = mix.StemCount;
            Step now = bars < 2 ? Step.Neutral
                : bars < 4 ? Step.LowPass
                : bars < 6 ? Step.HighPass
                : bars < 8 ? Step.Reverb
                : bars < 12 ? Step.Drop
                : bars < 12 + stems ? Step.MuteWalk
                : bars < 14 + stems ? Step.Solo
                : Step.Done;

            if (now != step) EnterStep(now, mix);

            float within = (float)(bars - System.Math.Floor(bars / 2.0) * 2.0) / 2f; // 0..1 across each 2-bar step
            switch (step)
            {
                case Step.LowPass:
                    for (int i = 0; i < stems; i++) mix.SetFilter(i, -within);
                    break;
                case Step.HighPass:
                    for (int i = 0; i < stems; i++) mix.SetFilter(i, within);
                    break;
                case Step.Reverb:
                    for (int i = 0; i < stems; i++) mix.SetReverb(i, within);
                    break;
                case Step.MuteWalk:
                    int muted = Mathf.Clamp((int)(bars - 12), 0, stems - 1);
                    if (muted != lastMutedStem)
                    {
                        if (lastMutedStem >= 0) mix.SetMuted(lastMutedStem, false);
                        mix.SetMuted(muted, true);
                        lastMutedStem = muted;
                        RemixLog.Info($"Self-test: stem {muted} ({player.GetDefinition(muted).Name}) muted for one bar.", this);
                    }
                    break;
            }
        }

        void EnterStep(Step next, MixState mix)
        {
            step = next;
            int stems = mix.StemCount;
            switch (next)
            {
                case Step.Neutral:
                    RemixLog.Info("Self-test: 2 bars neutral (full, dry mix).", this);
                    break;
                case Step.LowPass:
                    RemixLog.Info("Self-test: low-pass sweep on every stem over 2 bars (getting 'underwater').", this);
                    break;
                case Step.HighPass:
                    for (int i = 0; i < stems; i++) mix.SetFilter(i, 0f);
                    RemixLog.Info("Self-test: high-pass sweep over 2 bars (getting thin, bass disappears).", this);
                    break;
                case Step.Reverb:
                    for (int i = 0; i < stems; i++) mix.SetFilter(i, 0f);
                    RemixLog.Info("Self-test: reverb rising over 2 bars.", this);
                    break;
                case Step.Drop:
                    for (int i = 0; i < stems; i++) mix.SetReverb(i, 0f);
                    bool started = drop != null && drop.TriggerDrop();
                    RemixLog.Info(started
                        ? "Self-test: drop. Riser + filters closing for 2 bars, then the hit on the downbeat."
                        : "Self-test: drop skipped (no DropController or it couldn't start).", this);
                    break;
                case Step.MuteWalk:
                    RemixLog.Info("Self-test: muting one stem per bar (fades, no clicks).", this);
                    break;
                case Step.Solo:
                    if (lastMutedStem >= 0) mix.SetMuted(lastMutedStem, false);
                    lastMutedStem = -1;
                    mix.SetSoloed(0, true);
                    RemixLog.Info($"Self-test: stem 0 ({player.GetDefinition(0).Name}) soloed for 2 bars; the rest duck.", this);
                    break;
                case Step.Done:
                    RemixLog.Info("Self-test: finished. Mix reset; orbs are back in control.", this);
                    Finish(true);
                    break;
            }
        }

        void Finish(bool resetMix)
        {
            waiting = false;
            if (!running) return;
            running = false;
            lastMutedStem = -1;
            if (player == null || player.Mix == null) return;
            if (resetMix) player.Mix.ResetStems();
            player.Mix.ReleaseAllHeldBy(SelfTestHolderId);
        }

        void OnDisable() => Finish(true);
    }
}
