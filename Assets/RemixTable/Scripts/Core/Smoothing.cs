using System;
using UnityEngine;

namespace RemixTable
{
    /// <summary>
    /// A value that moves to its target in a straight line, taking <c>rampSeconds</c> from the most recent
    /// <see cref="SetTarget"/>. Every change therefore takes at least the ramp time, however small it is,
    /// which is what keeps gain changes free of clicks and zipper noise. When the target changes every frame
    /// (dragging), this behaves like a smooth follower with a time constant of the ramp time.
    /// </summary>
    public struct RampedValue
    {
        float current;
        float target;
        float ratePerSecond;

        public RampedValue(float value)
        {
            current = value;
            target = value;
            ratePerSecond = 0f;
        }

        public float Current => current;
        public float Target => target;
        public bool IsSettled => current == target;

        public void Snap(float value)
        {
            current = value;
            target = value;
            ratePerSecond = 0f;
        }

        public void SetTarget(float value, float rampSeconds)
        {
            if (value == target) return;
            target = value;
            float distance = Mathf.Abs(target - current);
            ratePerSecond = rampSeconds > 0f ? distance / rampSeconds : float.PositiveInfinity;
        }

        public float Tick(float deltaSeconds)
        {
            if (current == target) return current;
            float remaining = target - current;
            float step = ratePerSecond * Mathf.Max(0f, deltaSeconds);
            if (float.IsInfinity(step) || Mathf.Abs(remaining) <= step) current = target;
            else current += remaining > 0f ? step : -step;
            return current;
        }
    }

    public static class Smoothing
    {
        /// <summary>Frame-rate independent envelope follower: rises with <paramref name="attackSeconds"/>,
        /// falls with <paramref name="releaseSeconds"/> (time constants).</summary>
        public static float Follow(float current, float target, float deltaSeconds, float attackSeconds, float releaseSeconds)
        {
            float tau = target > current ? attackSeconds : releaseSeconds;
            if (tau <= 0f) return target;
            return target + (current - target) * Mathf.Exp(-Mathf.Max(0f, deltaSeconds) / tau);
        }
    }

    public static class SampleMath
    {
        /// <summary>Distance between two play positions on a loop of <paramref name="length"/> samples,
        /// taking the short way round (so 10 and length-10 are 20 apart).</summary>
        public static int CircularDistance(int a, int b, int length)
        {
            if (length <= 0) return Math.Abs(a - b);
            int d = Math.Abs(a - b) % length;
            return Math.Min(d, length - d);
        }

        /// <summary>Wraps <paramref name="value"/> into (-length/2, length/2].</summary>
        public static double WrapSigned(double value, double length)
        {
            if (length <= 0) return value;
            double wrapped = value - Math.Floor(value / length) * length; // [0, length)
            return wrapped > length * 0.5 ? wrapped - length : wrapped;
        }
    }
}
