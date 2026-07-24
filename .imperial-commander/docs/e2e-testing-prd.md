# PRD: E2E Integration Testing

## Overview
**Name:** Screen Studio E2E Test Suite
**Goal:** Verify that the isolated, unit-tested engine components compose into a correct,
robust end-to-end pipeline: raw capture → cursor smoothing → auto-zoom → frame composition →
export, producing a real, validated MP4 with audible mixed audio.

## Problem Statement
Each engine module (smoothing, zoom detection, timeline, export pipeline, audio DSP) is
covered by focused unit tests (92 passing). But none of those tests exercise the modules
*together* as they run in the real application. Integration bugs live at the seams:

- Timestamp conventions (QPC ms vs capture ms) drifting between capture → smoother → exporter
- The smoothed-cursor stream not aligning frame-for-frame with the zoom camera program
- The export pipeline emitting frames for *cut* regions, or skipping *kept* segments
- Audio mix length mismatching video duration (mux desync)
- Frame-compositor coordinate space disagreeing with the zoom camera's relative-focus model
- Pause intervals in a recording session leaking into the exported timeline
- Cancellation mid-export leaving a half-written, unplayable file

Unit tests cannot catch these. An E2E suite that drives the full pipeline with controlled
synthetic input (deterministic frames + scripted cursor events) and asserts on the real
output artifacts is the only way to prove the composition.

## Scope

### In Scope (v1 of the E2E suite)
1. **Synthetic recording fixture** — a deterministic, reproducible recording (frames +
   cursor stream + audio) that needs no real screen/mic, so the suite runs headlessly in CI.
2. **Full happy-path export** — synthetic recording through the entire pipeline to a real MP4;
   assert the file is valid and its duration matches the input.
3. **Effects correctness** — assert zoom keyframes fire at the expected times, the smoothed
   cursor stays within tolerance of the raw path, and clicks are preserved at their anchors.
4. **Timeline-edit interactions** — trim and cut operations actually reduce/don't-affect the
   exported duration; a cut removes exactly its span from the output.
5. **Audio integration** — mic + system tracks run through the gate/normalize/mix chain and
   the mix length matches the video timeline.
6. **Cancellation & resource safety** — cancelling mid-export aborts cleanly; no file handles
   leak; the partial file is either absent or explicitly finalized.
7. **Performance gate** — the full pipeline (encode included) for a fixed short clip finishes
   within a budget, guarding against silent regressions.

### Out of Scope (v1)
- Real-device capture (WGC/WASAPI) — covered separately by the env-aware native tests; the E2E
  suite uses the synthetic fixture so it is deterministic and CI-portable.
- WinUI/XAML UI integration — no UI exists yet; that's a future "UI E2E" layer.
- Cross-platform variants — Windows 11 only (the product target).
- Fuzzing / property-based stress (planned for v2).

## Success Criteria
1. The E2E suite runs to green on this machine with **zero real-device dependency** (synthetic
   fixtures only), in under the performance budget.
2. A single green run proves: capture-feed → smoothed cursor → zoom program → timeline-kept
   segments → composited frames → encoded MP4 (ftyp + decodable) → mixed audio, all consistent
   in duration and content.
3. Injecting a known defect at any seam (e.g. a frame-count mismatch) turns a green run red.
4. The suite is deterministic: same input ⇒ same output assertions, run after run.

## Test Architecture

### Layered, mirroring the app layers
```
┌──────────────────────────────────────────────────────┐
│  E2E: RecordingPipelineIntegrationTests              │
│  (drives Capture → Smoothing → Zoom → Export)         │
└───────────────────────┬──────────────────────────────┘
                        │ consumes
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
 SyntheticFixture   PipelineDriver   ArtifactValidator
 (frames/cursor/    (orchestrates    (MP4 ftyp+probe,
  audio builders)    the real types)  duration, frame count)
```

- **SyntheticFixture** — pure builders: `Frames.Gradient(width,height,duration)`,
  `Cursor.ClickStream(events)`, `Audio.ToneTrack(...)`. Deterministic, seeded.
- **PipelineDriver** — the real `RecordingSession` + `CursorSmoother` + `ZoomDetector` +
  `ZoomCamera` + `RecordingTimeline` + `FrameCompositor` + `ExportPipeline`, wired exactly as
  the app will wire them. No mocks of the engine types — only of the *native capture source*.
- **ArtifactValidator** — opens the produced MP4 (ftyp box + ffmpeg probe for stream info),
  checks duration against the input, checks frame count against fps×duration.

### Fixtures instead of real capture
The suite replaces the live `FfmpegScreenCapture`/`WindowsAudioCapture` (non-deterministic,
slow, device-dependent) with the `SyntheticFixture` feeding the same downstream types the real
capture feeds. This is the key to CI-portable, fast, deterministic E2E coverage. The env-aware
native tests separately cover that the *real* capture produces frames.

## Test Cases (v1)

| # | Name | Verifies |
|---|------|----------|
| E1 | HappyPath_Produces_Valid_Mp4_Matching_Duration | full pipeline → valid MP4, duration ≈ input |
| E2 | Smoothed_Cursor_Stays_Within_Tolerance_Of_Raw | smoothed path deviation bounded |
| E3 | Click_Anchors_Preserved_In_Output | every click's position present in smoothed stream |
| E4 | Zoom_Keyframes_Fire_At_Expected_Times | detector emits keys at scripted clicks/pauses |
| E5 | Compositor_Applies_Zoom_To_Focus_Region | zoomed output centers on the focus area |
| E6 | Trim_Reduces_Exported_Duration | timeline trim shortens output by exactly the trim span |
| E7 | Cut_Removes_Exactly_Its_Span | a cut removes only the cut interval, keeps the rest |
| E8 | Pause_Interval_Excluded_From_Export | paused time does not appear in output duration |
| E9 | Audio_Mix_Length_Matches_Video | mixed audio sample count ≈ video duration × rate |
| E10 | Audio_Gate_Silences_Noise_Floor | gated regions are attenuated in the mix |
| E11 | Cancellation_Aborts_Cleanly | cancel mid-export → no exception leak, file finalized or absent |
| E12 | Performance_Finishes_Within_Budget | full short-clip encode under the time budget |

## Non-Functional Requirements
- **Determinism:** seeded RNG everywhere; no wall-clock dependencies in assertions.
- **Speed:** the full suite target is < 30s wall-clock on this machine (short clips, small
  resolution). Performance gate (E12) has its own explicit budget.
- **Isolation:** each test owns its temp output file and cleans it up (finally-block delete).
- **Honest degradation:** if ffmpeg is absent, the encode-touching tests skip cleanly (the same
  `IsAvailable()` pattern as the native tests) rather than fail opaquely.

## Open Questions
1. Should the E2E suite also exercise the *ffmpeg-backed* capture adapters (real screen/mic) as
   a separate, opt-in "smoke" category, or keep v1 strictly synthetic? Recommendation: strictly
   synthetic for v1; add a `[Trait("Category","Smoke")]` opt-in layer later.
2. Target duration for the performance gate (E12): 2s vs 5s clip at 720p/30fps. Recommendation:
   2s — enough to exercise encode, short enough to keep the suite fast.
3. Whether to assert on *decoded pixel content* of the output MP4 (decode a frame and compare)
   or only on container/duration. Recommendation: container+duration for v1 (cheap, robust);
   pixel-decode comparison is a v2 hardening.
