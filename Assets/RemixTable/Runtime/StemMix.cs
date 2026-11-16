using System;
using UnityEngine;
namespace RemixTable
{
    // Per-song mixer state that is not time automation: mute / solo per stem and where each stem sits in the
    // room in SPATIAL mode. Indexed by (int)StemId.
    [Serializable] public class StemMix
    {
        public const int StemCount = 4;
        public bool[] muted = new bool[StemCount], soloed = new bool[StemCount];
        public bool spatial;
        // Stem positions relative to the seated head anchor, in the workspace's orientation (meters).
        // Defaults spread the stems in an arc around the listener.
        public Vector3[] positions = DefaultPositions();
        // Bumped on every change so UI and persistence can react cheaply.
        [NonSerialized] public int Version;

        public bool AnySolo { get { foreach (var s in soloed) if (s) return true; return false; } }
        // 0 when the stem is silenced by mute or by another stem's solo, else 1. Solo wins over mute on the
        // soloed stem itself, like a hardware console.
        public float Factor(StemId stem)
        {
            int i = (int)stem;
            if (AnySolo) return soloed[i] ? 1 : 0;
            return muted[i] ? 0 : 1;
        }
        public bool Audible(StemId stem) => Factor(stem) > 0;
        public void SetMuted(StemId stem, bool value) { if (muted[(int)stem] == value) return; muted[(int)stem] = value; Version++; }
        public void SetSoloed(StemId stem, bool value) { if (soloed[(int)stem] == value) return; soloed[(int)stem] = value; Version++; }
        public void ToggleMute(StemId stem) => SetMuted(stem, !muted[(int)stem]);
        public void ToggleSolo(StemId stem) => SetSoloed(stem, !soloed[(int)stem]);
        public void SetSpatial(bool value) { if (spatial == value) return; spatial = value; Version++; }
        public void SetPosition(StemId stem, Vector3 local)
        {
            if (float.IsNaN(local.x) || float.IsNaN(local.y) || float.IsNaN(local.z)) return;
            if ((positions[(int)stem] - local).sqrMagnitude < 1e-8f) return;
            positions[(int)stem] = local; Version++;
        }
        public void ClearMuteSolo()
        {
            bool changed = false;
            for (int i = 0; i < StemCount; i++) { changed |= muted[i] || soloed[i]; muted[i] = soloed[i] = false; }
            if (changed) Version++;
        }
        public void ResetPositions() { positions = DefaultPositions(); Version++; }
        public void CopyFrom(StemMix other)
        {
            for (int i = 0; i < StemCount; i++)
            {
                muted[i] = other != null && other.muted != null && i < other.muted.Length && other.muted[i];
                soloed[i] = other != null && other.soloed != null && i < other.soloed.Length && other.soloed[i];
                positions[i] = other != null && other.positions != null && i < other.positions.Length ? other.positions[i] : DefaultPositions()[i];
            }
            spatial = other != null && other.spatial;
            Version++;
        }
        // Vocals front and center; drums, bass and other fanned out to the sides at roughly arm's length.
        public static Vector3[] DefaultPositions()
        {
            var p = new Vector3[StemCount];
            p[(int)StemId.Vocals] = Polar(0, .15f);
            p[(int)StemId.Drums] = Polar(-50, -.1f);
            p[(int)StemId.Bass] = Polar(50, -.1f);
            p[(int)StemId.Melody] = Polar(-100, .1f);
            return p;
        }
        // Angle in degrees from straight ahead (positive = right); y is height relative to eye level.
        // 1.1 m keeps every orb beyond the 0.8 m ribbon arc, so grabbing one never touches a ribbon.
        static Vector3 Polar(float degrees, float y)
        {
            const float radius = 1.1f;
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius);
        }

        // Stereo pan and level for a stem placed at `world`, heard from a head at `head` facing `forward`.
        // Pan follows the left/right angle; level falls off gently with distance and is never louder than 1.
        public static void Spatialize(Vector3 head, Vector3 forward, Vector3 world, out float pan, out float gain)
        {
            var flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-6f) flatForward = Vector3.forward;
            flatForward.Normalize();
            var offset = world - head;
            var flat = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f) { pan = 0; gain = 1; return; }
            var right = Vector3.Cross(Vector3.up, flatForward);
            float sideways = Vector3.Dot(flat.normalized, right);                 // -1 left .. +1 right
            pan = Mathf.Clamp(sideways, -1, 1) * .9f;                            // Keep a little of the other ear.
            float distance = offset.magnitude;
            gain = Mathf.Clamp(1.2f / Mathf.Max(1.2f, distance), .2f, 1);        // Full level within 1.2 m.
            // Sources behind the listener are slightly quieter, a cheap front/back cue.
            if (Vector3.Dot(flat.normalized, flatForward) < 0) gain *= Mathf.Lerp(1, .75f, -Vector3.Dot(flat.normalized, flatForward));
        }
    }
}
