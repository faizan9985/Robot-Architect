using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // First 60 seconds, no wall of text: one short line at a time, a ghost pinch that shows the gesture right on
    // the ribbon, and each step completes the moment the user actually does it.
    //   1 PULL   pinch the orange VOCALS ribbon and pull down -> instant karaoke
    //   2 BACK   pull it back up (or UNDO)                     -> you can always get it back
    //   3 VIBE   tap a VIBE                                     -> the whole mix changes in one tap
    //   4 ROOM   press ROOM (headset only; skipped otherwise)   -> the table moves into your room
    //   DONE     "today's Daily Drop is on your left"
    // Runs once (remembered); SKIP ends it, saying "tutorial" / "help" replays it.
    public sealed class RemixOnboarding : MonoBehaviour
    {
        public enum Step { Waiting, Pull, Back, Vibe, Room, Done, Off }
        public RemixTableManager manager;
        public RemixToolbox toolbox;
        public RemixVibes vibes;
        [Header("UI")]
        public GameObject card;
        public TMP_Text stepLabel, instruction;
        public Button skipButton;
        public Graphic vibesHighlight;     // VIBES panel background, pulses on step 3
        [Header("Ghost pinch")]
        public Transform ghost, ghostThumb, ghostIndex;
        public Renderer[] ghostRenderers;
        public Color ghostColor = new Color(1f, .55f, .2f, 1);
        [Min(.5f)] public float startDelay = 1.5f;
        [Tooltip("PlayerPrefs key remembering that the quick start was finished or skipped.")]
        public string doneKey = "remix.onboarding.done";

        public Step Current { get; private set; } = Step.Waiting;
        public event Action<Step> StepChanged;
        public Func<bool> roomAvailable;   // set by the scene: passthrough can run here
        float stepTime, waitTime, highlightBase = -1; bool vibeTried; Color vibesBase;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");

        public static string Text(Step s) => s switch
        {
            Step.Pull => "PINCH THE ORANGE VOCALS RIBBON\nAND PULL DOWN",
            Step.Back => "INSTANT KARAOKE.\nPULL IT BACK UP (OR PRESS UNDO)",
            Step.Vibe => "NOW TAP A VIBE ON YOUR RIGHT",
            Step.Room => "PRESS ROOM TO BRING IT INTO YOUR SPACE",
            Step.Done => "YOU'RE PRODUCING.\nTODAY'S DAILY DROP IS ON YOUR LEFT",
            _ => "",
        };

        RemixVibes subscribed;   // subscribe lazily: survives vibes being wired late (and edit-mode tests)
        void Subscribe()
        {
            if (subscribed == vibes) return;
            if (subscribed != null) subscribed.Applied -= OnVibe;
            subscribed = vibes;
            if (subscribed != null) subscribed.Applied += OnVibe;
        }
        void OnEnable() => Subscribe();
        void OnDisable() { if (subscribed != null) subscribed.Applied -= OnVibe; subscribed = null; }
        void OnVibe(RemixVibe v) { if (v != RemixVibe.Normal) vibeTried = true; }
        void Start()
        {
            bool done = false;
            try { done = PlayerPrefs.GetInt(doneKey, 0) == 1; } catch { }
            if (done) Go(Step.Off); else Show(false);
        }

        public void Restart() { Go(Step.Waiting); waitTime = startDelay; }
        public void Skip() { Finish(); if (toolbox != null) toolbox.Say("SAY \"TUTORIAL\" ANY TIME TO SEE IT AGAIN"); }

        void Finish()
        {
            try { PlayerPrefs.SetInt(doneKey, 1); PlayerPrefs.Save(); } catch { }
            Go(Step.Off);
        }

        public void Go(Step step)
        {
            Subscribe();
            Current = step; stepTime = 0; vibeTried = false;
            Show(step != Step.Off && step != Step.Waiting);
            if (stepLabel != null)
            {
                int n = step == Step.Pull ? 1 : step == Step.Back ? 2 : step == Step.Vibe ? 3 : step == Step.Room ? 4 : 0;
                stepLabel.text = n > 0 ? $"QUICK START  {n} / {(RoomAvailable ? 4 : 3)}" : "QUICK START";
            }
            if (instruction != null) instruction.text = Text(step);
            StepChanged?.Invoke(step);
        }
        bool RoomAvailable => roomAvailable != null ? roomAvailable() :
            toolbox != null && toolbox.passthrough is IRemixToggle room && room.Available;
        void Show(bool on)
        {
            if (card != null && card.activeSelf != on) card.SetActive(on);
            if (ghost != null && ghost.gameObject.activeSelf && !on) ghost.gameObject.SetActive(false);
            if (!on) RestoreHighlight();
        }

        void Update() => Tick(Time.unscaledDeltaTime);
        public void Tick(float dt)
        {
            if (manager == null || Current == Step.Off) return;
            Subscribe();
            stepTime += dt;
            switch (Current)
            {
                case Step.Waiting:
                    waitTime += dt;
                    if (manager.anchored && waitTime >= startDelay && (manager.audioManager == null || manager.audioManager.Ready)) Go(Step.Pull);
                    break;
                case Step.Pull: if (VocalsLevel() <= .35f) Go(Step.Back); break;
                case Step.Back: if (VocalsLevel() >= .85f && stepTime > .6f) Go(Step.Vibe); break;
                case Step.Vibe: if (vibeTried) Go(RoomAvailable ? Step.Room : Step.Done); break;
                case Step.Room: if (manager.room != null || stepTime > 25) Go(Step.Done); break;
                case Step.Done: if (stepTime > 6) Finish(); break;
            }
        }
        // Vocals loudness factor at the playhead: mute / solo and gain automation together.
        float VocalsLevel()
        {
            var song = manager.library.Current.id;
            float gain = manager.automation.EvaluateGain(song, StemId.Vocals, manager.clock.Time, manager.clock.duration, manager.design != null ? manager.design.gainBoundaryFade : .06f);
            return gain * manager.mix.Factor(StemId.Vocals);
        }

        void LateUpdate()
        {
            if (Current == Step.Off || Current == Step.Waiting) return;
            AnimateGhost();
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 6);
            if (vibesHighlight != null)
            {
                if (highlightBase < 0) { vibesBase = vibesHighlight.color; highlightBase = 1; }
                vibesHighlight.color = Current == Step.Vibe ? Color.Lerp(vibesBase, new Color(.15f, .45f, .75f, 1), pulse * .7f) : vibesBase;
            }
            if (Current == Step.Room && toolbox != null && toolbox.passthroughButton != null && toolbox.passthroughButton.targetGraphic != null)
                toolbox.passthroughButton.targetGraphic.color = Color.Lerp(toolbox.idleColor, ghostColor, pulse * .8f); // after the toolbox paints
        }
        void RestoreHighlight() { if (vibesHighlight != null && highlightBase >= 0) vibesHighlight.color = vibesBase; }

        // Thumb and index tips open, pinch together on the vocals ribbon, drag down (or up on step 2), open again.
        void AnimateGhost()
        {
            bool show = (Current == Step.Pull || Current == Step.Back) && manager.ribbons != null && !manager.IsEditing;
            if (ghost == null) return;
            if (ghost.gameObject.activeSelf != show) ghost.gameObject.SetActive(show);
            if (!show) return;
            RibbonView vocals = null;
            foreach (var r in manager.ribbons) if (r != null && r.stem == StemId.Vocals) vocals = r;
            if (vocals == null) return;
            const float cycle = 2.4f; float t = Mathf.Repeat(stepTime, cycle);
            float close = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .7f, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.55f, 1.85f, t)));
            float drag = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.75f, 1.5f, t)) * (t < 2.1f ? 1 : 1 - Mathf.InverseLerp(2.1f, 2.4f, t));
            float alpha = Mathf.InverseLerp(0, .25f, t) * (1 - Mathf.InverseLerp(2.1f, 2.4f, t));
            float scale = manager.workspace != null ? manager.workspace.lossyScale.x : 1;
            var basePoint = vocals.SurfacePoint(0);
            var toHead = Camera.main != null ? (Camera.main.transform.position - basePoint).normalized : -vocals.transform.forward;
            var up = manager.workspace != null ? manager.workspace.up : Vector3.up;
            float direction = Current == Step.Pull ? -1 : 1;
            ghost.position = basePoint + toHead * .03f * scale + up * (direction * .09f * drag - direction * .02f) * scale;
            ghost.rotation = Quaternion.LookRotation(-toHead, up);
            float gap = Mathf.Lerp(.045f, .006f, close) * scale;
            if (ghostThumb != null) ghostThumb.localPosition = new Vector3(0, -gap * .5f / Mathf.Max(.001f, ghost.lossyScale.y), 0);
            if (ghostIndex != null) ghostIndex.localPosition = new Vector3(0, gap * .5f / Mathf.Max(.001f, ghost.lossyScale.y), 0);
            block ??= new MaterialPropertyBlock();
            var c = ghostColor * (1.2f + close * .8f); c.a = alpha;
            if (ghostRenderers != null) foreach (var r in ghostRenderers)
            {
                if (r == null) continue;
                r.enabled = alpha > .02f;
                r.GetPropertyBlock(block); block.SetColor(BaseColorId, c * alpha); block.SetColor(ColorId, c * alpha); r.SetPropertyBlock(block);
            }
        }
    }
}
