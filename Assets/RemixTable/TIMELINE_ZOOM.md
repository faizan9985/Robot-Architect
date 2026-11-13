# Adjustable timeline zoom

The current audio-derived holographic renderer retains this zoom and interaction path. See [HOLOGRAPHIC_RIBBONS.md](HOLOGRAPHIC_RIBBONS.md) for the smooth envelope interpolation and current geometry budget.

Waveform rendering now uses zoom-adaptive audio peak caches and 512 fixed columns per lane. See [AUDIO_WAVEFORMS.md](AUDIO_WAVEFORMS.md) for current geometry and memory budgets; earlier 128-segment rendering references below are historical. Zoom controls and gesture arbitration remain unchanged.

The saved RemixTable scene starts at Normal: eight seconds across the arc, four seconds on each side of the current audio timestamp. Detail = 4 seconds, Normal = 8, Wide = 16, Overview = 32. These values change the time mapping only. Radius (0.8 m), arc angle (180 degrees), lane spacing (9 cm), world anchor, transport and central playhead stay fixed.

## Shared mapping and smoothing

RemixTableManager owns one serializable TimelineZoom instance. VisibleSeconds is the continuous displayed window; TargetSeconds is the clamped request. Requests use the range 4?32 seconds and reject nonfinite values. The runtime value belongs to the manager, so gestures never mutate the shared design asset or automation data. The earlier design pastSeconds/futureSeconds fields remain for legacy authoring compatibility; curved runtime mapping uses manager.zoom exclusively. The flat compatibility path retains design.visibleSeconds.

For angle theta and total arc angle A, offset = theta / A * VisibleSeconds. Timestamp = (audio playhead + offset) modulo song duration. RibbonView routes mesh phase, highlighting, hit testing and region endpoints through this mapping. The physical position still comes from RemixDesign.ArcPoint. TimelineZoom also supplies AngleToTimestamp, TimestampToAngle and OffsetToAngle. The inverse timestamp mapping chooses the nearest loop occurrence; when a window is longer than a very short song, a timestamp has multiple visible occurrences, so use a known signed time offset to identify another occurrence.

The manager advances zoom once in LateUpdate, before ribbon mesh updates, using an exponential transition with a default 0.16-second time constant. Gesture processing runs first. If a ribbon is selected or the audio manager has an editing pause owner, interpolation freezes at its current displayed value. A menu selection changes the target only and is marked queued until the final owner releases. An in-progress transition also freezes immediately on selection. Existing one-hand gain anchors, two-hand boundaries, whole-stem operations, pause ownership and absolute automation timestamps are preserved. Audio never reads or depends on zoom.

## Two-hand gesture

Use the existing XR Hands pinch input in RibbonInteractionManager. Both hands must be tracked and pinching in empty, forward workspace, with at least 12 cm separation. Ribbon acquisition is tested first for both hands: either hand on a ribbon starts the original edit/region path. Existing selected ribbons or any audio editing owner prevent zoom. An open palm menu prevents acquisition as well.

The seated acquisition volume is relative to the stable workspace: x within ?0.65 m, y within ?0.5 m, z from -radius + 0.12 m to +0.18 m. Hands can be comfortably closer to the body than the wave surface; there is no need to reach either arc end. After acquisition, ZoomPending captures the initial hand separation and displayed window, and locks out ribbon acquisition. A 2.5 cm separation change activates Zooming. Default sensitivity is 1:

    requestedSeconds = initialSeconds * (initialSeparation / currentSeparation)^sensitivity

Spreading to twice the initial separation halves the time window; contraction doubles it. The same shared interpolation smooths both gesture and menu requests. Threshold, sensitivity and smoothing are Inspector settings in RemixDesign. The numeric range is always clamped to 4?32 seconds.

Release of either pinch, tracking loss, menu opening, external editing ownership, application focus/pause loss or component disable cancels a zoom session. It keeps the last requested window. Both hands must subsequently be tracked and observed released before another gesture can start. Tracking loss alone does not count as release. Pending and active zoom sessions never switch into ribbon editing mid-gesture and never acquire an audio pause owner.

## Existing palm menu

The expandable TIMELINE ZOOM button occupies a compact 36-unit header in the original left-palm canvas. Its four 50-unit-high preset buttons replace the gesture-instructions area while expanded; collapsing restores the instructions. The canvas dimensions, passthrough toggle, volume controls, close/navigation and XRI raycaster stay intact. No new menu canvas or EventSystem is created.

The persistent button bindings target TimelineZoomUI on the existing transport: Detail() requests 4, Normal() 8, Wide() 16, Overview() 32; ToggleOptions() switches the existing instructions area. Menu and transport feedback read the same shared state, show the current range, queued requests and MIN/MAX. Feedback uses TMP numeric formatting, rounds display to half-seconds, checks at most five times per second and only updates changed values. It adds one small label to the transport's existing title row.

## Validation and performance

Tests cover default and limits, each preset's bidirectional mapping, loop seams, shared mapping on all four stems, unchanged physical placement and automation, threshold and gesture direction, ribbon priority, tracking loss, release latch, disable cancellation, menu bindings, smooth transitions, edit deferral, live audio during zoom and audible-gain source values for future edits after zooming. The mesh object and vertex count remain unchanged. A 10,000-iteration interpolation/mapping test asserts zero managed allocation in that hot path. Existing 128-segment ribbon buffers and analytical proximity checks are reused, with no collider rebuilding or added triangles.

RemixZoomValidation supports validation in an already-open editor. Write scene, edit or play to Logs/RemixZoomValidation.request after refreshing assets; it refuses to replace unsaved open scenes. Scene mode applies the UI to the existing scene and runs scene/audio/layout/zoom validators. Test results are written to Logs/RemixZoomEditMode.xml and Logs/RemixZoomPlayMode.xml. Batch-mode Unity -runTests also works when the project is closed.

Quest 3 hardware validation remains required: seated two-hand comfort and pinch noise, intentional gesture activation versus ribbon/transport/palm use, tracking recovery and release latch, menu readability and hit targets, future edits across zoom and looping, and CPU/GPU timing, managed allocations, thermal behavior and audio underruns at the intended refresh rate. Editor tests do not establish device performance or perceived audio quality. No physical Quest validation is claimed.


Validation results (Unity 6000.3.23f1, Windows Editor, 2026-11-13): compilation and scene/audio/seated/zoom validation passed. Edit Mode: 54/54 passed. Play Mode: 8/8 passed in the existing Direct3D 12 editor. Future-edit validation checks AudioSource gain at the correct later timestamp. Reports are Logs/RemixZoomEditMode.xml, Logs/RemixZoomPlayMode.xml and Logs/RemixZoomSceneValidation.txt. Physical Quest testing remains outstanding.

Files added: Runtime/TimelineZoom.cs, Runtime/TimelineZoomUI.cs, Editor/RemixZoomValidation.cs, Tests/EditMode/TimelineZoomTests.cs, Tests/PlayMode/TimelineZoomPlayTests.cs, this document and Unity metadata.

Files extended for zoom: Runtime/RemixDesign.cs, Runtime/RemixTableManager.cs, Runtime/RibbonView.cs, Runtime/RibbonInteractionManager.cs, Runtime/RemixTransport.cs, Materials/RemixDesign.asset, Editor/RemixSeatedLayout.cs, Scenes/RemixTable.unity, Tests/EditMode/SeatedLayoutTests.cs, Tests/PlayMode/GestureGainTests.cs, README.md and CURVED_TIMELINE.md. Earlier uncommitted curved-layout work is retained.
