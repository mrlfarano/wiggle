# Implementation Plan: E2E Integration Testing

Companion to `e2e-testing-prd.md`. This is a build plan, not a task list — it describes the
artifacts to create, in dependency order, with the contracts and validation for each. No
`.imperial-commander/tasks/*.yml` is created.

## Guiding principles
- **Reuse the real engine types.** The E2E driver wires `RecordingSession`, `CursorSmoother`,
  `ZoomDetector`, `ZoomCamera`, `RecordingTimeline`, `FrameCompositor`, `ExportPipeline`,
  `AudioExportMixer`, and `FfmpegEncoder` exactly as the app will. We mock *only* the native
  capture source (with synthetic fixtures), never the engine internals.
- **Synthetic → deterministic.** All test data is built from seeded builders. No real screen,
  mic, or wall-clock in the assertions.
- **Assert on artifacts, not internals.** The strongest E2E signal is "the produced MP4 is
  valid and its duration/content match the input." Internals are already covered by unit tests.

---

## Step 0 — Project scaffold

### Artifact
New test project `tests/ScreenStudio.E2E.Tests/`, added to `ScreenStudio.sln`.

### csproj essentials
- `TargetFramework`: `net10.0-windows10.0.19041.0` (same as Native.Tests — the export path
  needs the Windows TFM for `FfmpegEncoder` and the WinRT references).
- References: `ScreenStudio.Core`, `ScreenStudio.Native` (project refs), xUnit, MS Test SDK.
- `IsTestProject=true`, `<Using Include="Xunit" />`.
- Add to the solution under the `tests` solution folder.

### Validation
- `dotnet build ScreenStudio.sln -c Release` → 0 errors, the new project compiles.
- One placeholder `[Fact]` that asserts `true` → green.

---

## Step 1 — Synthetic fixtures (the foundation everything else depends on)

### Artifact
`tests/ScreenStudio.E2E.Tests/Fixtures/SyntheticFixture.cs` — static builder class.

### Public API
```csharp
public static class SyntheticFixture
{
    // Deterministic BGRA frames. Each frame differs so "raw output verification" can confirm
    // frames aren't duplicated/blank.
    public static byte[] GradientFrame(int width, int height, int frameIndex, int seed = 1);

    // A scripted cursor stream: a baseline path plus injected clicks/pauses at known times.
    // Returns events sorted ascending by TimestampMs.
    public static List<CursorEvent> CursorStream(CursorScript script);

    // A pure-tone audio track of a given duration, used as mic/system source for mix tests.
    public static AudioTrack ToneTrack(int sampleRate, int channels, double seconds,
                                       double amplitude = 0.3, int freqHz = 440, int seed = 1);
}

public sealed class CursorScript
{
    public int SampleRateHz { get; set; } = 60;
    public double DurationSeconds { get; set; } = 2.0;
    public Vec2 Start { get; set; } = new(100, 100);
    public Vec2 End { get; set; } = new(800, 600);
    // (timeMs, position) pairs for clicks; (timeMs, durationMs) pairs for pauses.
    public List<(double timeMs, Vec2 pos)> Clicks { get; } = new();
    public List<(double timeMs, double durationMs)> Pauses { get; } = new();
}
```

### Implementation notes
- `GradientFrame`: per-pixel BGRA from `(x, y, frameIndex, seed)` via a cheap hash; ensures
  every frame is distinct and non-uniform (so the compositor/encoder can't hide blank output).
- `CursorStream`: linearly interpolate `Start→End` over `DurationSeconds` at `SampleRateHz`,
  then overlay the scripted clicks (set `CursorButtonState.Left` + pin position at click time)
  and pauses (hold position constant for `durationMs`). Add seeded micro-jitter so smoothing
  has something to smooth.
- `ToneTrack`: `sin(2π·freq·t)` interleaved across channels.

### Validation
- Unit-test the fixtures themselves (in the E2E project): gradient frames are non-uniform;
  cursor stream is sorted and contains the scripted clicks; tone track RMS ≈ amplitude/√2.

---

## Step 2 — The pipeline driver (the seam under test)

### Artifact
`tests/ScreenStudio.E2E.Tests/Drivers/RecordingPipelineDriver.cs`.

### Responsibility
Encapsulate "run a synthetic recording through the full engine and produce an MP4 + mixed
audio," so each E2E test case configures inputs and asserts on outputs without re-wiring the
engine every time.

### Public API
```csharp
public sealed class RecordingPipelineDriver
{
    public RecordingSession Session { get; }
    public RecordingTimeline Timeline { get; }
    public List<CursorEvent> RawCursor { get; }   // fed in
    public List<byte[]> SourceFrames { get; }      // fed in

    public RecordingPipelineDriver(int width, int height, int fps, double durationSeconds,
                                   CursorScript cursorScript);

    // Run smoothing + zoom detection and populate Timeline.Zoom + smoothed cursor.
    public List<CursorEvent> ApplyEffects(SmoothingIntensity smoothing);

    // Export to a real MP4 via FfmpegEncoder. Returns the output path + progress samples.
    public ExportResult Export(string outputPath, ExportSettings settings,
                               AudioTrack? mic = null, AudioTrack? system = null,
                               CancellationToken ct = default);
}

public sealed record ExportResult(string OutputPath, int FramesWritten, double DurationSeconds,
                                  List<ExportProgress> ProgressSamples);
```

### Internal wiring (the heart of "integration")
1. Feed `SourceFrames` + `RawCursor` into `Session` (via `Ingest`).
2. Smooth the cursor with the real `CursorSmoother`.
3. Run the real `ZoomDetector` over the raw cursor → populate `Timeline.Zoom`.
4. For each kept timeline segment, for each output frame:
   - Evaluate `ZoomCamera.Evaluate(Timeline.Zoom, t)` → camera state.
   - `FrameCompositor.Compose(sourceFrame, w, h, camera, cursorAtT, cursorStyle)` → BGRA.
   - Pipe to `FfmpegEncoder.WriteFrameRgb32`.
5. If audio provided, run `AudioExportMixer.MixForExport(mic, system)` → mixed track (length
   asserted against video duration in E9).
6. `FfmpegEncoder.FinalizeStream()`.

### Why this is the integration target
This is the only place the modules touch each other. Every E2E test exercises this driver, so
a bug at any seam (timestamp mismatch, coordinate-space disagreement, segment skipping) surfaces
here rather than being hidden by isolated unit tests.

### Validation
- Driver compiles; a 1-second happy-path export produces a non-empty `ExportResult`.

---

## Step 3 — Artifact validators

### Artifact
`tests/ScreenStudio.E2E.Tests/Validators/ArtifactValidator.cs`.

### Public API
```csharp
public static class ArtifactValidator
{
    // Open the MP4, assert ftyp box, return ffmpeg -i probe output.
    public static string AssertValidMp4(string path);

    // Parse "Duration: 00:00:02.00" from ffmpeg probe → seconds.
    public static double ProbeDurationSeconds(string ffmpegProbeOutput);

    // Run ffmpeg to count actual decoded frames (catches truncated/short exports).
    public static int CountDecodedFrames(string path);
}
```

### Implementation notes
- Uses `FfmpegEncoder.FindFfmpeg()`; if absent, throws `SkipException` (honest skip, not fail).
- `CountDecodedFrames`: `ffmpeg -i path -map 0:v:0 -c copy -f null -` and parse `frame= N`.
- All temp files owned by the caller (the test), cleaned in `finally`.

### Validation
- Validators compile; `AssertValidMp4` on the Step-2 happy-path output returns non-empty probe.

---

## Step 4 — E2E test cases (mapped 1:1 to the PRD test table)

### Artifact
`tests/ScreenStudio.E2E.Tests/RecordingPipelineIntegrationTests.cs` (+ a second file for
audio/cancellation/perf to keep files focused).

### Cases and their assertions
- **E1 HappyPath_Produces_Valid_Mp4_Matching_Duration**
  Driver export (2s, 720p, 30fps) → `AssertValidMp4`; `ProbeDurationSeconds` ≈ 2.0 (±1 frame);
  `FramesWritten` == 60; `CountDecodedFrames` == 60.

- **E2 Smoothed_Cursor_Stays_Within_Tolerance_Of_Raw**
  For the smoothed stream, max perpendicular distance from the raw Start→End line < tolerance
  (e.g. 5% of path length). Guards against the smoother diverging.

- **E3 Click_Anchors_Preserved_In_Output**
  For each scripted click in the CursorScript, some smoothed frame within ±1 step reports the
  click's exact position and `Buttons == Left`.

- **E4 Zoom_Keyframes_Fire_At_Expected_Times**
  After `ApplyEffects`, `Timeline.Zoom.Keyframes` contains an entry at (or within MinKeyframeGap
  of) each scripted click/pause time, with `Scale > 1`.

- **E5 Compositor_Applies_Zoom_To_Focus_Region**
  Build a half-blue/half-red source; script a click in the blue half; export one zoomed frame
  via `FrameCompositor.Compose` directly; assert the output center pixel is blue. (Frame-level
  check; no full decode needed.)

- **E6 Trim_Reduces_Exported_Duration**
  `Timeline.TrimEdges(500ms, 1500ms)` on a 2s clip → exported duration ≈ 1.0s, frames == 30.

- **E7 Cut_Removes_Exactly_Its_Span**
  `Timeline.Cut(800ms, 1200ms)` on a 2s clip → duration ≈ 1.6s (2.0 − 0.4), and frames span
  [0,800)∪[1200,2000).

- **E8 Pause_Interval_Excluded_From_Export**
  `Session.Pause/Resume` a 400ms window during the synthetic record → exported duration
  excludes it (≈ 1.6s for a 2s nominal record).

- **E9 Audio_Mix_Length_Matches_Video**
  Provide mic + system tone tracks for the clip duration; after `MixForExport`, the mixed track's
  sample count ≈ duration × sampleRate × channels (within one quantum).

- **E10 Audio_Gate_Silences_Noise_Floor**
  Mic track = signal half + low-amplitude noise half; after the mix (gate enabled), the noise
  half's peak < the signal half's peak by a clear margin.

- **E11 Cancellation_Aborts_Cleanly**
  Start export, cancel after ~10 frames: no unhandled exception escapes; the output file is
  either absent or, if present, `FinalizeStream` was called (no locked handle). Assert
  `File.Exists` is deterministic (the test asserts the *contract*: cancel doesn't corrupt
  state; a follow-up export on a fresh encoder still works).

- **E12 Performance_Finishes_Within_Budget**
  Happy-path export (2s, 720p, 30fps) completes in < N seconds (start N generous, e.g. 15s, to
  avoid CI flakiness; tighten once stable). Assert via `Stopwatch`.

### Shared setup
- A helper `TempMp4()` and `using`/`finally` cleanup in every test.
- A `[Fact]` per case (not theories) for v1 — deterministic single scenarios are clearer.

### Validation
- All 12 cases green on this machine.
- Manually inject one defect (e.g. make `FrameCompositor` skip the zoom transform) and confirm
  E5 goes red — proving the suite is sensitive, not vacuously green.

---

## Step 5 — CI / run integration

### Artifact
- A root-level `run-e2e.cmd` (or just documentation in the plan) invoking
  `dotnet test tests/ScreenStudio.E2E.Tests/ScreenStudio.E2E.Tests.csproj -c Release`.
- A note in the project README (when one exists) that E2E requires ffmpeg on PATH.

### Validation
- `dotnet test ScreenStudio.sln -c Release` runs the E2E project alongside the existing suites
  and all pass.

---

## Sequencing & dependencies

```
Step 0 (scaffold) ── must build first
   └─ Step 1 (fixtures) ── everything consumes these
        ├─ Step 2 (driver) ── depends on fixtures + all engine types
        │    └─ Step 3 (validators) ── depends on ffmpeg being found
        │         └─ Step 4 (test cases) ── depends on driver + validators
        │              └─ Step 5 (run integration)
        └─ (Step 1's own self-tests run independently)
```

Each step is independently shippable and independently verifiable before moving on.

## What "done" looks like
- `tests/ScreenStudio.E2E.Tests/` exists, builds, and the 12 E2E cases pass.
- The suite is deterministic and device-free (synthetic fixtures only).
- A defect injected at any pipeline seam turns the relevant case red.
- Total suite runtime is within the budget set in E12.

## Risks & mitigations

| Risk | Mitigation |
|------|------------|
| ffmpeg absent on a CI runner | validators `Skip` cleanly via `IsAvailable()` (matches native-test pattern) |
| Flaky perf budget (E12) | generous initial budget; separate `[Trait("Category","Performance")]` so it can be excluded from fast runs |
| Timestamp-unit drift between layers | the driver centralizes all timestamping; unit tests already pin each layer's convention |
| MP4 duration off-by-one-frame | assertions use ±1-frame tolerance, not exact equality |
| Cancellation test (E11) flaky due to timing | cancel after a fixed frame count (deterministic), not a wall-clock delay |

## Estimated effort
Small-to-medium. The engine types all exist and are unit-tested; this is wiring + fixtures +
assertions, no new production logic. The bulk is Step 1 (fixtures) and Step 4 (assertions),
each a few hours; Steps 0/2/3/5 are mechanical.
