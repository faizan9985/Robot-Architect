using UnityEngine;

namespace RemixTable
{
    /// <summary>
    /// Where the music clock gets "now" from. The local implementation is the audio clock
    /// (<see cref="AudioSettings.dspTime"/>), which is also what <see cref="AudioSource.PlayScheduled"/> uses.
    /// A future network time source must map shared time onto dspTime before anything is scheduled.
    /// </summary>
    public interface ITimeSource
    {
        double Now { get; }
    }

    public sealed class LocalDspTimeSource : ITimeSource
    {
        public static readonly LocalDspTimeSource Instance = new LocalDspTimeSource();

        public double Now => AudioSettings.dspTime;
    }
}
