using System;
using System.IO;
using System.Linq;
using Oculus.Voice.Dictation;
using RemixTable.Voice;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;
namespace RemixTable.Editor
{
    // Adds the feature set built on top of the seated Remix Table: the PITCH mode button, the MIX row
    // (mute / solo / clear per stem), the TOOLS row (snap, loop, spatial, record, passthrough, voice, reset),
    // the spatial stem orbs, remix persistence, voice input and the detected tempo. Idempotent: running it
    // again updates the same objects. Touches nothing else in the scene.
    public static class RemixFeatureLayout
    {
        public const string MixCanvasName = "MixCanvas", ToolsCanvasName = "ToolsCanvas", FieldName = "SpatialStemField", VoiceName = "VoiceInput";
        public const float MixHeight = .085f;
        public const string OrbMaterialPath = "Assets/RemixTable/Materials/SpatialOrb.mat";
        static readonly StemId[] Order = { StemId.Vocals, StemId.Drums, StemId.Bass, StemId.Melody };
        public static float MixCanvasY(RemixDesign d) => RemixModeLayout.PlaybackCanvasY(d) - RemixModeLayout.Height * .5f - RemixModeLayout.Gap - MixHeight * .5f;
        // TOOLS sits above UNDO / REDO so all the rows under the ribbons stay about editing.
        public static float ToolsCanvasY(RemixDesign d) => RemixModeLayout.HistoryCanvasY(d) + RemixModeLayout.HistorySize.y * .5f + RemixModeLayout.Gap + RemixModeLayout.Height * .5f;

        [MenuItem("Tools/Remix Table/Add Feature Rows (Mix, Tools, Spatial, Voice)")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var transport = RemixSceneBuilder.All<RemixTransport>(scene).Single();
            RemixModeLayout.Build(manager, transport);          // Mode row now carries PITCH too.
            DetectTempo(manager);
            var persistence = manager.GetComponent<RemixPersistence>();
            if (persistence == null) persistence = manager.gameObject.AddComponent<RemixPersistence>();
            persistence.manager = manager; EditorUtility.SetDirty(persistence);
            BuildMix(manager, transport);
            var field = BuildField(manager, transport, scene);
            var toolbox = BuildTools(manager, transport, field, scene);
            BuildVoice(toolbox);
            transport.toolbox = toolbox; EditorUtility.SetDirty(transport);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            RemixModeLayout.Validate();
            Validate();
        }

        static RemixMixControls BuildMix(RemixTableManager manager, RemixTransport transport)
        {
            var d = manager.design; var font = transport.songTitle.font;
            var rect = RemixModeLayout.Row(manager, transport, MixCanvasName, MixCanvasY(d), new Vector2(d.panelWidth, MixHeight));
            var mix = rect.GetComponent<RemixMixControls>(); if (mix == null) mix = rect.gameObject.AddComponent<RemixMixControls>();
            mix.manager = manager;
            mix.muteButtons = new Button[4]; mix.soloButtons = new Button[4]; mix.clearButtons = new Button[4];
            float group = d.panelWidth * 1000 / 4;
            for (int g = 0; g < 4; g++)
            {
                var stem = Order[g]; int i = (int)stem; string name = RemixCommands.StemName(stem); string key = Key(stem);
                float cx = -d.panelWidth * 500 + group * (g + .5f);
                var label = RemixSeatedLayout.Text(rect, key + "Label", name, new Vector2(cx, 26), new Vector2(group - 10, 22), 14, font);
                label.color = Color.Lerp(d.colors[i], Color.white, .35f);
                mix.muteButtons[i] = RemixSeatedLayout.Button(rect, key + "Mute", "M", cx - 58, 52, font, Action<RemixMixControls>(mix, "Mute" + key));
                mix.soloButtons[i] = RemixSeatedLayout.Button(rect, key + "Solo", "S", cx, 52, font, Action<RemixMixControls>(mix, "Solo" + key));
                mix.clearButtons[i] = RemixSeatedLayout.Button(rect, key + "Clear", "CLR", cx + 58, 52, font, Action<RemixMixControls>(mix, "Clear" + key));
                foreach (var b in new[] { mix.muteButtons[i], mix.soloButtons[i], mix.clearButtons[i] })
                    ((RectTransform)b.transform).anchoredPosition = new Vector2(((RectTransform)b.transform).anchoredPosition.x, -12);
            }
            EditorUtility.SetDirty(mix);
            return mix;
        }
        static string Key(StemId stem) => stem == StemId.Melody ? "Other" : stem.ToString();
        // Persistent UnityEvent listeners need a real method; look it up by name so the binding is serialized.
        static UnityEngine.Events.UnityAction Action<T>(T target, string method) where T : Object =>
            (UnityEngine.Events.UnityAction)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, method);

        static RemixToolbox BuildTools(RemixTableManager manager, RemixTransport transport, SpatialStemField field, UnityEngine.SceneManagement.Scene scene)
        {
            var d = manager.design; var font = transport.songTitle.font;
            var rect = RemixModeLayout.Row(manager, transport, ToolsCanvasName, ToolsCanvasY(d));
            var toolbox = rect.GetComponent<RemixToolbox>(); if (toolbox == null) toolbox = rect.gameObject.AddComponent<RemixToolbox>();
            toolbox.manager = manager;
            toolbox.modeSelector = RemixSceneBuilder.All<RemixModeSelector>(scene).Single();
            toolbox.playbackSelector = RemixSceneBuilder.All<RemixPlaybackModeSelector>(scene).Single();
            toolbox.transport = transport; toolbox.spatialField = field;
            string[] names = { "Snap", "Loop", "Spatial", "Record", "Room", "Mic", "Reset" };
            string[] labels = { "SNAP", "LOOP", "SPATIAL", "REC", "ROOM", "MIC", "RESET" };
            string[] methods = { nameof(RemixToolbox.ToggleSnap), nameof(RemixToolbox.ToggleLoop), nameof(RemixToolbox.ToggleSpatial), nameof(RemixToolbox.ToggleRecord),
                                 nameof(RemixToolbox.TogglePassthrough), nameof(RemixToolbox.ToggleVoice), nameof(RemixToolbox.ResetAll) };
            var buttons = new Button[names.Length];
            float slot = d.panelWidth * 1000 / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                float x = -d.panelWidth * 500 + slot * (i + .5f);
                buttons[i] = RemixSeatedLayout.Button(rect, names[i] + "Tool", labels[i], x, slot - 8, font, Action<RemixToolbox>(toolbox, methods[i]));
                ((RectTransform)buttons[i].transform).anchoredPosition = new Vector2(x, 0);
            }
            toolbox.snapButton = buttons[0]; toolbox.loopButton = buttons[1]; toolbox.spatialButton = buttons[2]; toolbox.recordButton = buttons[3];
            toolbox.passthroughButton = buttons[4]; toolbox.voiceButton = buttons[5]; toolbox.resetButton = buttons[6];
            toolbox.recordLabel = buttons[3].transform.Find("Label").GetComponent<TMP_Text>();
            toolbox.voiceLabel = buttons[5].transform.Find("Label").GetComponent<TMP_Text>();
            toolbox.status = null; // Feedback appears in the transport's time readout.
            toolbox.passthrough = BuildPassthrough(scene);
            EditorUtility.SetDirty(toolbox);
            return toolbox;
        }
        static RemixPassthroughToggle BuildPassthrough(UnityEngine.SceneManagement.Scene scene)
        {
            var controller = RemixSceneBuilder.All<PassthroughController>(scene).Single();
            var toggle = controller.GetComponent<RemixPassthroughToggle>(); if (toggle == null) toggle = controller.gameObject.AddComponent<RemixPassthroughToggle>();
            var data = new SerializedObject(toggle);
            data.FindProperty("passthrough").objectReferenceValue = controller;
            var hide = scene.GetRootGameObjects().Where(x => x.name == "Floor").ToArray();
            var list = data.FindProperty("hideInPassthrough"); list.arraySize = hide.Length;
            for (int i = 0; i < hide.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = hide[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            return toggle;
        }

        static SpatialStemField BuildField(RemixTableManager manager, RemixTransport transport, UnityEngine.SceneManagement.Scene scene)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == FieldName);
            if (root == null) { root = new GameObject(FieldName); SceneManager_Move(root, scene); }
            var field = root.GetComponent<SpatialStemField>(); if (field == null) field = root.AddComponent<SpatialStemField>();
            field.manager = manager;
            var material = OrbMaterial();
            var interactionManager = RemixSceneBuilder.All<XRInteractionManager>(scene).First();
            field.orbs = new XRGrabInteractable[4]; field.orbRenderers = new Renderer[4];
            foreach (var stem in Order)
            {
                int i = (int)stem; string key = Key(stem) + "Orb";
                var t = root.transform.Find(key); GameObject orb;
                if (t == null)
                {
                    orb = GameObject.CreatePrimitive(PrimitiveType.Sphere); orb.name = key; orb.transform.SetParent(root.transform, false);
                }
                else orb = t.gameObject;
                orb.layer = 0;
                orb.transform.localScale = Vector3.one * field.orbDiameter;
                orb.transform.position = Vector3.up * 1.2f + Vector3.forward * 1.1f; // Runtime places it around the listener.
                var renderer = orb.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                var body = orb.GetComponent<Rigidbody>(); if (body == null) body = orb.AddComponent<Rigidbody>();
                body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.None;
                var grab = orb.GetComponent<XRGrabInteractable>(); if (grab == null) grab = orb.AddComponent<XRGrabInteractable>();
                grab.interactionManager = interactionManager;
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.trackRotation = false; grab.throwOnDetach = false; grab.useDynamicAttach = true;
                var label = orb.transform.Find("Label");
                var text = label != null ? label.GetComponent<TextMeshPro>() : null;
                if (text == null)
                {
                    var go = new GameObject("Label", typeof(RectTransform)); go.transform.SetParent(orb.transform, false);
                    text = go.AddComponent<TextMeshPro>();
                }
                text.text = RemixCommands.StemName(stem); text.font = transport.songTitle.font; text.fontSize = 8;
                text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
                text.rectTransform.sizeDelta = new Vector2(20, 4);
                text.transform.localPosition = new Vector3(0, .95f, 0); text.transform.localScale = Vector3.one * .35f;
                text.transform.localRotation = Quaternion.identity;
                var billboard = text.GetComponent<SpatialOrbLabel>(); if (billboard == null) billboard = text.gameObject.AddComponent<SpatialOrbLabel>();
                field.orbs[i] = grab; field.orbRenderers[i] = renderer;
                orb.SetActive(false); // Shown when SPATIAL is switched on.
                EditorUtility.SetDirty(orb);
            }
            EditorUtility.SetDirty(field);
            return field;
        }
        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        static Material OrbMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OrbMaterialPath);
            if (material != null) return material;
            Directory.CreateDirectory(Path.GetDirectoryName(OrbMaterialPath));
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            RemixSceneBuilder.Require(shader != null, "URP Unlit shader available for orbs");
            material = new Material(shader) { name = "SpatialOrb" };
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, OrbMaterialPath);
            return material;
        }

        static RemixVoiceInput BuildVoice(RemixToolbox toolbox)
        {
            var scene = toolbox.gameObject.scene;
            var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == VoiceName);
            if (root == null) { root = new GameObject(VoiceName); SceneManager_Move(root, scene); }
            var voice = root.GetComponent<RemixVoiceInput>(); if (voice == null) voice = root.AddComponent<RemixVoiceInput>();
            voice.toolbox = toolbox;
            var child = root.transform.Find("Dictation");
            if (child == null)
            {
                var go = new GameObject("Dictation"); go.SetActive(false); // Inactive before the SDK component wakes up.
                go.transform.SetParent(root.transform, false); child = go.transform;
            }
            child.gameObject.SetActive(false);
            var dictation = child.GetComponent<AppDictationExperience>(); if (dictation == null) dictation = child.gameObject.AddComponent<AppDictationExperience>();
            voice.dictation = dictation;
            toolbox.voice = voice;
            EditorUtility.SetDirty(voice); EditorUtility.SetDirty(toolbox);
            return voice;
        }

        // ---- Tempo ---------------------------------------------------------------------------------------
        // Estimates bpm and first-beat offset from each song's drum stem WAV on disk (streamed clips cannot be
        // read through AudioClip.GetData) and stores them on the song for SNAP.
        public static void DetectTempo(RemixTableManager manager)
        {
            foreach (var song in manager.library.songs)
            {
                var drums = song.stems?.FirstOrDefault(s => s.id == StemId.Drums)?.clip;
                if (drums == null) continue;
                string path = AssetDatabase.GetAssetPath(drums);
                if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || !TryReadWav(path, out var mono, out int rate)) { Debug.LogWarning("TEMPO_SKIPPED " + song.id + " (not a readable PCM WAV)"); continue; }
                var result = TempoDetector.Detect(mono, rate);
                if (result.bpm <= 0) { Debug.LogWarning("TEMPO_FAILED " + song.id); continue; }
                song.bpm = Mathf.Round(result.bpm * 10) / 10; song.beatOffset = result.offset;
                Debug.Log($"TEMPO_DETECTED {song.id} bpm={song.bpm:0.0} offset={song.beatOffset:0.000}s confidence={result.confidence:0.00}");
            }
            EditorUtility.SetDirty(manager.library);
        }
        public static bool TryReadWav(string path, out float[] mono, out int sampleRate)
        {
            mono = null; sampleRate = 0;
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 44 || bytes[0] != 'R' || bytes[8] != 'W') return false;
            int channels = 0, bits = 0, format = 0, position = 12;
            while (position + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, position, 4); int size = BitConverter.ToInt32(bytes, position + 4);
                int body = position + 8;
                if (id == "fmt ") { format = BitConverter.ToInt16(bytes, body); channels = BitConverter.ToInt16(bytes, body + 2); sampleRate = BitConverter.ToInt32(bytes, body + 4); bits = BitConverter.ToInt16(bytes, body + 14); if (format == -2 && size >= 40) format = BitConverter.ToInt16(bytes, body + 24); }
                else if (id == "data")
                {
                    if (channels <= 0 || sampleRate <= 0) return false;
                    size = Mathf.Min(size, bytes.Length - body);
                    int bytesPer = bits / 8, frames = size / (bytesPer * channels);
                    if (bytesPer <= 0 || frames <= 0) return false;
                    mono = new float[frames];
                    for (int f = 0; f < frames; f++)
                    {
                        float sum = 0;
                        for (int c = 0; c < channels; c++)
                        {
                            int o = body + (f * channels + c) * bytesPer;
                            sum += format == 3 && bits == 32 ? BitConverter.ToSingle(bytes, o) :
                                   bits == 16 ? BitConverter.ToInt16(bytes, o) / 32768f :
                                   bits == 24 ? ((bytes[o] | bytes[o + 1] << 8 | (sbyte)bytes[o + 2] << 16) / 8388608f) :
                                   bits == 32 ? BitConverter.ToInt32(bytes, o) / 2147483648f : 0;
                        }
                        mono[f] = sum / channels;
                    }
                    return true;
                }
                position = body + size + (size & 1);
            }
            return false;
        }

        public static void Validate()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var d = manager.design;
            var persistence = RemixSceneBuilder.All<RemixPersistence>(scene).Single();
            RemixSceneBuilder.Require(persistence.manager == manager, "persistence wired");
            var mix = RemixSceneBuilder.All<RemixMixControls>(scene).Single();
            RemixSceneBuilder.Require(mix.manager == manager && mix.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace && mix.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null, "mix row is pokeable world UI");
            foreach (var stem in Order)
            {
                int i = (int)stem; string key = Key(stem);
                foreach (var (button, method) in new[] { (mix.muteButtons[i], "Mute" + key), (mix.soloButtons[i], "Solo" + key), (mix.clearButtons[i], "Clear" + key) })
                    RemixSceneBuilder.Require(button != null && button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == mix && button.onClick.GetPersistentMethodName(0) == method, "mix binding " + method);
            }
            var tools = RemixSceneBuilder.All<RemixToolbox>(scene).Single();
            RemixSceneBuilder.Require(tools.manager == manager && tools.modeSelector != null && tools.playbackSelector != null && tools.transport != null && tools.spatialField != null, "toolbox wired");
            RemixSceneBuilder.Require(tools.passthrough is IRemixToggle && tools.voice is IRemixToggle, "toolbox passthrough and voice toggles");
            foreach (var (button, method) in new[] { (tools.snapButton, nameof(RemixToolbox.ToggleSnap)), (tools.loopButton, nameof(RemixToolbox.ToggleLoop)), (tools.spatialButton, nameof(RemixToolbox.ToggleSpatial)),
                (tools.recordButton, nameof(RemixToolbox.ToggleRecord)), (tools.passthroughButton, nameof(RemixToolbox.TogglePassthrough)), (tools.voiceButton, nameof(RemixToolbox.ToggleVoice)), (tools.resetButton, nameof(RemixToolbox.ResetAll)) })
                RemixSceneBuilder.Require(button != null && button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == tools && button.onClick.GetPersistentMethodName(0) == method, "tool binding " + method);
            RemixSceneBuilder.Require(tools.transform.localPosition.y - RemixModeLayout.Height * .5f > RemixModeLayout.HistoryCanvasY(d) + RemixModeLayout.HistorySize.y * .5f, "tools row above undo/redo");
            RemixSceneBuilder.Require(mix.transform.localPosition.y + MixHeight * .5f < RemixModeLayout.PlaybackCanvasY(d) - RemixModeLayout.Height * .5f, "mix row below playback row");
            var field = RemixSceneBuilder.All<SpatialStemField>(scene).Single();
            RemixSceneBuilder.Require(field.manager == manager && field.orbs.All(o => o != null && !o.gameObject.activeSelf && o.GetComponent<Rigidbody>().isKinematic && o.interactionManager != null), "spatial orbs ready and hidden");
            var voice = RemixSceneBuilder.All<RemixVoiceInput>(scene).Single();
            RemixSceneBuilder.Require(voice.toolbox == tools && voice.dictation != null && !voice.dictation.gameObject.activeSelf, "voice input wired and dormant");
            var transport = RemixSceneBuilder.All<RemixTransport>(scene).Single();
            RemixSceneBuilder.Require(transport.toolbox == tools, "transport shows toolbox feedback");
            RemixSceneBuilder.Require(manager.library.songs.All(s => BeatGrid.Valid(s.bpm)), "every song has a tempo for SNAP");
            Debug.Log("REMIX_FEATURE_VALIDATION_PASS");
        }
    }
}
