# Curved seated timeline

The current renderer uses smooth, audio-derived holographic ribbons; see [HOLOGRAPHIC_RIBBONS.md](HOLOGRAPHIC_RIBBONS.md). Spatial placement and interaction coordinates below remain unchanged.

Audio-derived waveforms supersede the historical synthesized display and 128-segment geometry budget below. See [AUDIO_WAVEFORMS.md](AUDIO_WAVEFORMS.md) for signed peak caches, 512-column envelopes, gain mapping and stable baseline selection. Arc placement and time direction remain unchanged.

Adjustable zoom now controls the symmetric runtime time window. Normal remains 4 seconds past / 4 seconds future; see [TIMELINE_ZOOM.md](TIMELINE_ZOOM.md) for the authoritative zoom mapping, presets and edit deferral. The historical design past/future fields below remain for compatibility.

The existing RemixTable scene uses a procedural four-lane cylindrical timeline. The XR rig, hand tracking, passthrough, audio clips, automation store, and DSP transport remain in place. RobotArchitect.unity is outside this change.

Defaults in Materials/RemixDesign.asset:
- Radius 0.8 m; horizontal coverage 180 degrees; interactive coverage -45 to +45 degrees.
- Lane centers 0.09 m apart, total band height 0.348 m; center 0.20 m below initial eye height.
- Four seconds of past music and four seconds of upcoming music. The outer two seconds on either side are peripheral preview at these settings.
- 128 segments per lane: 1,032 vertices and 1,024 triangles total, four ribbon renderers sharing the existing material.

The workspace anchors once from the initial horizontal camera gaze. Its circle center is directly beneath the initial eye position; it does not follow later head motion. Transport and playhead remain fixed at the forward tangent. All settings are in the RemixDesign asset referenced by the scene manager. Radius, arc angle, interaction angle, arc scale, vertical eye drop, lane spacing, past and future seconds are configurable. Segment count is sampled at mesh creation. Apply Curved Layout in Tools/Remix Table saves the layout into the existing scene; runtime applies lane spacing on initial anchoring.

Mapping: p(theta) = (R sin(theta), laneY + waveformHeight, R(cos(theta)-1)). Hand world positions are transformed into ribbon space; atan2(x,z+R) recovers theta. The legacy LocalX API now returns a normalized angular coordinate expressed in the existing width parameter, rather than a physical flat X position. TimeOffset maps negative angles to the past window and positive angles to the future window. Timestamp adds the shared audio clock modulo song duration. Waveform phase and automation both use that timestamp, so geometry scrolls left through a stationary playhead and remains deterministic at loop boundaries. This extends the inherited synthesized waveform; it is not a sample-accurate PCM waveform display.

Analytical selection measures radial and vertical distance to the curved wave, limits acquisition to the central angle and the lane height, and uses no MeshCollider. Peripheral geometry is dimmed. Two-hand resizing uses angular coordinates clamped at the arc endpoints, preserving endpoint order across the song seam. One-hand drag uses local vertical displacement. Edit selection synchronously captures the audio clock after the existing global pause owner is acquired; all ribbons read that same frozen clock. Region timestamps remain song-relative. Release relinquishes the existing owner; manual pause continues to take precedence. Gain is stored through the existing automation store and evaluated by both rendering and audio.

Validation covers mapping round trips, central playhead, time direction, loop wrap, peripheral rejection, lane separation, one- and two-hand gestures on flat and curved surfaces, owner transfer, future edit timestamps and later AudioSource gain, global pause and saved XR scene behavior. Mapping allocation test checks 10,000 calls; mesh counts are bounded. These checks do not establish full frame allocation or device performance budgets.

Quest 3 hardware acceptance remains required: verify initial seated placement and comfortable reach (especially the 0.8 m radius), palm menu and transport ray interaction, occlusion and readability at both arc ends, hand tracking loss and owner transfer, all-stem pause/resume and future gain playback across looping. Profile CPU/GPU frame time, managed allocations, audio underruns and thermal behavior at the target refresh rate. Tune radius and interaction angle for the seated tester. No physical hardware validation is claimed.


Validation results (Unity 6000.3.23f1, Windows Editor, 2026-11-13): compilation and scene/audio/seated validators passed. Edit Mode 43/43 passed; Play Mode 7/7 passed using Direct3D 11. The future-region test verifies the later AudioSource volume, rather than asserting perceived hardware audio. Reports: Logs/RemixCurvedEditMode.xml and Logs/RemixCurvedPlayMode.xml; scene validation log: Logs/RemixCurvedValidation.log. No Quest hardware testing was performed.
