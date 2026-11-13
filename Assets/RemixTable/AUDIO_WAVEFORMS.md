# Audio-derived curved waveforms

The saved RemixTable scene references four `.rwave` assets through each song's `StemMetadata.waveform`. The original WAVs, streaming AudioSources, DSP clock, automation records and XR configuration are unchanged. Missing caches and silent placeholder songs display a flat baseline. There is no synthetic waveform fallback.

## Analysis and regeneration

With the project open and authored scenes saved, run **Tools > Remix Table > Bake Audio Waveforms**. This streams `Audio/NIVIROTest/Stems/{drums,bass,other,vocals}.wav`, rebuilds all four caches and assigns them in the saved RemixTable scene. Close Unity before using the equivalent command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'X:\Capstone\Robot Architect' -executeMethod RemixTable.Editor.WaveformBaker.BakeAll -quit -logFile Logs/RemixWaveformBake.log
```

Rebake whenever source samples, duration or sample rate change. Retain the cache `.meta` files so scene references remain stable. `WaveformBaker.Validate` verifies cache and imported clip frame counts/rates plus the existing scene/audio/seated/zoom validators. Generation reads original RIFF samples independently of Unity's Vorbis streaming import. Supports IEEE float32 and signed PCM16/24/32; unsupported formats fail explicitly. Original audio files and import settings are never written.

Each base bin spans 128 sample frames (2.902494 ms at 44.1 kHz). Minimum and maximum cover **both channels independently**, preserving polarity and avoiding stereo cancellation. RMS accumulates squared samples over both channels. No window or stem is display-normalized; no silence threshold discards low-level audio. Parents merge extrema and sample-weighted RMS energy, including the final partial bin. Source float amplitudes above 1 are preserved.

The little-endian RWV1 format contains sample rate/frame count/block size, one global reversible amplitude encoding scale, level counts, the SHA256 of the complete original WAV and packed signed-int16 min/max plus uint16 RMS triples. Extrema quantize outward so transients survive encoding. This quantization scale restores the original amplitude units at runtime; it does not equalize stems. The worst peak error is one quantization step (source absolute peak / 32767); RMS error is at most half a step per stored node.

For these 10,143,744-frame / 230.016871-second stereo sources, each cache has 18 levels, 79,248 base bins and **951,140 bytes on disk**. Four files total **3,804,560 bytes (3.63 MiB)**. Imported packed peak payloads total approximately 3.63 MiB plus small ScriptableObject/level-array overhead. Unity's editor/import/build copies are separate from this runtime bound. Startup deserializes compact peak bytes, never full-song samples. There is no runtime AudioClip.GetData, WAV decoding or cache construction.

## Lookup and rendering

Time comes from the existing shared DSP-backed `visualTime` and `TimelineZoom.Timestamp`, with the fixed center playhead and upcoming music on the right. Each column covers a half-open interval starting at its left edge. Looping queries split at the exact source duration, preserving tail and head peaks without phase offsets. The cache query uses the largest aligned pyramid nodes fitting inside the interval and descends for its edges. This is peak-preserving adaptive resolution without averaging transients away. Conservative edge bins add less than 2.903 ms on either side; sub-bin alignment is approximate. Full-song and longer spans are bounded to one loop.

512 columns per lane give 7.8125 / 15.625 / 31.25 / 62.5 ms per column at the 4 / 8 / 16 / 32-second presets. Interior nodes typically use levels 1 / 2 / 3 / 4 respectively, with finer nodes at boundaries. This also supports every intermediate continuous zoom value. Geometry is fixed at 2,048 vertices and 1,024 triangles per lane: **8,192 vertices, 4,096 triangles and four waveform draws** overall. Adjacent independent quads form sharp, stepped signed envelopes. Both edges follow the existing cylindrical `ArcPoint` mapping. Vertex/color/index buffers and meshes are created once; updates reuse buffers through Mesh.SetVertices/SetColors. No normals, colliders, FFT, particles or new textures are generated. The URP stereo-aware unlit shader now writes opaque depth with crisp colored edges instead of additive glow, reducing transparency overdraw. Existing violet/orange/green/cyan colors and peripheral dimming remain.

## Gain and interaction

For each column, original min/max amplitudes multiply the existing `AutomationStore.EvaluateGain` at its left-edge timestamp. Whole-stem and region precedence, endpoint fades, maximum gain, stored timestamps and loop persistence are identical to playback. Region boundaries are represented to one rendered column (up to 62.5 ms at Overview), plus base-bin lookup tolerance; audio still evaluates the precise playback timestamp with its existing temporal smoothing. Waveform edits are immediate; audio source smoothing remains unchanged. There is no additional normalization, use of mix headroom, pitch deformation or synthetic effect displacement in the envelope.

Let `a = source amplitude * evaluated gain`. The consistent signed display mapping is `height = H * a / (1 + abs(a))`, with `H = laneHeight/2 - 3mm - baselineHalfWidth`. For the saved 78 mm lane and 0.35 mm baseline, H is 35.65 mm. This monotonic soft bound accommodates source peaks above 1 and gain up to 2.5 without hard clipping or crossing lanes. An amplitude of 1 occupies H/2; amplitude 2.5 occupies about 0.714H. All stems and regions share this scale. Min/max extend from a thin 0.7 mm total baseline; exact silence or zero gain stays flat. RMS is available for inspection but does not replace signed peaks.

`HeightAt` and `SurfacePoint` intentionally describe the stable lane baseline for analytical hand acquisition. The original radial/angular/lane bounds and 3.2 cm selection radius remain independent of waveform detail. Silent regions remain editable. Gain drag, boundary selection, two-hand zoom arbitration, pause ownership, palm presets and transport continue through the existing interaction path.

## Verification and Quest acceptance

Waveform tests compare the caches against direct source samples, synthetic signed stereo silence/dynamics/one-sample impulses, all zoom levels and seam splits. Geometry tests cover radius, lane bounds, region-only gain and muted selection; allocation tests exercise real mesh updates after warmup. Play Mode checks real streaming audio, source synchronization, DSP mapping, zoom without mesh replacement, paused gain edits and repeated-loop display. Run both complete RemixTable test assemblies, including all existing gesture/audio/zoom regression tests. Results are under ignored `Logs/RemixWaveform*` files.

Physical Quest 3 testing is still required: peak legibility at seated distance and arc ends, transient flicker while scrolling, silent-region acquisition, two-hand gesture comfort, palm and transport visibility, passthrough occlusion, and CPU/GPU frame time, managed/native allocation, thermals and audio underruns at the target refresh rate. Editor results do not establish Quest performance or hardware interaction quality.

Validation on 2026-11-13 with Unity 6000.3.23f1: compilation and scene/audio/seated/zoom/waveform validators passed; Edit Mode **65/65** and Play Mode **9/9** passed (Direct3D 11). Warmed-up waveform mesh updates allocated zero managed bytes in the Edit Mode measurement. Play Mode uses desktop audio and synthetic hand input, with no connected headset validation. Reports: `Logs/RemixWaveformEditMode.xml`, `Logs/RemixWaveformPlayMode.xml`, `Logs/RemixWaveformValidation.txt` and corresponding editor logs.
