# Minimal rounded icon-based spatial UI

The existing Remix Table controls now use a shared original icon atlas, rounded
dark surfaces, violet outlines, and delayed contextual names. The animated
horizontal navigation, UI hit rectangles, original actions, and musical visuals
are preserved. No controls or functions were removed or added.

## Inventory and complete mapping

The scene contains eight world-space canvases: TransportCanvas, HistoryCanvas
(animated navigation), ToolsCanvas, PlaybackModeCanvas, Hand Menu Canvas,
MixCanvas, Palm Launcher Canvas, and ModeCanvas. The twelve Speed/Zoom presets
are in the existing two masked pop-outs, not extra canvases.

There are **45 buttons**, one palm passthrough toggle, and one master-volume
slider. Before this change, Undo/Redo used original code-drawn icons and the other
43 buttons used TextMeshPro labels. The existing history artwork is reused.

| Existing control(s) | Visible icon or retained text | Original action |
| --- | --- | --- |
| PreviousSong | Previous-track triangle/bar | PreviousSong |
| PlayPause | Play triangle / Pause bars | TogglePlayback |
| NextSong | Next-track triangle/bar | NextSong |
| Undo / Redo | Existing curved arrows, packed into atlas | Undo / Redo |
| Speed | Current numeric multiplier | Toggle Speed pop-out |
| Preset0–Preset7 | 0.25x, 0.5x, 0.75x, 1x, 1.25x, 1.5x, 1.75x, 2x | SelectPreset(0–7) |
| Zoom | Current numeric song-time window | ToggleOptions |
| Detail / Normal / Wide / Overview | 4s / 8s / 16s / 32s | Original zoom preset actions |
| VolumeMode | Speaker with sound waves | SelectVolume (Gain) |
| FilterMode | Equalizer sliders | SelectFilter |
| PitchMode | Musical notes | SelectPitch |
| EditMode | Pencil plus concise “Edit” caption | SelectEdit |
| LiveMode | Play/broadcast plus concise “Live” caption | SelectLive |
| Vocals/Drums/Bass/Other Mute (4) | Muted speaker | Original per-stem Mute action |
| Vocals/Drums/Bass/Other Solo (4) | Headphones | Original per-stem Solo action |
| Vocals/Drums/Bass/Other Clear (4) | Eraser | Original per-stem Clear action |
| SnapTool | Magnet | ToggleSnap |
| LoopTool | Repeat arrows | ToggleLoop |
| SpatialTool | Spatial source / satellite circles | ToggleSpatial |
| RecordTool | Record circle / Stop square; numeric duration while recording | ToggleRecord |
| RoomTool | Environment/house with view line | TogglePassthrough |
| MicTool | Microphone | ToggleVoice |
| ResetTool | Reset arrow plus “Reset” caption | ResetAll |
| Open Menu Button | Settings sliders | Existing runtime OpenMenu listener |
| Close Button | Close cross | Existing runtime CloseMenu listener |
| Palm passthrough toggle | Existing text and check state | Original passthrough listener |
| Master-volume slider | Existing numeric percentage | Original AudioListener volume listener |

There is no standalone Save, Stop-transport, Tutorial, or Tools-expander button in
this interface. Autosave, the existing palm instructions, recording stop, and the
Tools row retain their original behavior. Save and question-circle artwork are
included in the small atlas, without introducing new actions. Important error and
status messages, song titles, time readouts, pitch/filter/gain values, numeric
presets, stem names, palm guidance, and recording duration remain available.

## Visual implementation

`RemixUiPresentation` is a presentation-only component on the existing manager.
`RemixIconButton` styles each existing Button and observes the authoritative
mode, audio, mixer, and tool state. It never replaces or adds an action listener.
Original TMP label objects and serialized references are retained; icon-only
labels stop rendering while their existing controllers may continue updating
their values. Recording duration is shown in a separate compact numeric caption.

The atlas is one 1024x512 RGBA texture containing **29 sprites**, including the
9-sliced surface and border. New glyphs are original 24-unit vector-style
geometry rasterized offline by `Tools/GenerateRemixUiAtlas.py` with Pillow. The
existing project-generated Undo/Redo images are reused. No external artwork,
Unicode/emoji icons, new Unity packages, or runtime texture generation are used.
The atlas imports explicitly as Texture2D, with FullRect sprite meshes, sRGB,
bilinear filtering, no mipmaps, no CPU readability, and uncompressed UI detail.

Buttons and panel backgrounds share the atlas, the default UI material, a
consistent corner treatment, dark translucent fill, and restrained violet
borders. Existing disabled panel backgrounds keep their decorative borders
disabled, including the retired navigation background. Existing masks are
preserved; no new Canvas or mask is introduced. The slider thumb is also rounded.

Idle, hover/focus, pressed, selected, and disabled states use UI-specific colors
and outlines. A selected check provides a persistent cue independent of color.
Press feedback lasts approximately 100 ms after release/submit, without scaling
or moving the control. Disabled icons and numeric labels dim. The full original
rectangular hit area is retained despite transparent rounded corners; alpha hit
testing stays at its original zero threshold.

All **806 original scene object/component IDs** are retained. The 45 original
button action blocks match the original scene exactly, and all 45 actual button
RectTransforms retain their original position, anchors, pivot, scale, and size.
Only decorative children, presentation components, sprites, label visibility,
and UI colors/transitions were changed. All eight original XR graphic raycasters
remain in place. Hand tracking, gestures, XR rigs, DSP, and navigation algorithms
are unchanged. The approved waveform and sphere 5x-hover/10x-grab contributions
are unchanged, verified by protected-file hashes.

## Tooltips and accessibility

One shared tooltip appears after **600 ms** of intentional pointer hover or
EventSystem focus. It contains the concise semantic name. A new candidate hides
the previous bubble immediately. Pointer down/click, submit, exit, deselection,
disabled controls, parent hiding, app focus loss, or pause dismiss it. Hover does
not activate any function.

Workspace tooltips occupy a stable help strip above the topmost Tools controls,
clear of the ribbons and other buttons. Palm hints stay outside their existing
panel edge. The bubble is reparented into existing canvases, has no collider,
has no raycastable graphics, and uses a non-interactable CanvasGroup with
blocksRaycasts=false. It never creates another gesture target.

Semantic GameObject names and original reference identities are preserved. Each
button also stores a human-readable semanticName. Ambiguous/destructive actions
receive explicit names (for example “Clear Bass” and “Reset All (Undo restores)”).
Edit, Live, and Reset keep concise captions; original confirmation/error behavior
is unchanged. Selected checks, play/pause and record/stop glyph changes, and
numeric readouts ensure state is not communicated through color alone.

## Validation

- Unity 6000.3.23f1 compilation passed.
- **Edit Mode: 355 passed, 0 failed, 0 skipped**
  (`Logs/IconUi/EditModeFinal2.xml`). Seven focused UI tests cover retained
  controls/actions, numerics, animation, atlas/rounded targets, tooltip lifecycle,
  pointer actions, disabled states, idempotency, and allocations. Existing
  assertions were not weakened.
- **Play Mode: 32 passed, 0 failed, 0 skipped**
  (`Logs/IconUi/PlayModeFinal2.xml`). The new saved-scene test exercises original
  pointer-click actions, mixer feedback, tooltips, speed/zoom presets, and targets.
- **0 managed bytes** allocated across 30 warmed full presentation-update passes
  in the added allocation test.
- Scene/UI validation passed for all 45 buttons, 29 atlas sprites, eight existing
  world-space XR graphic raycasters, the default UI material/shader, and missing
  scene scripts. **1,147 unique Unity metadata GUIDs** were checked.
- Canonical scene checks preserve all 806 original IDs, all 45 original action
  blocks, all 45 button target RectTransforms, and 67 non-UI transform/audio/
  rigidbody/collider components.
- Desktop render previews inspected: `Logs/IconUi/Workspace.png`,
  `Logs/IconUi/Palm.png`, and `Logs/IconUi/HoverTooltip.png`. The hint strip is
  above the controls, not over the waveforms. These are editor previews, not
  physical Quest captures.

- **Quest APK build succeeded** with the existing Meta Quest profile, Android,
  OpenXR, URP, and Vulkan. Output: `Builds/RemixTable-IconSpatialUI.apk`
  (**130,032,478 bytes**). Packed-asset validation includes the atlas and all
  eight existing holographic shader/material assets. APK signature verification
  passed.
- APK SHA-256:
  `0d7254a849fdb0109907840c0c7f111632179dc78818ceb7060dcba722b91243`.
- The isolated build used the verified tracked blank DevAgent template with the
  existing credential guard active. A private credential audit scanned **443 APK
  entries and 471 decoded UnityFS blocks (61,617,084 bytes)** and confirmed the
  SDK-restored development token was absent. Isolated settings were restored to
  the blank template afterward. Local credential settings were never copied,
  modified, or exposed.
- Protected hashes confirm the musical renderer/interaction/audio scripts,
  shaders, and four pre-existing local settings modifications are unchanged.
  Feature whitespace, staged scope/credential-pattern checks, and matching of
  staged assets to the validated build copy passed.

Generated previews, logs, isolated builds, and APKs are excluded from Git.

## Files

- New runtime components and `.meta`: `RemixUiPresentation.cs`,
  `RemixIconButton.cs`, `RemixUiTooltip.cs` under `Assets/RemixTable/Runtime`.
- New editor layout/reference/preview utility and `.meta`:
  `Assets/RemixTable/Editor/RemixUiLayout.cs`.
- New atlas and importer metadata: `Assets/Resources/RemixUiAtlas.png`.
- New tests and `.meta`: `Assets/RemixTable/Tests/EditMode/RemixIconUiTests.cs`
  and `Assets/RemixTable/Tests/PlayMode/RemixIconUiPlayTests.cs`.
- New offline original-art source: `Tools/GenerateRemixUiAtlas.py`.
- Modified: `Assets/RemixTable/Scenes/RemixTable.unity` and
  `Assets/RemixTable/Editor/VocalsBuildValidation.cs` (UI and packed-atlas checks).
- This report: `MINIMAL_ICON_UI.md`.

## Remaining Quest checks

No physical Quest validation was performed for this milestone. Check icon
recognition and readable size at seated distance, rounded-edge contrast in
passthrough/dark environments, both-hand/controller poke and ray interaction,
hover/focus timing and tooltip placement while moving the palm menu, recording
duration, disabled states, selected checks, the animated preset pop-outs, and
GPU/CPU frame time with the full workspace visible. The desktop previews and
automated checks do not establish headset performance or on-device legibility.
