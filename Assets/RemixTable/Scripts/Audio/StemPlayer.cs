using System;
using UnityEngine;

namespace RemixTable
{
    /// <summary>Result of the dev-only sync check (stem play positions compared with stem 0).</summary>
    public struct DriftReport
    {
        /// <summary>False in release builds, where the check is compiled out.</summary>
        public bool Supported;
        /// <summary>True once at least one clean measurement exists since the last (re)start.</summary>
        public bool Active;
        public int LastMaxSamples;
        public int WorstSamples;
        public int ToleranceSamples;
        public int Warnings;
        public bool InSync => !Active || LastMaxSamples <= ToleranceSamples;
    }

    /// <summary>
    /// Plays a <see cref="SongDefinition"/>: one looping AudioSource per stem, all started with PlayScheduled at the
    /// same dsp time so they stay sample-locked. Owns the song's <see cref="MixState"/> and <see cref="MusicClock"/>,
    /// and applies the mix every frame with smoothing. It only reads the mix; it never changes it.
    /// Never stops one stem on its own: mute and solo are gain ramps.
    /// Each stem's AudioSource sits on a child GameObject that follows its registered <see cref="StemOrb"/>.
    /// </summary>
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Remix Table/Stem Player")]
    public sealed class StemPlayer : MonoBehaviour
    {
        const double MinStartDelaySeconds = 0.2;
        const double ResumeDelaySeconds = 0.12;
        const double CheckGraceSeconds = 0.15;
        const double ClockResyncThresholdSeconds = 0.05;

        [SerializeField] SongDefinition song;
        [Tooltip("Optional. Without one, MixTuning defaults are used.")]
        [SerializeField] MixTuning tuning;
        [SerializeField] bool playOnStart = true;
        [Tooltip("How far ahead the stems are scheduled. At least 0.2 s so every source is ready.")]
        [SerializeField, Min(0.2f)] float startDelaySeconds = 0.3f;

        [Header("Spatial audio")]
        [Tooltip("Uses the project's spatializer (Meta XR Audio). Turn off if the plugin misbehaves in the Editor.")]
        [SerializeField] bool spatialize = true;
        [Tooltip("Off (Unity default): spatialize first, then the filters. On: filters first, then spatialize.")]
        [SerializeField] bool spatializePostEffects = false;
        [Tooltip("Custom rolloff is flat, so this only bounds the source range; loudness comes from MixMapping.")]
        [SerializeField, Min(1f)] float sourceMaxDistance = 20f;

        [Header("Effects")]
        [SerializeField] StemEffectsBackend effectsBackend = StemEffectsBackend.FilterComponents;

        [Header("Pause")]
        [SerializeField] bool pauseOnFocusLoss = true;
        [Tooltip("The Editor loses focus whenever you click another window, so focus pausing is off there by default.")]
        [SerializeField] bool handleFocusInEditor = false;

        [Header("Debug (Editor and development builds only)")]
        [SerializeField, Min(1)] int driftToleranceSamples = 256;
#pragma warning disable CS0414 // only read by the dev-build drift check
        [SerializeField, Min(0.05f)] float driftCheckIntervalSeconds = 0.5f;
#pragma warning restore CS0414

        [Flags]
        enum PauseReason
        {
            None = 0,
            User = 1,
            Application = 2,
            Focus = 4,
        }

        sealed class Stem
        {
            public StemDefinition definition;
            public AudioSource source;
            public IStemEffects effects;
            public RampedValue gain;
            public RampedValue filter;
            public RampedValue reverb;
            public Transform follow;
        }

        static AnimationCurve flatRolloff;

        Stem[] stems = Array.Empty<Stem>();
        bool ownsTuning;
        bool started;
        PauseReason pauseReasons;
        bool audioPaused;
        bool pauseFadePending;
        double pauseFadeZeroDsp = -1;
        bool fadeInPending;
        double fadeInAtDsp;
        int pausedSample;
        double pausedSongTime;
        double resyncCheckAtDsp = -1;
        RampedValue transportFade = new RampedValue(1f);

        DriftReport drift;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        double driftChecksFromDsp;
        float nextDriftCheckTime;
        bool driftWarned;
#endif

        /// <summary>Raised after a song is loaded and a new <see cref="Mix"/> and <see cref="Clock"/> exist. Rebind then.</summary>
        public event Action SongLoaded;
        /// <summary>True when playback pauses (user, app pause or focus loss), false when it resumes.</summary>
        public event Action<bool> PausedChanged;

        public SongDefinition Song => song;
        public MixTuning Tuning => tuning;
        public MixState Mix { get; private set; }
        public MusicClock Clock { get; private set; }
        public bool IsLoaded => stems.Length > 0 && Mix != null;
        public int StemCount => stems.Length;
        public bool IsStarted => started;
        public bool IsPaused => pauseReasons != PauseReason.None;
        public bool IsUserPaused => (pauseReasons & PauseReason.User) != 0;
        public bool IsPlaying => started && !IsPaused;
        public DriftReport Drift => drift;

        /// <summary>Dev toggle: use the spatializer plugin or not. Applies to all stems immediately.</summary>
        public bool Spatialize
        {
            get => spatialize;
            set
            {
                spatialize = value;
                for (int i = 0; i < stems.Length; i++) stems[i].source.spatialize = value;
            }
        }

        public AudioSource GetSource(int stem) => stems[stem].source;
        public StemDefinition GetDefinition(int stem) => stems[stem].definition;
        /// <summary>The smoothed gain being played (after mute, solo, ducking and the drop bump; before headroom).</summary>
        public float GetAppliedGain(int stem) => (uint)stem < (uint)stems.Length ? stems[stem].gain.Current : 0f;
        /// <summary>The smoothed filter being played (includes the drop sweep).</summary>
        public float GetAppliedFilter(int stem) => (uint)stem < (uint)stems.Length ? stems[stem].filter.Current : 0f;
        public float GetAppliedReverb(int stem) => (uint)stem < (uint)stems.Length ? stems[stem].reverb.Current : 0f;

        void Awake()
        {
            if (tuning == null)
            {
                tuning = MixTuning.CreateRuntimeDefault();
                ownsTuning = true;
            }

            drift.Supported = Application.isEditor || Debug.isDebugBuild;
            drift.ToleranceSamples = driftToleranceSamples;

            if (song != null) LoadSong(song);
        }

        void Start()
        {
            if (playOnStart && IsLoaded) Play();
        }

        void OnEnable() => AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
        void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;

        void OnDestroy()
        {
            TearDownStems();
            if (ownsTuning && tuning != null) Destroy(tuning);
        }

        /// <summary>Loads a song: builds one AudioSource per stem and a fresh MixState and MusicClock.
        /// Does not start playback. Returns false if the song can't play.</summary>
        public bool LoadSong(SongDefinition newSong)
        {
            if (newSong == null) throw new ArgumentNullException(nameof(newSong));

            if (!newSong.TryValidate(out string errors, out string warnings))
            {
                RemixLog.Error($"Song '{newSong.Title}' can't play:\n{errors}", newSong);
                return false;
            }
            if (warnings.Length > 0) RemixLog.Warn($"Song '{newSong.Title}':\n{warnings}", newSong);

            TearDownStems();
            song = newSong;

            int count = newSong.StemCount;
            stems = new Stem[count];
            for (int i = 0; i < count; i++) stems[i] = CreateStem(i, newSong.GetStem(i));

            Mix = new MixState(count);
            Clock = new MusicClock(newSong.ComputeTiming(), LocalDspTimeSource.Instance);
            started = false;
            pauseReasons = PauseReason.None;
            audioPaused = false;
            pauseFadePending = false;
            fadeInPending = false;
            transportFade.Snap(1f);

            SongLoaded?.Invoke();
            return true;
        }

        /// <summary>Starts every stem from the top, scheduled together.</summary>
        public void Play()
        {
            if (!IsLoaded)
            {
                RemixLog.Warn("Play called with no song loaded.", this);
                return;
            }

            bool wasPaused = IsPaused;
            pauseReasons = PauseReason.None;
            audioPaused = false;
            pauseFadePending = false;
            fadeInPending = false;

            double startDsp = AudioSettings.dspTime + Math.Max(MinStartDelaySeconds, startDelaySeconds);
            ScheduleAll(startDsp, 0);
            Clock.StartAt(startDsp, 0.0);
            started = true;
            transportFade.Snap(1f);
            UpdateMix(0f, true);
            ArmChecks(startDsp);

            if (wasPaused) PausedChanged?.Invoke(false);
        }

        /// <summary>Stops everything (end of a session or before switching songs). Not for muting a stem.</summary>
        public void StopAll()
        {
            for (int i = 0; i < stems.Length; i++) stems[i].source.Stop();
            Clock?.Stop();
            started = false;
            audioPaused = false;
            pauseFadePending = false;
            fadeInPending = false;
            pauseReasons = PauseReason.None;
        }

        public void Pause() => AddPauseReason(PauseReason.User, false);
        public void Resume() => RemovePauseReason(PauseReason.User);

        public void TogglePause()
        {
            if (IsUserPaused) Resume();
            else Pause();
        }

        /// <summary>Makes the stem's AudioSource follow <paramref name="target"/> (normally the orb).</summary>
        public void RegisterOrb(int stem, Transform target)
        {
            if ((uint)stem >= (uint)stems.Length || target == null) return;
            stems[stem].follow = target;
            stems[stem].source.transform.position = target.position;
        }

        public void UnregisterOrb(int stem, Transform target)
        {
            if ((uint)stem >= (uint)stems.Length) return;
            if (stems[stem].follow == target) stems[stem].follow = null;
        }

        void Update()
        {
            if (!IsLoaded) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            double dsp = AudioSettings.dspTime;

            if (started)
            {
                UpdateTransport(dsp);
                UpdateMix(dt, false);
                CheckClockResync(dsp);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CheckDrift(dsp);
#endif
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < stems.Length; i++)
            {
                Stem s = stems[i];
                if (s.follow != null) s.source.transform.position = s.follow.position;
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) AddPauseReason(PauseReason.Application, true);
            else RemovePauseReason(PauseReason.Application);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!pauseOnFocusLoss) return;
            if (Application.isEditor && !handleFocusInEditor) return;
            if (hasFocus) RemovePauseReason(PauseReason.Focus);
            else AddPauseReason(PauseReason.Focus, true);
        }

        // ---------------------------------------------------------------- mix

        void UpdateMix(float dt, bool snap)
        {
            MixState mix = Mix;
            DropEffect drop = DropMath.Evaluate(mix.Drop, Clock.SongTime, Clock.Timing, tuning);
            bool anySoloed = mix.AnySoloed;
            float gainRamp = Mathf.Max(MixTuning.MinGainRampSeconds, tuning.gainRampSeconds);
            float filterRamp = drop.fastFilter ? tuning.dropHitFilterRampSeconds : tuning.filterRampSeconds;
            float bump = 1f + drop.gainBump;
            float fade = snap ? transportFade.Current : transportFade.Tick(dt);
            float headroom = tuning.masterHeadroom;

            for (int i = 0; i < stems.Length; i++)
            {
                Stem s = stems[i];
                StemMix m = mix.GetStem(i);
                float gain = MixMapping.EffectiveGain(m, anySoloed, tuning) * bump;
                float filter = Mathf.Lerp(m.filter, tuning.dropBuildFilterTarget, drop.filterAmount);
                float reverb = m.reverb01;

                if (snap)
                {
                    s.gain.Snap(gain);
                    s.filter.Snap(filter);
                    s.reverb.Snap(reverb);
                }
                else
                {
                    s.gain.SetTarget(gain, gainRamp);
                    s.filter.SetTarget(filter, filterRamp);
                    s.reverb.SetTarget(reverb, tuning.reverbRampSeconds);
                    s.gain.Tick(dt);
                    s.filter.Tick(dt);
                    s.reverb.Tick(dt);
                }

                s.source.volume = Mathf.Clamp01(s.gain.Current * headroom * fade);
                s.effects.SetFilter(s.filter.Current);
                s.effects.SetReverb(s.reverb.Current);
            }
        }

        // ---------------------------------------------------------------- transport

        void ScheduleAll(double dspTime, int sample)
        {
            for (int i = 0; i < stems.Length; i++)
            {
                AudioSource source = stems[i].source;
                source.Stop();
                source.timeSamples = Mathf.Clamp(sample, 0, Mathf.Max(0, source.clip.samples - 1));
                source.PlayScheduled(dspTime);
            }
        }

        void AddPauseReason(PauseReason reason, bool immediate)
        {
            bool wasPaused = IsPaused;
            pauseReasons |= reason;
            if (!started) return;

            if (!audioPaused)
            {
                if (immediate) PauseAudioNow();
                else if (!pauseFadePending)
                {
                    pauseFadePending = true;
                    pauseFadeZeroDsp = -1;
                    fadeInPending = false;
                    transportFade.SetTarget(0f, tuning.transportFadeSeconds);
                }
            }

            if (!wasPaused) PausedChanged?.Invoke(true);
        }

        void RemovePauseReason(PauseReason reason)
        {
            if ((pauseReasons & reason) == 0) return;
            pauseReasons &= ~reason;
            if (IsPaused || !started) return;

            if (audioPaused) ResumeAudioNow();
            else if (pauseFadePending)
            {
                pauseFadePending = false;
                transportFade.SetTarget(1f, tuning.transportFadeSeconds);
            }

            PausedChanged?.Invoke(false);
        }

        void UpdateTransport(double dsp)
        {
            if (fadeInPending && dsp >= fadeInAtDsp)
            {
                fadeInPending = false;
                transportFade.SetTarget(1f, tuning.transportFadeSeconds);
            }

            if (pauseFadePending && transportFade.Current <= 0f)
            {
                // Wait until the mixer has rendered at least one buffer at zero volume, then pause.
                if (pauseFadeZeroDsp < 0) pauseFadeZeroDsp = dsp;
                else if (dsp > pauseFadeZeroDsp) PauseAudioNow();
            }
        }

        void PauseAudioNow()
        {
            pauseFadePending = false;
            fadeInPending = false;
            pausedSample = stems[0].source.timeSamples;
            pausedSongTime = Clock.SongTime;
            for (int i = 0; i < stems.Length; i++) stems[i].source.Pause();
            Clock.Pause();
            audioPaused = true;
            transportFade.Snap(0f);
            drift.Active = false;
        }

        /// <summary>Resumes by re-scheduling every stem at the same sample and dsp time. Pause/UnPause per source
        /// isn't atomic, so it could leave the stems a buffer apart; rescheduling can't.</summary>
        void ResumeAudioNow()
        {
            AudioClip clip = stems[0].source.clip;
            int sample = Mathf.Clamp(pausedSample, 0, Mathf.Max(0, clip.samples - 1));
            double resumeDsp = AudioSettings.dspTime + ResumeDelaySeconds;
            ScheduleAll(resumeDsp, sample);
            Clock.StartAt(resumeDsp, Clock.Timing.ResumeSongTime(pausedSongTime, sample / (double)clip.frequency));

            audioPaused = false;
            transportFade.Snap(0f);
            fadeInPending = true;
            fadeInAtDsp = resumeDsp;
            ArmChecks(resumeDsp);
        }

        void HandleAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!started || !IsLoaded || audioPaused) return;
            // Unity may stop sources when the output device changes; restart them together from the clock position.
            AudioClip clip = stems[0].source.clip;
            pausedSongTime = Clock.SongTime;
            pausedSample = (int)(Clock.LoopTime * clip.frequency) % Mathf.Max(1, clip.samples);
            RemixLog.Info($"Audio configuration changed (device changed: {deviceWasChanged}). Rescheduling stems.", this);
            ResumeAudioNow();
        }

        void ArmChecks(double startDsp)
        {
            resyncCheckAtDsp = startDsp + CheckGraceSeconds;
            drift.Active = false;
            drift.LastMaxSamples = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            driftChecksFromDsp = startDsp + CheckGraceSeconds;
            driftWarned = false;
            nextDriftCheckTime = 0f;
#endif
        }

        /// <summary>Once after each (re)start, re-anchors the clock on stem 0's real play position, in case the
        /// audio started somewhere other than where we asked.</summary>
        void CheckClockResync(double dsp)
        {
            if (resyncCheckAtDsp < 0 || dsp < resyncCheckAtDsp || audioPaused) return;
            resyncCheckAtDsp = -1;

            AudioSource source = stems[0].source;
            if (!source.isPlaying || source.clip == null) return;
            double audioLoopTime = source.timeSamples / (double)source.clip.frequency;
            double error = SampleMath.WrapSigned(Clock.LoopTime - audioLoopTime, Clock.Timing.LoopSeconds);
            if (Math.Abs(error) <= ClockResyncThresholdSeconds) return;

            Clock.AdjustStartTime(error);
            RemixLog.Warn($"Music clock was {error * 1000.0:F0} ms off the audio; re-anchored.", this);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void CheckDrift(double dsp)
        {
            if (audioPaused || stems.Length < 2 || dsp < driftChecksFromDsp) return;
            if (Time.unscaledTime < nextDriftCheckTime) return;

            AudioSource reference = stems[0].source;
            int length = reference.clip.samples;
            int before = reference.timeSamples;
            int max = 0;
            int stoppedStem = -1;
            for (int i = 1; i < stems.Length; i++)
            {
                AudioSource source = stems[i].source;
                if (!source.isPlaying) stoppedStem = i;
                int distance = SampleMath.CircularDistance(source.timeSamples, before, length);
                if (distance > max) max = distance;
            }

            // The mixer may have advanced while we read; retry next frame rather than report a false drift.
            if (reference.timeSamples != before) return;
            nextDriftCheckTime = Time.unscaledTime + driftCheckIntervalSeconds;

            drift.Active = true;
            drift.LastMaxSamples = max;
            if (max > drift.WorstSamples) drift.WorstSamples = max;
            drift.ToleranceSamples = driftToleranceSamples;

            bool bad = max > driftToleranceSamples || (stoppedStem >= 0 && reference.isPlaying);
            if (bad && !driftWarned)
            {
                driftWarned = true;
                drift.Warnings++;
                if (stoppedStem >= 0)
                    RemixLog.Warn($"Stem {stoppedStem} stopped playing while stem 0 plays (voice limit?).", this);
                else
                    RemixLog.Warn($"Stems drifted: {max} samples from stem 0 (tolerance {driftToleranceSamples}).", this);
            }
            else if (!bad)
            {
                driftWarned = false;
            }
        }
#endif

        // ---------------------------------------------------------------- setup

        Stem CreateStem(int index, StemDefinition definition)
        {
            var go = new GameObject($"Stem {index} {definition.Name}");
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.clip = definition.Clip;
            source.volume = 0f;
            source.priority = 0;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.spatialBlend = 1f;
            source.spatialize = spatialize;
            source.spatializePostEffects = spatializePostEffects;
            // Distance loudness comes from MixMapping, so neutralise the built-in rolloff.
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FlatRolloff());
            source.minDistance = 1f;
            source.maxDistance = sourceMaxDistance;

            if (definition.Clip.loadState == AudioDataLoadState.Unloaded) definition.Clip.LoadAudioData();

            return new Stem
            {
                definition = definition,
                source = source,
                effects = CreateEffects(go),
                gain = new RampedValue(0f),
                filter = new RampedValue(0f),
                reverb = new RampedValue(0f),
            };
        }

        IStemEffects CreateEffects(GameObject host)
        {
            switch (effectsBackend)
            {
                case StemEffectsBackend.FilterComponents:
                    return new FilterComponentEffects(host, tuning);
                default:
                    throw new NotSupportedException($"Effects backend {effectsBackend} is not implemented.");
            }
        }

        void TearDownStems()
        {
            for (int i = 0; i < stems.Length; i++)
            {
                Stem s = stems[i];
                s.effects?.Dispose();
                if (s.source != null)
                {
                    s.source.Stop();
                    Destroy(s.source.gameObject);
                }
            }
            stems = Array.Empty<Stem>();
            Mix = null;
            Clock = null;
        }

        static AnimationCurve FlatRolloff()
        {
            flatRolloff ??= new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));
            return flatRolloff;
        }

        internal void EditorConfigure(SongDefinition songDefinition, MixTuning mixTuning)
        {
            song = songDefinition;
            tuning = mixTuning;
        }
    }
}
