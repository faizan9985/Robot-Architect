using System;
using UnityEngine;

namespace RemixTable
{
    /// <summary>Who caused a mix change. Remote changes come from the network and must not be sent back.</summary>
    public enum MixChangeSource
    {
        Local,
        Remote,
    }

    public enum MixField
    {
        Muted,
        Soloed,
        Gain,
        Filter,
        Reverb,
        Holder,
        /// <summary>Global field (StemIndex is -1).</summary>
        Drop,
    }

    public enum DropPhase
    {
        Idle,
        Build,
        Hit,
        Release,
    }

    /// <summary>Holder ids for <see cref="MixState.TryBeginHold"/>. Networking will hand out ids from 2 upwards per player.</summary>
    public static class HolderIds
    {
        public const int None = 0;
        public const int LocalPlayer = 1;
    }

    /// <summary>Payload of <see cref="MixState.Changed"/>. A struct, so raising it never allocates.</summary>
    public readonly struct MixChange
    {
        public readonly int StemIndex;
        public readonly MixField Field;
        public readonly MixChangeSource Source;

        public MixChange(int stemIndex, MixField field, MixChangeSource source)
        {
            StemIndex = stemIndex;
            Field = field;
            Source = source;
        }

        public bool IsGlobal => StemIndex < 0;

        public override string ToString() => $"{Field} (stem {StemIndex}, {Source})";
    }

    /// <summary>The mix settings of one stem. Field names are lower-case so JsonUtility snapshots stay compact.</summary>
    [Serializable]
    public struct StemMix
    {
        public bool muted;
        public bool soloed;
        [Range(0f, 1f)] public float gain01;
        [Range(-1f, 1f)] public float filter;
        [Range(0f, 1f)] public float reverb01;
        public int holderId;

        public static StemMix Default => new StemMix { gain01 = 1f };

        public bool IsHeld => holderId != HolderIds.None;
    }

    /// <summary>The drop timeline, in song seconds (see <see cref="MusicClock.SongTime"/>), so it is the same on
    /// every client once their clocks agree.</summary>
    [Serializable]
    public struct DropState
    {
        public DropPhase phase;
        public double buildStartSongTime;
        public double hitSongTime;

        public static DropState Idle => default;

        public bool IsActive => phase != DropPhase.Idle;

        public DropState WithPhase(DropPhase newPhase)
        {
            DropState copy = this;
            copy.phase = newPhase;
            return copy;
        }

        public bool SameAs(in DropState other) =>
            phase == other.phase && buildStartSongTime == other.buildStartSongTime && hitSongTime == other.hitSongTime;
    }

    /// <summary>Serialisable copy of the whole mix (JsonUtility-friendly) for networking and record-a-take.</summary>
    [Serializable]
    public sealed class MixSnapshot
    {
        public int version;
        public StemMix[] stems = Array.Empty<StemMix>();
        public DropState drop;
    }

    /// <summary>
    /// The single source of truth for the mix. Every input (desktop, hands, network) changes the mix only through
    /// these methods; the audio engine and visuals only read it.
    /// Rules:
    /// - Every real change bumps <see cref="Version"/> and raises <see cref="Changed"/> exactly once per field.
    ///   Writing the same value again does nothing.
    /// - Solo: while any stem is soloed, the others duck to <see cref="MixTuning.soloDuckGain"/>.
    /// - A soloed stem plays even if it is muted (you hear the orb you hold to your ear); it is still muted afterwards.
    /// - Muting a soloed stem clears its solo, so the other stems come back.
    /// - Holding: whoever holds a stem keeps it until they let go; a second holder cannot take it.
    /// </summary>
    public sealed class MixState
    {
        /// <summary>Continuous values closer than this to the current value are not a real change.</summary>
        public const float ValueEpsilon = 1e-4f;

        readonly StemMix[] stems;
        DropState drop;
        int soloCount;

        public event Action<MixChange> Changed;

        public MixState(int stemCount)
        {
            if (stemCount <= 0) throw new ArgumentOutOfRangeException(nameof(stemCount), "A mix needs at least one stem.");
            stems = new StemMix[stemCount];
            for (int i = 0; i < stemCount; i++) stems[i] = StemMix.Default;
        }

        public int StemCount => stems.Length;
        public int Version { get; private set; }
        public bool AnySoloed => soloCount > 0;
        public int SoloCount => soloCount;
        public DropState Drop => drop;
        public DropPhase DropPhase => drop.phase;

        public StemMix GetStem(int stem) => stems[CheckIndex(stem)];
        public bool IsMuted(int stem) => stems[CheckIndex(stem)].muted;
        public bool IsSoloed(int stem) => stems[CheckIndex(stem)].soloed;
        public float GetGain(int stem) => stems[CheckIndex(stem)].gain01;
        public float GetFilter(int stem) => stems[CheckIndex(stem)].filter;
        public float GetReverb(int stem) => stems[CheckIndex(stem)].reverb01;
        public int GetHolder(int stem) => stems[CheckIndex(stem)].holderId;

        /// <summary>True when someone other than <paramref name="holderId"/> holds the stem.</summary>
        public bool IsHeldByOther(int stem, int holderId)
        {
            int holder = stems[CheckIndex(stem)].holderId;
            return holder != HolderIds.None && holder != holderId;
        }

        public bool SetMuted(int stem, bool muted, MixChangeSource source = MixChangeSource.Local)
        {
            ref StemMix s = ref stems[CheckIndex(stem)];
            if (s.muted == muted) return false;

            s.muted = muted;
            bool clearedSolo = false;
            if (muted && s.soloed)
            {
                s.soloed = false;
                soloCount--;
                clearedSolo = true;
            }

            Raise(stem, MixField.Muted, source);
            if (clearedSolo) Raise(stem, MixField.Soloed, source);
            return true;
        }

        public bool ToggleMute(int stem, MixChangeSource source = MixChangeSource.Local) =>
            SetMuted(stem, !IsMuted(stem), source);

        public bool SetSoloed(int stem, bool soloed, MixChangeSource source = MixChangeSource.Local)
        {
            ref StemMix s = ref stems[CheckIndex(stem)];
            if (s.soloed == soloed) return false;
            s.soloed = soloed;
            soloCount += soloed ? 1 : -1;
            Raise(stem, MixField.Soloed, source);
            return true;
        }

        public bool SetGain(int stem, float gain01, MixChangeSource source = MixChangeSource.Local)
        {
            ref StemMix s = ref stems[CheckIndex(stem)];
            if (float.IsNaN(gain01)) return false;
            gain01 = Mathf.Clamp01(gain01);
            if (!IsRealChange(s.gain01, gain01)) return false;
            s.gain01 = gain01;
            Raise(stem, MixField.Gain, source);
            return true;
        }

        /// <summary>-1 = full low-pass, 0 = neutral, +1 = full high-pass.</summary>
        public bool SetFilter(int stem, float filter, MixChangeSource source = MixChangeSource.Local)
        {
            ref StemMix s = ref stems[CheckIndex(stem)];
            if (float.IsNaN(filter)) return false;
            filter = Mathf.Clamp(filter, -1f, 1f);
            if (!IsRealChange(s.filter, filter)) return false;
            s.filter = filter;
            Raise(stem, MixField.Filter, source);
            return true;
        }

        public bool SetReverb(int stem, float reverb01, MixChangeSource source = MixChangeSource.Local)
        {
            ref StemMix s = ref stems[CheckIndex(stem)];
            if (float.IsNaN(reverb01)) return false;
            reverb01 = Mathf.Clamp01(reverb01);
            if (!IsRealChange(s.reverb01, reverb01)) return false;
            s.reverb01 = reverb01;
            Raise(stem, MixField.Reverb, source);
            return true;
        }

        /// <summary>Claims a stem. Returns true if <paramref name="holderId"/> now holds it (including if it already
        /// did); false if someone else holds it.</summary>
        public bool TryBeginHold(int stem, int holderId, MixChangeSource source = MixChangeSource.Local)
        {
            if (holderId == HolderIds.None)
                throw new ArgumentException("Holder id 0 means 'nobody'; use a real holder id.", nameof(holderId));
            ref StemMix s = ref stems[CheckIndex(stem)];
            if (s.holderId == holderId) return true;
            if (s.holderId != HolderIds.None) return false;
            s.holderId = holderId;
            Raise(stem, MixField.Holder, source);
            return true;
        }

        /// <summary>Releases a stem, but only if <paramref name="holderId"/> is the one holding it.</summary>
        public bool EndHold(int stem, int holderId, MixChangeSource source = MixChangeSource.Local)
        {
            ref StemMix s = ref stems[CheckIndex(stem)];
            if (holderId == HolderIds.None || s.holderId != holderId) return false;
            s.holderId = HolderIds.None;
            Raise(stem, MixField.Holder, source);
            return true;
        }

        /// <summary>Releases everything a holder had (for example when a remote player leaves). Returns the count.</summary>
        public int ReleaseAllHeldBy(int holderId, MixChangeSource source = MixChangeSource.Local)
        {
            int released = 0;
            for (int i = 0; i < stems.Length; i++)
                if (EndHold(i, holderId, source)) released++;
            return released;
        }

        public bool SetDrop(DropState state, MixChangeSource source = MixChangeSource.Local)
        {
            if (drop.SameAs(state)) return false;
            drop = state;
            Raise(-1, MixField.Drop, source);
            return true;
        }

        /// <summary>Back to neutral: unmuted, unsoloed, full gain, no filter or reverb. Holders are kept.</summary>
        public void ResetStems(MixChangeSource source = MixChangeSource.Local)
        {
            for (int i = 0; i < stems.Length; i++)
            {
                SetSoloed(i, false, source);
                SetMuted(i, false, source);
                SetGain(i, 1f, source);
                SetFilter(i, 0f, source);
                SetReverb(i, 0f, source);
            }
        }

        /// <summary>Copies the mix. Pass an existing snapshot to reuse its arrays (no allocation).</summary>
        public MixSnapshot CaptureSnapshot(MixSnapshot into = null)
        {
            into ??= new MixSnapshot();
            if (into.stems == null || into.stems.Length != stems.Length) into.stems = new StemMix[stems.Length];
            Array.Copy(stems, into.stems, stems.Length);
            into.drop = drop;
            into.version = Version;
            return into;
        }

        /// <summary>Makes this mix equal to the snapshot, raising one event per field that actually differs.
        /// Returns the number of changes. The snapshot is taken as-is (the mute/solo rules were applied by whoever
        /// produced it).</summary>
        public int ApplySnapshot(MixSnapshot snapshot, MixChangeSource source = MixChangeSource.Remote)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.stems == null || snapshot.stems.Length != stems.Length)
                throw new ArgumentException($"Snapshot has {snapshot.stems?.Length ?? 0} stems, mix has {stems.Length}.", nameof(snapshot));

            int changes = 0;
            for (int i = 0; i < stems.Length; i++)
            {
                StemMix target = snapshot.stems[i];
                ref StemMix s = ref stems[i];

                if (s.muted != target.muted)
                {
                    s.muted = target.muted;
                    Raise(i, MixField.Muted, source);
                    changes++;
                }
                if (s.soloed != target.soloed)
                {
                    s.soloed = target.soloed;
                    soloCount += target.soloed ? 1 : -1;
                    Raise(i, MixField.Soloed, source);
                    changes++;
                }
                float gain = SanitizeOr(target.gain01, 0f, 1f, s.gain01);
                if (s.gain01 != gain)
                {
                    s.gain01 = gain;
                    Raise(i, MixField.Gain, source);
                    changes++;
                }
                float filter = SanitizeOr(target.filter, -1f, 1f, s.filter);
                if (s.filter != filter)
                {
                    s.filter = filter;
                    Raise(i, MixField.Filter, source);
                    changes++;
                }
                float reverb = SanitizeOr(target.reverb01, 0f, 1f, s.reverb01);
                if (s.reverb01 != reverb)
                {
                    s.reverb01 = reverb;
                    Raise(i, MixField.Reverb, source);
                    changes++;
                }
                if (s.holderId != target.holderId)
                {
                    s.holderId = target.holderId;
                    Raise(i, MixField.Holder, source);
                    changes++;
                }
            }

            if (SetDrop(snapshot.drop, source)) changes++;
            return changes;
        }

        void Raise(int stem, MixField field, MixChangeSource source)
        {
            Version++;
            Changed?.Invoke(new MixChange(stem, field, source));
        }

        int CheckIndex(int stem)
        {
            if ((uint)stem >= (uint)stems.Length)
                throw new ArgumentOutOfRangeException(nameof(stem), $"Stem {stem} is out of range (0..{stems.Length - 1}).");
            return stem;
        }

        static bool IsRealChange(float current, float next)
        {
            if (current == next) return false;
            // Always allow landing exactly on an end or the neutral point, even from very close by.
            if (next == 0f || next == 1f || next == -1f) return true;
            return Mathf.Abs(next - current) >= ValueEpsilon;
        }

        static float SanitizeOr(float value, float min, float max, float fallback) =>
            float.IsNaN(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
