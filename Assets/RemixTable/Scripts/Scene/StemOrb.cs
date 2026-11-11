using UnityEngine;
using UnityEngine.Events;

namespace RemixTable
{
    /// <summary>
    /// Links a scene object (the glowing orb) to one stem. Every frame it turns its position into gain (distance
    /// to the listener along the desk) and reverb (height above the desk) and writes them to the
    /// <see cref="MixState"/>, unless another holder has the stem. Gesture code calls the public methods; nothing
    /// here talks to AudioSources. The stem's sound follows this transform.
    /// Events fire from MixState changes, so they also fire for changes made elsewhere (for example the network).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Remix Table/Stem Orb")]
    public sealed class StemOrb : MonoBehaviour
    {
        [SerializeField] StemPlayer player;
        [SerializeField] DeskFrame desk;
        [SerializeField, Min(0)] int stemIndex;
        [Tooltip("Distance from the orb's pivot to its bottom (its radius for a sphere). Height is measured from the bottom.")]
        [SerializeField, Min(0f)] float restHeight = 0.04f;
        [Tooltip("Turn off to drive gain and reverb some other way.")]
        [SerializeField] bool mapPosition = true;
        [SerializeField] int localHolderId = HolderIds.LocalPlayer;

        public UnityEvent<bool> OnMuted = new UnityEvent<bool>();
        public UnityEvent<bool> OnSoloed = new UnityEvent<bool>();
        public UnityEvent<bool> OnHeld = new UnityEvent<bool>();

        MixState mix;
        MixTuning tuning;
        float twistDegrees;

        public int StemIndex => stemIndex;
        public StemPlayer Player => player;
        public DeskFrame Desk => desk;
        public float RestHeight => restHeight;
        public bool IsBound => mix != null && stemIndex < mix.StemCount;
        public bool IsMuted => IsBound && mix.IsMuted(stemIndex);
        public bool IsSoloed => IsBound && mix.IsSoloed(stemIndex);
        public int HolderId => IsBound ? mix.GetHolder(stemIndex) : HolderIds.None;
        public bool IsHeld => HolderId != HolderIds.None;
        public bool IsHeldLocally => HolderId == localHolderId;

        /// <summary>Accumulated twist in degrees (negative = low-pass side). Hand input should add its wrist delta
        /// to the value it read when the grab started.</summary>
        public float TwistDegrees => twistDegrees;

        public float HeightAboveDesk => desk != null ? desk.HeightAbove(transform.position) - restHeight : 0f;
        public float DistanceToListener => desk != null ? desk.HorizontalDistanceToListener(transform.position) : 0f;

        /// <summary>For code that spawns orbs (for example the record pull-apart).</summary>
        public void Configure(StemPlayer stemPlayer, DeskFrame deskFrame, int stem, float orbRestHeight)
        {
            player = stemPlayer;
            desk = deskFrame;
            stemIndex = stem;
            restHeight = orbRestHeight;
            if (isActiveAndEnabled && player != null) Bind();
        }

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<StemPlayer>();
            if (desk == null) desk = FindFirstObjectByType<DeskFrame>();
            if (player == null) RemixLog.Warn($"StemOrb '{name}' has no StemPlayer.", this);
            if (desk == null) RemixLog.Warn($"StemOrb '{name}' has no DeskFrame; position won't change the mix.", this);
            if (player == null) return;

            player.SongLoaded += Bind;
            Bind();
        }

        void OnDestroy()
        {
            Unbind();
            if (player != null)
            {
                player.SongLoaded -= Bind;
                player.UnregisterOrb(stemIndex, transform);
            }
        }

        void Update()
        {
            if (!mapPosition || desk == null || !IsBound) return;
            if (mix.IsHeldByOther(stemIndex, localHolderId)) return;

            mix.SetGain(stemIndex, MixMapping.DistanceToGain(DistanceToListener, tuning));
            mix.SetReverb(stemIndex, MixMapping.HeightToReverb(HeightAboveDesk, tuning));
        }

        public bool BeginHold() => BeginHold(localHolderId);

        /// <summary>Claims the stem. False if someone else is holding it.</summary>
        public bool BeginHold(int holderId) => IsBound && mix.TryBeginHold(stemIndex, holderId);

        public bool EndHold() => EndHold(localHolderId);

        public bool EndHold(int holderId) => IsBound && mix.EndHold(stemIndex, holderId);

        /// <summary>Void versions for Inspector wiring, e.g. XRGrabInteractable Select Entered / Select Exited.</summary>
        public void BeginLocalHold() => BeginHold();
        public void EndLocalHold() => EndHold();

        public void ToggleMute()
        {
            if (IsBound) mix.ToggleMute(stemIndex);
        }

        public void SetMuted(bool muted)
        {
            if (IsBound) mix.SetMuted(stemIndex, muted);
        }

        public void SetSolo(bool soloed)
        {
            if (IsBound) mix.SetSoloed(stemIndex, soloed);
        }

        /// <summary>Absolute twist in degrees → filter (clamped to ±twistFullDegrees, dead zone in the middle).</summary>
        public void SetTwist(float degrees)
        {
            if (!IsBound) return;
            float limit = tuning.twistFullDegrees;
            twistDegrees = Mathf.Clamp(degrees, -limit, limit);
            mix.SetFilter(stemIndex, MixMapping.TwistToFilter(twistDegrees, tuning));
        }

        public void AddTwist(float deltaDegrees) => SetTwist(twistDegrees + deltaDegrees);

        void Bind()
        {
            Unbind();
            if (player == null || player.Mix == null) return;
            if (stemIndex >= player.Mix.StemCount)
            {
                RemixLog.Warn($"StemOrb '{name}' points at stem {stemIndex}, but the song has {player.Mix.StemCount}.", this);
                return;
            }

            mix = player.Mix;
            tuning = player.Tuning;
            mix.Changed += HandleMixChanged;
            twistDegrees = MixMapping.FilterToTwist(mix.GetFilter(stemIndex), tuning);
            player.RegisterOrb(stemIndex, transform);
        }

        void Unbind()
        {
            if (mix != null) mix.Changed -= HandleMixChanged;
            mix = null;
        }

        void HandleMixChanged(MixChange change)
        {
            if (change.StemIndex != stemIndex) return;
            switch (change.Field)
            {
                case MixField.Muted:
                    OnMuted.Invoke(mix.IsMuted(stemIndex));
                    break;
                case MixField.Soloed:
                    OnSoloed.Invoke(mix.IsSoloed(stemIndex));
                    break;
                case MixField.Holder:
                    OnHeld.Invoke(mix.GetHolder(stemIndex) != HolderIds.None);
                    break;
                case MixField.Filter:
                    // Keep the twist angle in step when the filter was changed elsewhere (reset, network).
                    float filter = mix.GetFilter(stemIndex);
                    if (Mathf.Abs(MixMapping.TwistToFilter(twistDegrees, tuning) - filter) > 1e-3f)
                        twistDegrees = MixMapping.FilterToTwist(filter, tuning);
                    break;
            }
        }
    }
}
