using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RemixTable.Tests
{
    public class MixStateTests
    {
        MixTuning tuning;

        [SetUp]
        public void SetUp() => tuning = ScriptableObject.CreateInstance<MixTuning>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(tuning);

        static List<MixChange> Record(MixState mix)
        {
            var changes = new List<MixChange>();
            mix.Changed += changes.Add;
            return changes;
        }

        float Effective(MixState mix, int stem) => MixMapping.EffectiveGain(mix.GetStem(stem), mix.AnySoloed, tuning);

        [Test]
        public void NewState_HasNeutralDefaults()
        {
            var mix = new MixState(3);
            Assert.AreEqual(3, mix.StemCount);
            Assert.AreEqual(0, mix.Version);
            Assert.AreEqual(DropPhase.Idle, mix.DropPhase);
            for (int i = 0; i < 3; i++)
            {
                StemMix s = mix.GetStem(i);
                Assert.IsFalse(s.muted);
                Assert.IsFalse(s.soloed);
                Assert.AreEqual(1f, s.gain01);
                Assert.AreEqual(0f, s.filter);
                Assert.AreEqual(0f, s.reverb01);
                Assert.AreEqual(HolderIds.None, s.holderId);
            }
        }

        [Test]
        public void Constructor_RejectsZeroStems()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MixState(0));
        }

        [Test]
        public void OutOfRangeStem_Throws()
        {
            var mix = new MixState(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => mix.SetGain(2, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => mix.GetStem(-1));
        }

        [Test]
        public void SetGain_FiresOncePerRealChange()
        {
            var mix = new MixState(2);
            var changes = Record(mix);

            Assert.IsTrue(mix.SetGain(0, 0.5f));
            Assert.IsFalse(mix.SetGain(0, 0.5f), "same value");
            Assert.IsFalse(mix.SetGain(0, 0.50001f), "below epsilon");

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(0, changes[0].StemIndex);
            Assert.AreEqual(MixField.Gain, changes[0].Field);
            Assert.AreEqual(MixChangeSource.Local, changes[0].Source);
            Assert.AreEqual(1, mix.Version);
        }

        [Test]
        public void Setters_ClampAndIgnoreNaN()
        {
            var mix = new MixState(1);
            Assert.IsFalse(mix.SetGain(0, 2f), "clamps to 1, which is already the value");
            mix.SetGain(0, -1f);
            Assert.AreEqual(0f, mix.GetGain(0));
            mix.SetFilter(0, 5f);
            Assert.AreEqual(1f, mix.GetFilter(0));
            mix.SetFilter(0, -5f);
            Assert.AreEqual(-1f, mix.GetFilter(0));
            mix.SetReverb(0, 3f);
            Assert.AreEqual(1f, mix.GetReverb(0));

            int version = mix.Version;
            Assert.IsFalse(mix.SetGain(0, float.NaN));
            Assert.IsFalse(mix.SetFilter(0, float.NaN));
            Assert.IsFalse(mix.SetReverb(0, float.NaN));
            Assert.AreEqual(version, mix.Version);
        }

        [Test]
        public void SetGain_CanLandExactlyOnTheEndsFromNearby()
        {
            var mix = new MixState(1);
            mix.SetGain(0, 0.99995f); // a real change from 1? no: below epsilon, ignored
            Assert.AreEqual(1f, mix.GetGain(0));
            mix.SetGain(0, 0.5f);
            mix.SetGain(0, 0.00005f);
            Assert.IsTrue(mix.SetGain(0, 0f), "landing exactly on 0 always counts");
            Assert.AreEqual(0f, mix.GetGain(0));
        }

        [Test]
        public void Version_CountsEveryChangeEvent()
        {
            var mix = new MixState(2);
            var changes = Record(mix);
            mix.SetGain(0, 0.2f);
            mix.SetFilter(1, -0.4f);
            mix.SetReverb(1, 0.3f);
            mix.SetMuted(0, true);
            mix.SetMuted(0, true);
            Assert.AreEqual(4, changes.Count);
            Assert.AreEqual(4, mix.Version);
        }

        [Test]
        public void Mute_ReportsStemFieldAndSource()
        {
            var mix = new MixState(2);
            var changes = Record(mix);
            mix.SetMuted(1, true, MixChangeSource.Remote);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(1, changes[0].StemIndex);
            Assert.AreEqual(MixField.Muted, changes[0].Field);
            Assert.AreEqual(MixChangeSource.Remote, changes[0].Source);
        }

        [Test]
        public void ToggleMute_TogglesBackAndForth()
        {
            var mix = new MixState(1);
            var changes = Record(mix);
            mix.ToggleMute(0);
            Assert.IsTrue(mix.IsMuted(0));
            mix.ToggleMute(0);
            Assert.IsFalse(mix.IsMuted(0));
            Assert.AreEqual(2, changes.Count);
        }

        [Test]
        public void MutedStem_IsSilent()
        {
            var mix = new MixState(2);
            mix.SetMuted(0, true);
            Assert.AreEqual(0f, Effective(mix, 0));
            Assert.AreEqual(1f, Effective(mix, 1));
        }

        [Test]
        public void Solo_DucksTheOtherStems()
        {
            var mix = new MixState(3);
            mix.SetGain(1, 0.8f);
            mix.SetSoloed(0, true);

            Assert.IsTrue(mix.AnySoloed);
            Assert.AreEqual(1f, Effective(mix, 0));
            Assert.AreEqual(0.8f * tuning.soloDuckGain, Effective(mix, 1), 1e-6f);
            Assert.AreEqual(tuning.soloDuckGain, Effective(mix, 2), 1e-6f);
        }

        [Test]
        public void Solo_SeveralSoloedStemsAllPlay()
        {
            var mix = new MixState(3);
            mix.SetSoloed(0, true);
            mix.SetSoloed(1, true);
            Assert.AreEqual(2, mix.SoloCount);
            Assert.AreEqual(1f, Effective(mix, 0));
            Assert.AreEqual(1f, Effective(mix, 1));
            Assert.AreEqual(tuning.soloDuckGain, Effective(mix, 2), 1e-6f);
        }

        [Test]
        public void Unsolo_RestoresTheOthers()
        {
            var mix = new MixState(2);
            mix.SetSoloed(0, true);
            mix.SetSoloed(0, false);
            Assert.IsFalse(mix.AnySoloed);
            Assert.AreEqual(1f, Effective(mix, 1));
        }

        [Test]
        public void MutingASoloedStem_ClearsItsSoloAndRestoresTheOthers()
        {
            var mix = new MixState(2);
            mix.SetSoloed(0, true);
            var changes = Record(mix);

            mix.SetMuted(0, true);

            Assert.IsTrue(mix.IsMuted(0));
            Assert.IsFalse(mix.IsSoloed(0));
            Assert.IsFalse(mix.AnySoloed);
            Assert.AreEqual(0f, Effective(mix, 0));
            Assert.AreEqual(1f, Effective(mix, 1));
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(MixField.Muted, changes[0].Field);
            Assert.AreEqual(MixField.Soloed, changes[1].Field);
        }

        [Test]
        public void SoloingAMutedStem_PlaysWhileSoloed_AndIsStillMutedAfter()
        {
            var mix = new MixState(2);
            mix.SetMuted(0, true);
            mix.SetSoloed(0, true);
            Assert.AreEqual(1f, Effective(mix, 0), "solo overrides mute while held at the ear");
            Assert.AreEqual(tuning.soloDuckGain, Effective(mix, 1), 1e-6f);

            mix.SetSoloed(0, false);
            Assert.IsTrue(mix.IsMuted(0));
            Assert.AreEqual(0f, Effective(mix, 0));
        }

        [Test]
        public void MutedStem_StaysSilentWhileAnotherIsSoloed()
        {
            var mix = new MixState(3);
            mix.SetMuted(1, true);
            mix.SetSoloed(0, true);
            Assert.AreEqual(0f, Effective(mix, 1));
        }

        [Test]
        public void Hold_SecondHolderCannotTakeAHeldStem()
        {
            var mix = new MixState(2);
            Assert.IsTrue(mix.TryBeginHold(0, 1));
            Assert.IsFalse(mix.TryBeginHold(0, 2));
            Assert.AreEqual(1, mix.GetHolder(0));
            Assert.IsTrue(mix.IsHeldByOther(0, 2));
            Assert.IsFalse(mix.IsHeldByOther(0, 1));

            Assert.IsFalse(mix.EndHold(0, 2), "only the holder can let go");
            Assert.AreEqual(1, mix.GetHolder(0));

            Assert.IsTrue(mix.EndHold(0, 1));
            Assert.IsTrue(mix.TryBeginHold(0, 2));
            Assert.AreEqual(2, mix.GetHolder(0));
        }

        [Test]
        public void Hold_SameHolderAgain_RaisesNoExtraEvent()
        {
            var mix = new MixState(1);
            var changes = Record(mix);
            mix.TryBeginHold(0, HolderIds.LocalPlayer);
            Assert.IsTrue(mix.TryBeginHold(0, HolderIds.LocalPlayer));
            Assert.AreEqual(1, changes.Count);
        }

        [Test]
        public void Hold_RejectsTheNoneHolderId()
        {
            var mix = new MixState(1);
            Assert.Throws<ArgumentException>(() => mix.TryBeginHold(0, HolderIds.None));
            Assert.IsFalse(mix.EndHold(0, HolderIds.None));
        }

        [Test]
        public void ReleaseAllHeldBy_ReleasesOnlyThatHolder()
        {
            var mix = new MixState(3);
            mix.TryBeginHold(0, 2);
            mix.TryBeginHold(1, 2);
            mix.TryBeginHold(2, 3);
            Assert.AreEqual(2, mix.ReleaseAllHeldBy(2));
            Assert.AreEqual(HolderIds.None, mix.GetHolder(0));
            Assert.AreEqual(HolderIds.None, mix.GetHolder(1));
            Assert.AreEqual(3, mix.GetHolder(2));
        }

        [Test]
        public void SetDrop_RaisesOneGlobalChange()
        {
            var mix = new MixState(1);
            var changes = Record(mix);
            var drop = new DropState { phase = DropPhase.Build, buildStartSongTime = 1.0, hitSongTime = 5.0 };
            Assert.IsTrue(mix.SetDrop(drop));
            Assert.IsFalse(mix.SetDrop(drop));
            Assert.AreEqual(1, changes.Count);
            Assert.IsTrue(changes[0].IsGlobal);
            Assert.AreEqual(MixField.Drop, changes[0].Field);
            Assert.AreEqual(DropPhase.Build, mix.DropPhase);
        }

        [Test]
        public void ResetStems_ReturnsToNeutralButKeepsHolders()
        {
            var mix = new MixState(2);
            mix.SetMuted(0, true);
            mix.SetSoloed(1, true);
            mix.SetGain(1, 0.2f);
            mix.SetFilter(0, 0.7f);
            mix.SetReverb(1, 0.4f);
            mix.TryBeginHold(1, 1);

            mix.ResetStems();

            for (int i = 0; i < 2; i++)
            {
                StemMix s = mix.GetStem(i);
                Assert.IsFalse(s.muted);
                Assert.IsFalse(s.soloed);
                Assert.AreEqual(1f, s.gain01);
                Assert.AreEqual(0f, s.filter);
                Assert.AreEqual(0f, s.reverb01);
            }
            Assert.IsFalse(mix.AnySoloed);
            Assert.AreEqual(1, mix.GetHolder(1));
        }

        // ------------------------------------------------------------------ snapshots

        static MixState MakeInterestingMix()
        {
            var mix = new MixState(3);
            mix.SetMuted(0, true);
            mix.SetSoloed(1, true);
            mix.SetGain(1, 0.42f);
            mix.SetFilter(2, -0.65f);
            mix.SetReverb(0, 0.3f);
            mix.TryBeginHold(2, 7);
            mix.SetDrop(new DropState { phase = DropPhase.Hit, buildStartSongTime = 12.5, hitSongTime = 16.0 });
            return mix;
        }

        static void AssertSameMix(MixState expected, MixState actual)
        {
            Assert.AreEqual(expected.StemCount, actual.StemCount);
            for (int i = 0; i < expected.StemCount; i++)
            {
                StemMix e = expected.GetStem(i), a = actual.GetStem(i);
                Assert.AreEqual(e.muted, a.muted, $"stem {i} muted");
                Assert.AreEqual(e.soloed, a.soloed, $"stem {i} soloed");
                Assert.AreEqual(e.gain01, a.gain01, $"stem {i} gain");
                Assert.AreEqual(e.filter, a.filter, $"stem {i} filter");
                Assert.AreEqual(e.reverb01, a.reverb01, $"stem {i} reverb");
                Assert.AreEqual(e.holderId, a.holderId, $"stem {i} holder");
            }
            Assert.AreEqual(expected.AnySoloed, actual.AnySoloed);
            Assert.AreEqual(expected.SoloCount, actual.SoloCount);
            Assert.IsTrue(expected.Drop.SameAs(actual.Drop), "drop state");
        }

        [Test]
        public void Snapshot_RoundTripsThroughApply()
        {
            MixState source = MakeInterestingMix();
            MixSnapshot snapshot = source.CaptureSnapshot();
            Assert.AreEqual(source.Version, snapshot.version);

            var copy = new MixState(3);
            var changes = Record(copy);
            int count = copy.ApplySnapshot(snapshot, MixChangeSource.Remote);

            AssertSameMix(source, copy);
            Assert.AreEqual(count, changes.Count);
            Assert.IsTrue(changes.TrueForAll(c => c.Source == MixChangeSource.Remote));
        }

        [Test]
        public void Snapshot_RoundTripsThroughJson()
        {
            MixState source = MakeInterestingMix();
            string json = JsonUtility.ToJson(source.CaptureSnapshot());
            var restored = JsonUtility.FromJson<MixSnapshot>(json);

            var copy = new MixState(3);
            copy.ApplySnapshot(restored);
            AssertSameMix(source, copy);
        }

        [Test]
        public void Snapshot_IsACopy()
        {
            var mix = new MixState(1);
            MixSnapshot snapshot = mix.CaptureSnapshot();
            mix.SetGain(0, 0.1f);
            Assert.AreEqual(1f, snapshot.stems[0].gain01);
        }

        [Test]
        public void CaptureSnapshot_ReusesTheGivenSnapshot()
        {
            var mix = new MixState(2);
            var snapshot = new MixSnapshot();
            MixSnapshot first = mix.CaptureSnapshot(snapshot);
            StemMix[] array = first.stems;
            mix.SetGain(0, 0.3f);
            MixSnapshot second = mix.CaptureSnapshot(snapshot);
            Assert.AreSame(snapshot, second);
            Assert.AreSame(array, second.stems);
            Assert.AreEqual(0.3f, second.stems[0].gain01);
        }

        [Test]
        public void ApplySnapshot_OnlyRaisesForDifferences()
        {
            MixSnapshot snapshot = MakeInterestingMix().CaptureSnapshot();
            var copy = new MixState(3);
            copy.ApplySnapshot(snapshot);

            var changes = Record(copy);
            Assert.AreEqual(0, copy.ApplySnapshot(snapshot));
            Assert.AreEqual(0, changes.Count);
        }

        [Test]
        public void ApplySnapshot_RejectsWrongStemCount()
        {
            MixSnapshot snapshot = new MixState(2).CaptureSnapshot();
            Assert.Throws<ArgumentException>(() => new MixState(3).ApplySnapshot(snapshot));
            Assert.Throws<ArgumentNullException>(() => new MixState(3).ApplySnapshot(null));
        }
    }
}
