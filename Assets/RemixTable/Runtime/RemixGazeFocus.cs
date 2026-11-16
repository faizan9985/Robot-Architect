using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;
namespace RemixTable
{
    // Gaze (head direction) as a lightweight second input:
    //  - the ribbon you look at brightens and a small label names it, with its state (MUTED / SOLO);
    //  - look at a ribbon and hold a pinch anywhere away from the ribbons: you hear that stem alone for as long
    //    as you hold (audition). Release and the full mix is back. Nothing in the remix changes.
    // Dwell smoothing stops the focus flickering between lanes; editing, orb grabs and two-hand gestures win.
    public sealed class RemixGazeFocus : MonoBehaviour
    {
        public RemixTableManager manager;
        public TMP_Text label;
        [Tooltip("Seconds the gaze must rest on a new ribbon before focus moves.")]
        [Min(0)] public float dwell = .12f;
        [Tooltip("Seconds a pinch must be held before the audition starts (quick UI pinches never trigger it).")]
        [Min(0)] public float auditionDelay = .35f;
        [Tooltip("A pinch closer than this to a ribbon belongs to ribbon editing (meters).")]
        [Min(0)] public float ribbonClearance = .09f;

        public int Focus { get; private set; } = -1;          // index into manager.ribbons
        public bool Auditioning { get; private set; }
        int candidate = -1; float candidateTime, pinchTime;
        readonly List<XRHandSubsystem> systems = new List<XRHandSubsystem>();
        readonly bool[] pinching = new bool[2];

        void Update()
        {
            var cam = Camera.main;
            if (manager == null || cam == null) return;
            Tick(new Ray(cam.transform.position, cam.transform.forward), ReadHands(out var a, out var b), a, b, Time.unscaledDeltaTime);
        }

        // Testable core: gaze ray, number of pinching hands and their positions.
        public void Tick(Ray gaze, int pinches, Vector3 handA, Vector3 handB, float dt)
        {
            int hit = Raycast(gaze, out var point);
            if (hit != candidate) { candidate = hit; candidateTime = 0; }
            candidateTime += dt;
            if (candidate != Focus && candidateTime >= dwell && !Auditioning) Focus = candidate;

            // Audition: exactly one pinching hand, away from every ribbon, nothing else going on.
            bool blocked = manager.IsEditing || manager.ExternalHold || pinches != 1 || NearRibbon(handA);
            if (Focus >= 0 && pinches == 1 && !blocked) pinchTime += dt; else pinchTime = 0;
            bool audition = Focus >= 0 && pinchTime >= auditionDelay;
            if (audition != Auditioning)
            {
                Auditioning = audition;
                if (manager.audioManager != null) manager.audioManager.auditionStem = audition ? (int)manager.ribbons[Focus].stem : -1;
            }
            if (manager.ribbons != null)
                for (int i = 0; i < manager.ribbons.Length; i++)
                {
                    var r = manager.ribbons[i]; if (r == null) continue;
                    float target = i == Focus ? (Auditioning ? 1 : .6f) : 0;
                    r.gazeGlow = Mathf.MoveTowards(r.gazeGlow, target, dt * 5);
                }
            UpdateLabel(hit >= 0 ? point : default);
        }

        void OnDisable()
        {
            if (Auditioning && manager != null && manager.audioManager != null) manager.audioManager.auditionStem = -1;
            Auditioning = false;
            if (manager != null && manager.ribbons != null) foreach (var r in manager.ribbons) if (r != null) r.gazeGlow = 0;
            if (label != null) label.gameObject.SetActive(false);
        }

        // Which ribbon lane the gaze crosses on the arc (curved) or plane (flat), or -1.
        public int Raycast(Ray gaze, out Vector3 point)
        {
            point = default;
            if (manager.workspace == null || manager.ribbons == null || manager.design == null) return -1;
            var d = manager.design; var w = manager.workspace;
            var o = w.InverseTransformPoint(gaze.origin); var dir = w.InverseTransformDirection(gaze.direction);
            float t;
            if (d.curvedTimeline)
            {
                // Cylinder of radius R around (0, *, -R): take the exit point (the head is inside it).
                float R = d.Radius; var oc = new Vector2(o.x, o.z + R); var dv = new Vector2(dir.x, dir.z);
                float a = Vector2.Dot(dv, dv), b = 2 * Vector2.Dot(oc, dv), c = Vector2.Dot(oc, oc) - R * R;
                float disc = b * b - 4 * a * c;
                if (a < 1e-6f || disc < 0) return -1;
                t = (-b + Mathf.Sqrt(disc)) / (2 * a);
            }
            else
            {
                if (Mathf.Abs(dir.z) < 1e-5f) return -1;
                t = -o.z / dir.z;
            }
            if (t <= 0) return -1;
            var p = o + dir * t;
            if (d.curvedTimeline && Mathf.Abs(Mathf.Atan2(p.x, p.z + d.Radius)) > d.HalfAngle) return -1;
            if (!d.curvedTimeline && Mathf.Abs(p.x) > d.width * .5f) return -1;
            int best = -1; float bestDistance = d.laneHeight * .65f;
            for (int i = 0; i < manager.ribbons.Length; i++)
            {
                var r = manager.ribbons[i]; if (r == null) continue;
                float dy = Mathf.Abs(p.y - r.transform.localPosition.y);
                if (dy < bestDistance) { bestDistance = dy; best = i; }
            }
            if (best >= 0) point = w.TransformPoint(p);
            return best;
        }

        bool NearRibbon(Vector3 world)
        {
            if (manager.ribbons == null) return false;
            foreach (var r in manager.ribbons)
            {
                if (r == null) continue;
                r.CanSelectAt(world, out float distance); // distance to the ribbon surface, reported either way
                if (distance < ribbonClearance) return true;
            }
            return false;
        }

        void UpdateLabel(Vector3 point)
        {
            if (label == null) return;
            bool show = Focus >= 0 && manager.ribbons[Focus] != null && !manager.IsEditing;
            if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            if (!show) return;
            var stem = manager.ribbons[Focus].stem;
            string state = Auditioning ? "LISTENING ALONE" : manager.mix.soloed[(int)stem] ? "SOLO" : !manager.mix.Audible(stem) ? "MUTED" : "PINCH IN THE AIR TO HEAR IT ALONE";
            string text = RemixCommands.StemName(stem) + "  ·  " + state;
            if (label.text != text) label.text = text;
            var cam = Camera.main; if (cam == null || point == default) return;
            float scale = manager.workspace.lossyScale.x;
            var target = point + manager.workspace.up * (manager.design.laneHeight * .75f * scale) + (cam.transform.position - point).normalized * .02f;
            label.transform.position = Vector3.Lerp(label.transform.position, target, .35f);
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, manager.workspace.up);
        }

        int ReadHands(out Vector3 a, out Vector3 b)
        {
            a = b = default; int count = 0;
            SubsystemManager.GetSubsystems(systems);
            XRHandSubsystem system = null; foreach (var s in systems) if (s.running) { system = s; break; }
            if (system == null) return 0;
            var tracking = Camera.main != null ? Camera.main.transform.parent : null;
            var hands = new[] { system.leftHand, system.rightHand };
            for (int h = 0; h < 2; h++)
            {
                var hand = hands[h];
                if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) || !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb)) { pinching[h] = false; continue; }
                var sample = RibbonInteractionManager.SamplePinch(index.position, thumb.position, tracking, pinching[h]);
                pinching[h] = sample.pinch;
                if (!sample.pinch) continue;
                if (count == 0) a = sample.position; else b = sample.position;
                count++;
            }
            return count;
        }
    }
}
