using NUnit.Framework;
using UnityEngine;

namespace RemixTable.Tests
{
    public class MixMappingTests
    {
        MixTuning t;

        [SetUp]
        public void SetUp() => t = ScriptableObject.CreateInstance<MixTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(t);

        // ------------------------------------------------------------------ distance → gain

        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(0.25f)]
        public void DistanceToGain_IsFullUpClose(float distance)
        {
            Assert.AreEqual(1f, MixMapping.DistanceToGain(distance, t));
        }

        [TestCase(0.85f)]
        [TestCase(1.5f)]
        [TestCase(100f)]
        public void DistanceToGain_SitsOnTheQuietFloorFarAway(float distance)
        {
            Assert.AreEqual(0.05f, MixMapping.DistanceToGain(distance, t), 1e-4f);
        }

        [Test]
        public void DistanceToGain_FallsSmoothlyAndMonotonically()
        {
            float previous = MixMapping.DistanceToGain(0f, t);
            for (float d = 0.01f; d <= 1.2f; d += 0.01f)
            {
                float g = MixMapping.DistanceToGain(d, t);
                Assert.LessOrEqual(g, previous + 1e-6f, $"not monotonic at {d}");
                if (d > 0.26f && d < 0.84f) Assert.Less(g, previous, $"flat inside the curve at {d}");
                Assert.Less(previous - g, 0.08f, $"jump at {d}");
                previous = g;
            }
            float middle = MixMapping.DistanceToGain(0.55f, t);
            Assert.Greater(middle, 0.05f);
            Assert.Less(middle, 1f);
        }

        // ------------------------------------------------------------------ height → reverb

        [TestCase(-0.2f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.03f, 0f)]
        [TestCase(0.165f, 0.5f)]
        [TestCase(0.30f, 1f)]
        [TestCase(2f, 1f)]
        public void HeightToReverb_HasDeadZoneAndClamps(float height, float expected)
        {
            Assert.AreEqual(expected, MixMapping.HeightToReverb(height, t), 1e-4f);
        }

        [Test]
        public void HeightToReverb_IsMonotonic()
        {
            float previous = 0f;
            for (float h = 0f; h <= 0.5f; h += 0.005f)
            {
                float r = MixMapping.HeightToReverb(h, t);
                Assert.GreaterOrEqual(r, previous);
                previous = r;
            }
        }

        // ------------------------------------------------------------------ twist → filter

        [TestCase(0f)]
        [TestCase(5f)]
        [TestCase(-7.9f)]
        [TestCase(8f)]
        [TestCase(-8f)]
        public void TwistToFilter_HasADeadZone(float degrees)
        {
            Assert.AreEqual(0f, MixMapping.TwistToFilter(degrees, t));
        }

        [TestCase(90f, 1f)]
        [TestCase(-90f, -1f)]
        [TestCase(180f, 1f)]
        [TestCase(-500f, -1f)]
        [TestCase(49f, 0.5f)]
        [TestCase(-49f, -0.5f)]
        public void TwistToFilter_ReachesAndClampsAtTheEnds(float degrees, float expected)
        {
            Assert.AreEqual(expected, MixMapping.TwistToFilter(degrees, t), 1e-4f);
        }

        [Test]
        public void TwistToFilter_IsMonotonicAndSymmetric()
        {
            float previous = -1f;
            for (float d = -120f; d <= 120f; d += 0.5f)
            {
                float f = MixMapping.TwistToFilter(d, t);
                Assert.GreaterOrEqual(f, previous, $"not monotonic at {d}");
                Assert.AreEqual(-f, MixMapping.TwistToFilter(-d, t), 1e-6f, $"not symmetric at {d}");
                previous = f;
            }
            Assert.Greater(MixMapping.TwistToFilter(9f, t), 0f, "just past the dead zone");
        }

        [TestCase(-1f)]
        [TestCase(-0.3f)]
        [TestCase(0f)]
        [TestCase(0.75f)]
        [TestCase(1f)]
        public void FilterToTwist_InvertsTwistToFilter(float filter)
        {
            float degrees = MixMapping.FilterToTwist(filter, t);
            Assert.AreEqual(filter, MixMapping.TwistToFilter(degrees, t), 1e-4f);
        }

        // ------------------------------------------------------------------ filter → cutoff

        [Test]
        public void LowPassCutoff_OpenAtNeutralAndHighPassSide()
        {
            Assert.AreEqual(22000f, MixMapping.LowPassCutoffHz(0f, t));
            Assert.AreEqual(22000f, MixMapping.LowPassCutoffHz(1f, t));
        }

        [Test]
        public void LowPassCutoff_SweepsExponentiallyAndClamps()
        {
            Assert.AreEqual(350f, MixMapping.LowPassCutoffHz(-1f, t), 0.5f);
            Assert.AreEqual(350f, MixMapping.LowPassCutoffHz(-3f, t), 0.5f);
            Assert.AreEqual(Mathf.Sqrt(22000f * 350f), MixMapping.LowPassCutoffHz(-0.5f, t), 1f, "geometric midpoint");

            float previous = float.MaxValue;
            for (float f = 0f; f >= -1f; f -= 0.02f)
            {
                float hz = MixMapping.LowPassCutoffHz(f, t);
                Assert.LessOrEqual(hz, previous);
                previous = hz;
            }
        }

        [Test]
        public void HighPassCutoff_OpenAtNeutralAndLowPassSide()
        {
            Assert.AreEqual(10f, MixMapping.HighPassCutoffHz(0f, t));
            Assert.AreEqual(10f, MixMapping.HighPassCutoffHz(-1f, t));
        }

        [Test]
        public void HighPassCutoff_SweepsExponentiallyAndClamps()
        {
            Assert.AreEqual(2500f, MixMapping.HighPassCutoffHz(1f, t), 0.5f);
            Assert.AreEqual(2500f, MixMapping.HighPassCutoffHz(4f, t), 0.5f);
            Assert.AreEqual(Mathf.Sqrt(10f * 2500f), MixMapping.HighPassCutoffHz(0.5f, t), 0.5f, "geometric midpoint");

            float previous = 0f;
            for (float f = 0f; f <= 1f; f += 0.02f)
            {
                float hz = MixMapping.HighPassCutoffHz(f, t);
                Assert.GreaterOrEqual(hz, previous);
                previous = hz;
            }
        }

        // ------------------------------------------------------------------ reverb levels

        [Test]
        public void Reverb_IsSilentAtZeroAndRisesMonotonically()
        {
            Assert.AreEqual(MixMapping.SilentMillibels, MixMapping.ReverbToRoomMillibels(0f, t));
            Assert.AreEqual(0f, MixMapping.ReverbToDryMillibels(0f, t), 1e-3f);

            float previousRoom = MixMapping.SilentMillibels;
            float previousDry = 0f;
            for (float r = 0.05f; r <= 1f; r += 0.05f)
            {
                float room = MixMapping.ReverbToRoomMillibels(r, t);
                float dry = MixMapping.ReverbToDryMillibels(r, t);
                Assert.Greater(room, previousRoom);
                Assert.LessOrEqual(dry, previousDry);
                Assert.LessOrEqual(room, 0f);
                previousRoom = room;
                previousDry = dry;
            }
        }
    }
}
