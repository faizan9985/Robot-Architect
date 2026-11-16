# Adjustable timeline zoom

The current audio-derived holographic renderer retains this zoom and interaction path. See [HOLOGRAPHIC_RIBBONS.md](HOLOGRAPHIC_RIBBONS.md) for the smooth envelope interpolation and current geometry budget.

Waveform rendering now uses zoom-adaptive audio peak caches and 512 fixed columns per lane. See [AUDIO_WAVEFORMS.md](AUDIO_WAVEFORMS.md) for current geometry and memory budgets; earlier 128-segment rendering references below are historical. The current inline navigation toolbar replaces the historical palm preset UI; waveform mapping and zoom state remain shared.

The saved RemixTable scene starts at Normal: eight seconds across the arc, four seconds on each side of the current audio timestamp. Detail = 4 seconds, Normal = 8, Wide = 16, Overview = 32. These values change the time mapping only. Radius (0.8 m), arc angle (180 degrees), lane spacing (9 cm), world anchor, transport and central playhead stay fixed.

## Shared mapping and smoothing

RemixTableManager owns one serializable TimelineZoom instance. VisibleSeconds is the continuous displayed window; TargetSeconds is the clamped request. Requests use the range 4?32 seconds and reject nonfinite values. The runtime value belongs to the manager, so gestures never mutate the shared design asset or automation data. The earlier design pastSeconds/futureSeconds fields remain for legacy authoring compatibility; curved runtime mapping uses manager.zoom exclusively. The flat compatibility path retains design.visibleSeconds.

For angle theta and total arc angle A, offset = theta / A * VisibleSeconds. Timestamp = (audio playhead + offset) modulo song duration. RibbonView routes mesh phase, highlighting, hit testing and region endpoints through this mapping. The physical position still comes from RemixDesign.ArcPoint. TimelineZoom also supplies AngleToTimestamp, TimestampToAngle and OffsetToAngle. The inverse timestamp mapping chooses the nearest loop occurrence; when a window is longer than a very short song, a timestamp has multiple visible occurrences, so use a known signed time offset to identify another occurrence.

The manager advances zoom once in LateUpdate, before ribbon mesh updates, using an exponential transition with a default 0.16-second time constant. Gesture processing runs first. If a ribbon is selected or the audio manager has an editing pause owner, interpolation freezes at its current displayed value. A menu selection changes the target only and is marked queued until the final owner releases. An in-progress transition also freezes immediately on selection. Existing one-hand gain anchors, two-hand boundaries, whole-stem operations, pause ownership and absolute automation timestamps are preserved. Audio never reads or depends on zoom.

## Two-hand gesture

Use the existing XR Hands pinch input in RibbonInteractionManager. Both hands must be tracked and pinching in empty, forward workspace, with at least 12 cm separation. Ribbon acquisition is tested first for both hands: either hand on a ribbon starts the original edit/region path. Existing selected ribbons or any audio editing owner prevent zoom. An open palm menu or an opening/open/closing navigation menu prevents acquisition as well. Opening a toolbar menu cancels any existing zoom session with the normal release latch.

The seated acquisition volume is relative to the stable workspace: x within ?0.65 m, y within ?0.5 m, z from -radius + 0.12 m to +0.18 m. Hands can be comfortably closer to the body than the wave surface; there is no need to reach either arc end. After acquisition, ZoomPending captures the initial hand separation and displayed window, and locks out ribbon acquisition. A 2.5 cm separation change activates Zooming. Default sensitivity is 1:

    requestedSeconds = initialSeconds * (initialSeparation / currentSeparation)^sensitivity

Spreading to twice the initial separation halves the time window; contraction doubles it. The same shared interpolation smooths both gesture and menu requests. Threshold, sensitivity and smoothing are Inspector settings in RemixDesign. The numeric range is always clamped to 4?32 seconds.

Release of either pinch, tracking loss, menu opening, external editing ownership, application focus/pause loss or component disable cancels a zoom session. It keeps the last requested window. Both hands must subsequently be tracked and observed released before another gesture can start. Tracking loss alone does not count as release. Pending and active zoom sessions never switch into ribbon editing mid-gesture and never acquire an audio pause owner.

## Inline navigation toolbar

The existing top HistoryCanvas contains Undo, Redo, Playback Speed and Timeline Zoom, in that order. `RemixNavigationToolbar` coordinates the two inline reveal slots. Speed is a two-row, four-column block; Zoom is a single four-button row. Opening Speed reserves width and pushes Zoom right. Closing returns every displaced control to its collapsed position. Header/preset targets stay 50 × 40 UI units at the existing 0.001 canvas scale. Both menus share the toolbar centerline; there is no vertical animation, floating overlay, dropdown, scrolling, layout group or content-size fitter.

The four persistent callbacks still target the single `TimelineZoomUI` on transport: Detail() requests 4, Normal() 8, Wide() 16, Overview() 32. ToggleOptions() now toggles the toolbar Zoom slot. The adapter uses only manager.zoom; the button reads VisibleSeconds and shows compact tenths (for example 11.4s). Presets highlight only when the current value is within 0.025 seconds. Continuous gestures never snap to presets. Transport feedback retains queued/MIN/MAX states. Audio rate is independent of song-time window length.

Each menu uses a 230-unit-wide full-size content block inside its own animated RectMask2D viewport. Width and alpha follow cubic ease-out over 280 ms while opening, smoothstep ease-in-out over 250 ms while closing. Only one menu is targeted open. Switching reverses from current rendered fractions, with neighboring positions derived from both currently occupied widths. Partially revealed and outgoing options reject input. The padded bar is 23 cm collapsed and 46.6 cm with either menu fully open; horizontal offset clamps the current bar to the configured seated width during transitions. Y and target dimensions remain fixed.

The old palm TimelineZoom and TimelineZoomOptions objects and their bindings are removed. Original passthrough/volume/close controls remain, and gesture instructions regain their full original area instead of being hidden behind zoom options. No extra Canvas, EventSystem or XR rig is added.

## Validation and performance

Tests cover default and limits, each preset's bidirectional mapping, loop seams, shared mapping on all four stems, unchanged physical placement and automation, threshold and gesture direction, ribbon priority, tracking loss, release latch, disable cancellation, menu bindings, smooth transitions, edit deferral, live audio during zoom and audible-gain source values for future edits after zooming. The mesh object and vertex count remain unchanged. A 10,000-iteration interpolation/mapping test asserts zero managed allocation in that hot path. Existing 128-segment ribbon buffers and analytical proximity checks are reused, with no collider rebuilding or added triangles.

RemixZoomValidation supports validation in an already-open editor. Write scene, edit or play to Logs/RemixZoomValidation.request after refreshing assets; it refuses to replace unsaved open scenes. Scene mode validates the saved scene without rebuilding it; the Tools / Remix Table / Apply Navigation Toolbar command rebuilds toolbar bindings through Unity. Test results are written to Logs/RemixZoomEditMode.xml and Logs/RemixZoomPlayMode.xml. Batch-mode Unity -runTests also works when the project is closed.

Quest 3 hardware validation remains required: seated two-hand comfort and pinch noise, intentional gesture activation versus ribbon/transport/palm use, tracking recovery and release latch, menu readability and hit targets, future edits across zoom and looping, and CPU/GPU timing, managed allocations, thermal behavior and audio underruns at the intended refresh rate. Editor tests do not establish device performance or perceived audio quality. No physical Quest validation is claimed.


Validation results (Unity 6000.3.23f1, Windows Editor, 2026-11-13): compilation and scene/audio/seated/zoom validation passed. Edit Mode: 54/54 passed. Play Mode: 8/8 passed in the existing Direct3D 12 editor. Future-edit validation checks AudioSource gain at the correct later timestamp. Reports are Logs/RemixZoomEditMode.xml, Logs/RemixZoomPlayMode.xml and Logs/RemixZoomSceneValidation.txt. Physical Quest testing remains outstanding.

Files added: Runtime/TimelineZoom.cs, Runtime/TimelineZoomUI.cs, Editor/RemixZoomValidation.cs, Tests/EditMode/TimelineZoomTests.cs, Tests/PlayMode/TimelineZoomPlayTests.cs, this document and Unity metadata.

Files extended for zoom: Runtime/RemixDesign.cs, Runtime/RemixTableManager.cs, Runtime/RibbonView.cs, Runtime/RibbonInteractionManager.cs, Runtime/RemixTransport.cs, Materials/RemixDesign.asset, Editor/RemixSeatedLayout.cs, Scenes/RemixTable.unity, Tests/EditMode/SeatedLayoutTests.cs, Tests/PlayMode/GestureGainTests.cs, README.md and CURVED_TIMELINE.md. Earlier uncommitted curved-layout work is retained.

Inline toolbar validation (2026-11-15): Unity compilation and saved-scene validation passed; Edit Mode 143/143 and Play Mode 13/13 passed. Reports: Logs/RemixToolbarSceneValidation.log, Logs/RemixToolbarEditMode.xml and Logs/RemixToolbarPlayMode.xml. Physical Quest 3 testing remains outstanding.
