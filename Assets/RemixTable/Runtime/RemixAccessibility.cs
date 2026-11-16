using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // ACCESS panel:
    //  - HIGH CONTRAST: black panels and buttons with white outlines, white bold text, a strong blue for the
    //    selected state, brighter room glow. Text-only feedback already doubles as captions.
    //  - CLOSER: brings the whole table toward the listener (shorter reach, rows higher) for limited reach,
    //    wheelchairs or a cramped seat. Works in and out of ROOM mode.
    // Every hand interaction also has a voice route (MIC) and one-hand alternatives (SNAP + one-hand pinch
    // makes a beat region; VIBES are single taps). Both settings are remembered between sessions.
    public sealed class RemixAccessibility : MonoBehaviour
    {
        public RemixTableManager manager;
        public RemixToolbox toolbox;
        public RemixRoomPlacer placer;
        public RemixRoomVisuals roomVisuals;
        public Button contrastButton, reachButton;
        [Range(.6f, .95f)] public float closerScale = .8f;
        public Color contrastIdle = new Color(0, 0, 0, 1), contrastSelected = new Color(0, .45f, 1, 1), contrastMute = new Color(.85f, .1f, .1f, 1), contrastSolo = new Color(1, .75f, 0, 1);
        public Color idleColor = new Color(.07f, .11f, .15f, 1), selectedColor = new Color(.13f, .42f, .62f, 1);
        const string ContrastKey = "remix.access.contrast", CloserKey = "remix.access.closer";

        public bool HighContrast { get; private set; }
        public bool Closer { get; private set; }

        readonly Dictionary<Graphic, Color> colors = new Dictionary<Graphic, Color>();
        readonly Dictionary<TMP_Text, FontStyles> styles = new Dictionary<TMP_Text, FontStyles>();
        readonly Dictionary<MonoBehaviour, Color[]> painters = new Dictionary<MonoBehaviour, Color[]>();
        readonly List<Outline> outlines = new List<Outline>();

        void Start()
        {
            if (Read(ContrastKey)) SetContrast(true);
            if (Read(CloserKey))
            {
                Closer = true; if (manager != null) manager.reachScale = closerScale; // Applied when the table anchors.
                if (manager != null && manager.anchored) Rescale();
            }
            Paint();
        }
        static bool Read(string key) { try { return PlayerPrefs.GetInt(key, 0) == 1; } catch { return false; } }
        static void Write(string key, bool on) { try { PlayerPrefs.SetInt(key, on ? 1 : 0); PlayerPrefs.Save(); } catch { } }

        public void ToggleContrast() { SetContrast(!HighContrast); Write(ContrastKey, HighContrast); if (toolbox != null) toolbox.Say(HighContrast ? "HIGH CONTRAST ON" : "HIGH CONTRAST OFF"); }
        public void ToggleReach() { SetCloser(!Closer); Write(CloserKey, Closer); if (toolbox != null) toolbox.Say(Closer ? "TABLE MOVED CLOSER" : "TABLE AT NORMAL DISTANCE"); }

        public void SetCloser(bool on)
        {
            Closer = on;
            if (manager == null) return;
            manager.reachScale = on ? closerScale : 1;
            Rescale();
            Paint();
        }
        void Rescale()
        {
            if (placer != null) placer.ApplyReach();
            else manager.SetWorkspaceScale(manager.reachScale);
        }

        public void SetContrast(bool on)
        {
            if (on == HighContrast || manager == null) { Paint(); return; }
            HighContrast = on;
            if (on)
            {
                colors.Clear(); styles.Clear();
                foreach (var g in Graphics())
                {
                    if (g == null) continue;
                    if (g is TMP_Text text)
                    {
                        colors[g] = g.color; styles[text] = text.fontStyle;
                        g.color = Color.white; text.fontStyle |= FontStyles.Bold;
                    }
                    else if (g is Image image && !image.raycastTarget && image.sprite == null && !g.name.EndsWith("Fill"))
                    {
                        colors[g] = g.color; g.color = Color.black;                // panel backgrounds
                    }
                    else if (g is Image button && button.raycastTarget && button.GetComponent<Button>() != null)
                    {
                        var outline = button.GetComponent<Outline>(); if (outline == null) outline = button.gameObject.AddComponent<Outline>();
                        outline.effectColor = Color.white; outline.effectDistance = new Vector2(2.5f, -2.5f); outline.enabled = true;
                        outlines.Add(outline);
                    }
                }
            }
            else
            {
                foreach (var pair in colors) if (pair.Key != null) pair.Key.color = pair.Value;
                foreach (var pair in styles) if (pair.Key != null) pair.Key.fontStyle = pair.Value;
                foreach (var o in outlines) if (o != null) o.enabled = false;
                colors.Clear(); styles.Clear(); outlines.Clear();
            }
            SetPainterColors(on);
            if (roomVisuals != null) roomVisuals.intensityScale = on ? 1.45f : 1;
            Paint();
        }

        IEnumerable<Graphic> Graphics()
        {
            if (manager.workspace != null) foreach (var g in manager.workspace.GetComponentsInChildren<Graphic>(true)) yield return g;
        }

        // The rows that tint their own buttons (selected / muted / soloed) get high-contrast palettes, then are
        // toggled off and on so they repaint immediately.
        void SetPainterColors(bool on)
        {
            foreach (var p in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                switch (p)
                {
                    case RemixToolbox t: Swap(p, on, () => new[] { t.idleColor, t.selectedColor }, c => { t.idleColor = c[0]; t.selectedColor = c[1]; }); break;
                    case RemixModeSelector m: Swap(p, on, () => new[] { m.idleColor, m.selectedColor }, c => { m.idleColor = c[0]; m.selectedColor = c[1]; }); break;
                    case RemixPlaybackModeSelector m: Swap(p, on, () => new[] { m.idleColor, m.selectedColor }, c => { m.idleColor = c[0]; m.selectedColor = c[1]; }); break;
                    case RemixVibes v: Swap(p, on, () => new[] { v.idleColor, v.selectedColor }, c => { v.idleColor = c[0]; v.selectedColor = c[1]; }); break;
                    case RemixMixControls m:
                        if (on) { painters[p] = new[] { m.idleColor, m.muteColor, m.soloColor }; m.idleColor = contrastIdle; m.muteColor = contrastMute; m.soloColor = contrastSolo; }
                        else if (painters.TryGetValue(p, out var c)) { m.idleColor = c[0]; m.muteColor = c[1]; m.soloColor = c[2]; }
                        break;
                    default: continue;
                }
                if (p.isActiveAndEnabled) { p.enabled = false; p.enabled = true; }
            }
            if (!on) painters.Clear();
        }
        void Swap(MonoBehaviour p, bool on, System.Func<Color[]> get, System.Action<Color[]> set)
        {
            if (on) { painters[p] = get(); set(new[] { contrastIdle, contrastSelected }); }
            else if (painters.TryGetValue(p, out var c)) set(c);
        }

        void Paint()
        {
            var idle = HighContrast ? contrastIdle : idleColor; var selected = HighContrast ? contrastSelected : selectedColor;
            if (contrastButton != null && contrastButton.targetGraphic != null) contrastButton.targetGraphic.color = HighContrast ? selected : idle;
            if (reachButton != null && reachButton.targetGraphic != null) reachButton.targetGraphic.color = Closer ? selected : idle;
        }
    }
}
