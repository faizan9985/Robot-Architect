using UnityEngine;
using UnityEngine.InputSystem;

namespace RemixTable.Dev
{
    /// <summary>
    /// Mouse and keyboard mixing for the desktop sandbox (no headset needed). Uses the Input System, because the
    /// project's Active Input Handling is "Input System Package (New)". Disabled in release builds.
    /// Click the Game view first so it has keyboard focus.
    ///   Left-drag orb: move across the desk (volume) · Shift + drag: lift / lower (reverb)
    ///   Hover + wheel: twist (filter) · Hover + M: mute · Hover + hold S: solo
    ///   Space: drop · R: reset layout · P: pause / resume · T: toggle spatializer
    /// Every change goes through StemOrb → MixState, exactly like hand input will.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Remix Table/Dev/Desktop Dev Input")]
    public sealed class DesktopDevInput : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] StemPlayer player;
        [SerializeField] DropController drop;
        [SerializeField] DeskFrame desk;
        [SerializeField] StemAnalyzer analyzer;
        [SerializeField] StemOrb[] orbs = new StemOrb[0];

        [Header("Feel")]
        [SerializeField] float twistDegreesPerNotch = 7.5f;
        [SerializeField] float liftMetresPerPixel = 0.0015f;
        [SerializeField] float maxLiftMetres = 0.45f;
        [SerializeField] float deskMargin = 0.04f;
        [SerializeField] LayerMask orbLayers = ~0;
        [SerializeField] bool showReadout = true;

        Vector3[] homePositions = new Vector3[0];
        StemOrb hovered;
        StemOrb dragged;
        StemOrb soloing;
        Collider lastCollider;
        StemOrb lastColliderOrb;
        Plane dragPlane;
        Vector3 dragOffset;
        bool dragLifting;
        GUIStyle readoutStyle;

        public void Configure(Camera cam, StemPlayer stemPlayer, DropController dropController, DeskFrame deskFrame,
            StemAnalyzer stemAnalyzer, StemOrb[] stemOrbs)
        {
            viewCamera = cam;
            player = stemPlayer;
            drop = dropController;
            desk = deskFrame;
            analyzer = stemAnalyzer;
            orbs = stemOrbs;
        }

        void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            enabled = false;
            return;
#else
            if (viewCamera == null) viewCamera = Camera.main;
            homePositions = new Vector3[orbs.Length];
            for (int i = 0; i < orbs.Length; i++)
                if (orbs[i] != null) homePositions[i] = orbs[i].transform.position;
#endif
        }

        void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null || viewCamera == null) return;

            Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            hovered = dragged != null ? dragged : RaycastOrb(ray);
            bool shift = keyboard.shiftKey.isPressed;

            if (dragged == null && hovered != null && mouse.leftButton.wasPressedThisFrame) BeginDrag(hovered, ray, shift);
            if (dragged != null)
            {
                if (mouse.leftButton.isPressed) Drag(ray, mouse.delta.ReadValue(), shift);
                else EndDrag();
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (hovered != null && scroll != 0f) hovered.AddTwist(Mathf.Sign(scroll) * twistDegreesPerNotch);

            if (hovered != null && keyboard.mKey.wasPressedThisFrame) hovered.ToggleMute();
            if (hovered != null && keyboard.sKey.wasPressedThisFrame)
            {
                if (soloing != null) soloing.SetSolo(false);
                soloing = hovered;
                soloing.SetSolo(true);
            }
            if (soloing != null && keyboard.sKey.wasReleasedThisFrame)
            {
                soloing.SetSolo(false);
                soloing = null;
            }

            if (keyboard.spaceKey.wasPressedThisFrame && drop != null) drop.TriggerDrop();
            if (keyboard.rKey.wasPressedThisFrame) ResetLayout();
            if (keyboard.pKey.wasPressedThisFrame && player != null) player.TogglePause();
            if (keyboard.tKey.wasPressedThisFrame && player != null) player.Spatialize = !player.Spatialize;
        }

        StemOrb RaycastOrb(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, 20f, orbLayers, QueryTriggerInteraction.Collide)) return null;
            if (hit.collider != lastCollider)
            {
                lastCollider = hit.collider;
                lastColliderOrb = hit.collider.GetComponentInParent<StemOrb>();
            }
            return lastColliderOrb;
        }

        void BeginDrag(StemOrb orb, Ray ray, bool shift)
        {
            if (!orb.BeginHold()) return;
            dragged = orb;
            ResetDragPlane(ray, shift);
        }

        void ResetDragPlane(Ray ray, bool shift)
        {
            dragLifting = shift;
            Vector3 up = desk != null ? desk.Up : Vector3.up;
            Vector3 position = dragged.transform.position;
            dragPlane = new Plane(up, position);
            dragOffset = dragPlane.Raycast(ray, out float enter) ? position - ray.GetPoint(enter) : Vector3.zero;
        }

        void Drag(Ray ray, Vector2 mouseDelta, bool shift)
        {
            if (shift != dragLifting) ResetDragPlane(ray, shift);
            Transform t = dragged.transform;

            if (shift)
            {
                if (desk == null) return;
                float height = desk.HeightAbove(t.position) - dragged.RestHeight;
                float newHeight = Mathf.Clamp(height + mouseDelta.y * liftMetresPerPixel, 0f, maxLiftMetres);
                t.position += desk.Up * (newHeight - height);
                return;
            }

            if (!dragPlane.Raycast(ray, out float enter)) return;
            Vector3 target = ray.GetPoint(enter) + dragOffset;
            if (desk != null) target = desk.ClampToDesk(target, deskMargin);
            t.position = target;
        }

        void EndDrag()
        {
            if (dragged != null) dragged.EndHold();
            dragged = null;
        }

        void ResetLayout()
        {
            EndDrag();
            if (soloing != null)
            {
                soloing.SetSolo(false);
                soloing = null;
            }
            for (int i = 0; i < orbs.Length && i < homePositions.Length; i++)
                if (orbs[i] != null) orbs[i].transform.position = homePositions[i];
            if (player != null && player.Mix != null) player.Mix.ResetStems();
        }

        void OnDisable() => EndDrag();

        // ---------------------------------------------------------------- readout (dev only; allocates, never ships)

        void OnGUI()
        {
            if (!showReadout || player == null || !player.IsLoaded) return;
            readoutStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, 13),
                fontSize = 13,
                richText = true,
            };

            MixState mix = player.Mix;
            MusicClock clock = player.Clock;
            DriftReport drift = player.Drift;

            GUILayout.BeginArea(new Rect(10, 10, 640, 330), GUI.skin.box);
            string clockText = clock.HasReachedStart
                ? $"bar {clock.BarInLoop + 1}:{clock.BeatInBar + 1}  ({clock.Timing.Bpm:F1} BPM, {clock.Timing.LoopBars} bars)"
                : "starting…";
            GUILayout.Label($"<b>Remix Table sandbox</b>   {clockText}", readoutStyle);

            string driftText = !drift.Supported ? "n/a"
                : !drift.Active ? "measuring…"
                : drift.InSync ? $"<color=#7CFC00>OK</color> (now {drift.LastMaxSamples}, worst {drift.WorstSamples} smp)"
                : $"<color=#FF5050>DRIFT {drift.LastMaxSamples} smp</color> (worst {drift.WorstSamples})";
            string state = player.IsPaused ? "<color=#FFD700>PAUSED</color>" : "playing";
            GUILayout.Label($"Sync: {driftText}   Spatializer: {(player.Spatialize ? "ON" : "off")}   " +
                            $"Drop: {mix.DropPhase}   {state}", readoutStyle);

            GUILayout.Label("<b>stem        dist    gain   out    filter  (applied)  reverb  level   M S H</b>", readoutStyle);
            for (int i = 0; i < mix.StemCount; i++)
            {
                StemMix m = mix.GetStem(i);
                StemOrb orb = FindOrb(i);
                string dist = orb != null ? $"{orb.DistanceToListener:F2}m" : "  -  ";
                float level = analyzer != null ? analyzer.GetLevel01(i) : 0f;
                GUILayout.Label(
                    $"<color=#{ColorUtility.ToHtmlStringRGB(player.GetDefinition(i).Color)}>{player.GetDefinition(i).Name,-10}</color> " +
                    $"{dist,6}  {m.gain01,5:F2}  {player.GetAppliedGain(i),5:F2}  {m.filter,6:+0.00;-0.00}  ({player.GetAppliedFilter(i),6:+0.00;-0.00})  " +
                    $"{m.reverb01,5:F2}   {Bar(level)}  {(m.muted ? "M" : ".")} {(m.soloed ? "S" : ".")} {(m.IsHeld ? "H" : ".")}",
                    readoutStyle);
            }

            GUILayout.Space(6);
            GUILayout.Label("drag = move (volume) · Shift+drag = lift (reverb) · wheel = twist (filter) · M = mute · hold S = solo\n" +
                            "Space = drop · R = reset · P = pause · T = spatializer on/off   (click the Game view first)", readoutStyle);
            GUILayout.EndArea();
        }

        StemOrb FindOrb(int stem)
        {
            for (int i = 0; i < orbs.Length; i++)
                if (orbs[i] != null && orbs[i].StemIndex == stem) return orbs[i];
            return null;
        }

        static string Bar(float level01)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(level01 * 8f), 0, 8);
            return new string('|', filled).PadRight(8, '·');
        }
    }
}
