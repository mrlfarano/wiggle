# Implementation Plan: Screen Studio Clone (Windows 11)

> **Updated 2026-07-24** — retargeted from macOS to Windows 11; all phases reflect the actual
> C#/.NET 10 + WinUI 3 + Windows App SDK stack. The original plan described macOS/Swift; this
> version is the source of truth. See `prd-screenstudio-clone.md` for the full PRD.

## Completed Phases (001–022)

All 22 tasks are complete. The phases below are the retrospective structure.

### Phase 0: Foundation (001)
- ✅ Tech stack decision: C# / .NET 10 + WinUI 3
- ✅ Windows 11 API mapping: WGC (capture), Media Foundation (encode), WASAPI (audio), WH_MOUSE_LL (cursor)
- ✅ Solution scaffolded: Core (engine), Native (Windows adapters), App (WinUI)

### Phase 1: Core Recording (002)
- ✅ Screen capture: FFmpegScreenCapture (gdigrab) + WindowsScreenCapture (WGC)
- ✅ Cursor tracking: CursorHook (WH_MOUSE_LL) + CursorEventLogger (binary persist)
- ✅ Recording controls: RecordingSession state machine (Idle/Recording/Paused/Stopped)

### Phase 2: Cursor Smoothing (003)
- ✅ Centripetal Catmull-Rom (Barry–Goldman) with configurable intensity
- ✅ Click-anchor preservation; 60fps benchmark (812ms for 10-min clip)

### Phase 3: Auto-Zoom (004)
- ✅ ZoomDetector (click/drag/pause detection with min-gap)
- ✅ ZoomCamera (ease-in/out cubic interpolation)
- ✅ FrameCompositor (CPU RGB32 zoom transform + cursor overlay)

### Phase 4: Timeline Editor (005)
- ✅ RecordingTimeline (trim/cut segments, keyframe add/move/remove, playhead snap)

### Phase 5: Export Engine (006)
- ✅ ExportPipeline (async, cancellable, progress-reporting)
- ✅ FfmpegEncoder (H.264/HEVC/GIF/WebM via libx264/libvpx)
- ✅ MediaFoundationEncoder (MF sink writer, NVENC/QSV/AMF — compile-verified)
- ✅ ExportSettingsViewModel + ExportSettingsConsoleUi + presets

### Phase 6: Additional Features (007–009, 016–020)
- ✅ Cursor customization (size, auto-hide, loop, custom image)
- ✅ Audio recording (noise gate, level meter, normalization, mixer, export mixer)
- ✅ Aspect-ratio modes (16:9 ↔ 9:16, social presets, relative-keyframe recalc)
- ✅ Webcam PiP overlay (FfmpegWebcamCapture + ComposeWithWebcam)
- ✅ Visual customization (backgrounds, padding, shadows, borders)
- ✅ Motion blur (cursor trail based on velocity)
- ✅ Keyboard shortcut display (KeyboardHook + DrawKeycap)
- ✅ GIF/WebM export + social presets
- ✅ Crop region, speed ramp, transcript generation contract, hide-desktop-icons

### Phase 7: UI (010–015)
- ✅ WinUI App scaffold (WindowsAppSDK 2.3.1, builds without VS)
- ✅ RecordingOverlay (floating chrome, tray, transport controls)
- ✅ PostRecordWindow (live preview + ExportSheet)
- ✅ StylePanel (cursor/smoothing/zoom/background/effects tabs)
- ✅ TimelinePanel (Canvas-based keyframe editor)
- ✅ SettingsWindow (persistence, device enumeration)

### Phase 8: Testing & Polish (021–022)
- ✅ Tray context menu + window enumeration wiring
- ✅ Runtime smoke test (launch crash fixed: XAML root/theme-resource/null-VM)
- ✅ E2E integration tests (15 cases: full pipeline → real MP4)
- ✅ Performance profiling (7 tests: 30s 720p=16s, 2min 720p=47s)
- ✅ PRD-based user-feature tests (20 cases: U01–U20)
- ✅ Packaging (publish.cmd → self-contained 85MB zip)

## Future Work (deferred)

| Item | Priority | Notes |
|------|----------|-------|
| Live record→export through GUI | High | Needs human at keyboard |
| MSIX installer + code signing | Medium | Needs cert + admin |
| iOS device recording (USB) | Low | Niche; complex AVFoundation bridge |
| Whisper.net speech-to-text | Medium | Contract built; needs model integration |
| Share/collaboration (cloud) | Low | Different product (Loom/Tella territory) |
