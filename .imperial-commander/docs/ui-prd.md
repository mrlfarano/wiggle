# UI PRD: Screen Studio Clone — Windows 11
**Status:** Draft · **Companion to:** `research.md`, `user-journey.md`
**Date:** 2026-07-24

This PRD specifies the **presentation layer** (the WinUI 3 frontend) for an app whose engine is
already built and tested (92 passing tests across `ScreenStudio.Core` + `ScreenStudio.Native`).
It is grounded in (a) the actual engine binding surface that exists today and (b) the competitive
research in `research.md`. See `research.md` §7 for the synthesis this PRD implements.

---

## 1. Vision

**Record once, get a polished video automatically — on Windows.**

The UI must make the app's core differentiator *visible*: the user records a raw capture and the
app invisibly applies professional effects (auto-zoom, smooth cursor, beautiful framing). The UI's
job is to **stay out of the way during recording** and **make the magic obvious after**. Per
`research.md`, this is the Screen Studio paradigm (matched, not reinvented) executed natively on
Windows with Fluent aesthetics that Electron rivals can't match.

## 2. Goals & non-goals

### Goals
- **G1 — Frictionless recording.** A first-time user reaches a finished MP4 in <2 minutes,
  including any permission grants (research §4 complaint #4; NN/g onboarding principle).
- **G2 — Zero-config default path.** Sensible auto settings; advanced controls are one click away,
  never in the default flow (Nielsen #8 minimalist design; research §5).
- **G3 — Native, premium feel.** WinUI 3 / Fluent materials (Mica, acrylic, reveal, subtle motion)
  as a visual differentiator vs. Electron competitors (research §5).
- **G4 — Power-user escape hatches.** A light timeline + keyframe editing for the ~10% who want to
  nudge a zoom — without imposing Camtasia-level complexity on the other 90% (research §4 #3).
- **G5 — Bind to existing ViewModels/engine, don't reimplement.** The UI is a thin shell over
  `ExportSettingsViewModel`, `CursorCustomization`, `RecordingSession`, `RecordingTimeline`,
  `ZoomProgram`, `ExportPipeline`, and the `IScreenCapture`/`IVideoEncoder`/`IAudioCapture`
  adapters (codebase survey §1–3).

### Non-goals (v1)
- Cloud sharing / collaboration (Tella/Loom's product — research §7.6).
- AI transcripts / text-based editing (Camtasia's wedge).
- Mobile companion app.
- A full non-linear editor (we are *not* Camtasia).

## 3. Personas (see `user-journey.md` for full flows)

| Persona | Skill | What they want | UI implication |
|---|---|---|---|
| **Marketing Maya** — product marketer | low | a polished demo clip, fast, no editing | default auto path; presets; one-click export |
| **Educator Eli** — course creator | medium | clear zooms on clicks, good audio, maybe trim | light timeline trim; cursor customization |
| **Creator Cory** — social content maker | medium | 9:16 vertical, social presets, click highlights | aspect-ratio switcher prominent |
| **Dev Dana** — records app demos | high | keyboard shortcuts, precise control, fast | full shortcut set; keyframe editor; advanced export |

All four must succeed on the **default path**; only Dana needs the advanced surfaces.

## 4. Design principles (binding constraints on every screen)

1. **Default to zero config.** Every advanced control has a sensible default that produces good
   output. Nothing is required to start recording.
2. **Progressive disclosure.** Basic choices first; "Advanced" expands the rest. (Already the
   pattern in `ExportSettingsViewModel.Presets` + custom fallthrough.)
3. **Obvious system status.** Recording/paused/stopped are visually unmistakable (Nielsen #1).
4. **Recognition over recall.** Surface actions as visible controls (preset chips, keyframe dots
   on the timeline), not menu items (Nielsen #6).
5. **Native Fluent aesthetics.** Mica window background, acrylic flyouts, reveal on lists,
   subtle connected-motion transitions. This is our visual wedge.
6. **Keyboard-first for repeat users.** Global hotkeys for start/stop/pause/region; never require
   the mouse for the core loop (Nielsen #7).

## 5. Information architecture

```
App
├── SystemTrayIcon  (always-present entry point; research §5 "Windows idiom")
│     └── context menu: New Recording · Recent · Settings · Quit
│
├── RecordingOverlay  (floating, always-on-top, minimal — the "Loom stays out of the way" model)
│     ├── target selector (FullScreen / Display / Window / Region)  → CaptureOptions.Target
│     ├── mic toggle · system-audio toggle                            → CaptureOptions.Capture*
│     ├── Start / Pause / Stop                                        → RecordingSession.*
│     └── live timer + REC indicator                                  → RecordingSession.State/RecordedDurationMs
│
├── PostRecordWindow  (the "magic is obvious" moment)
│     ├── Preview  (auto-applied zoom + smoothed cursor, live)
│     ├── Quick tweaks (background, padding, cursor size, smoothing)  → CursorCustomization / SmoothingIntensity
│     ├── Aspect ratio switcher (16:9 / 9:16 / social presets)         → AspectRatioMode presets + AspectRatioConverter
│     ├── "Edit zoom" → opens TimelinePanel (collapsed by default)     → RecordingTimeline/ZoomProgram
│     └── Export button → ExportSheet
│
├── TimelinePanel  (power-user, collapsed by default — research §7.2)
│     ├── time ruler + video track + zoom-keyframe track               → RecordingTimeline.Segments + ZoomProgram.Keyframes
│     ├── drag keyframes (time/focus/scale)                            → ZoomProgram.ReplaceAt
│     ├── trim/cut                                                     → RecordingTimeline.TrimEdges/Cut
│     └── playhead scrub + snap                                        → SetPlayhead/SnapToKeyframe
│
├── ExportSheet  (Flyout/ContentDialog over PostRecordWindow)
│     ├── preset chips (1080p/60, 4K/60, 720p/60, …)                   → ExportSettingsViewModel.Presets
│     ├── "Custom" → codec/resolution/fps/bitrate                      → ExportSettingsViewModel two-way props
│     ├── validation message (binds CanExport → Export button enabled) → ExportSettingsViewModel.ValidationError/CanExport
│     ├── output path picker
│     └── progress bar + cancel (live)                                 → ExportPipeline.RunAsync (IAsyncEnumerable<ExportProgress>)
│
└── SettingsWindow  (persisted app preferences — the one big missing engine piece, see §9)
      ├── defaults (smoothing intensity, zoom sensitivity, default codec)
      ├── shortcuts (reassignable hotkeys)
      ├── audio devices (mic picker → needs device-enumeration surface, §9 gap)
      └── about / license
```

## 6. Screen specs

### 6.1 RecordingOverlay (the daily-driver surface)
- **Trigger:** tray icon click or global hotkey. Floats top-center, always-on-top, ~64px tall pill.
- **Bindings:**
  - Target selector → `CaptureOptions.Target` (FullScreen/Display/Window/Region).
  - Mic/system-audio toggles → `CaptureOptions.CaptureMicrophone`/`CaptureSystemAudio`.
  - Start/Pause/Stop → `RecordingSession.Start/Pause/Resume/Stop(nowMs)`.
  - Timer + REC dot → `RecordingSession.State` + `RecordedDurationMs` (poll or wrap in an
    observable ViewModel — see §9 gap: `RecordingSession` has no change event today).
- **States:** Idle (show controls + Start), Recording (REC dot pulsing, live timer, Pause/Stop),
  Paused (amber, Resume/Stop), Stopped (auto-transitions to PostRecordWindow).
- **WinUI:** pill uses acrylic background; always-on-top via `DesktopAcrylicController`/overlay
  window. Region select opens a click-drag full-screen picker (translucent overlay).
- **Permission handling:** if mic/screen-capture permission denied, show inline message with a
  "Open Windows Settings" deep-link (research §5).

### 6.2 PostRecordWindow (the value-reveal)
- **Layout:** left = large preview canvas; right = a tabbed "Style" panel; bottom = Export button
  + "Edit zoom" toggle.
- **Preview:** renders the composited output (zoom + smoothed cursor) using `FrameCompositor` to a
  `SwapChainPanel` (or SoftwareBitmapSource for v1 simplicity). Scrubbable via the collapsed
  timeline's playhead.
- **Style panel tabs:**
  - *Cursor* → `CursorCustomization` (size slider with `SizePresets` detents, auto-hide toggle +
    timeout, loop toggle, custom-image picker).
  - *Background* → background color/image, padding, device frame (engine gap — see §9; UI shells
    the controls, render is a v1.1 engine task).
  - *Smoothing* → `SmoothingIntensity` segmented control (None/Light/Medium/Heavy); live preview
    updates as the user changes it.
  - *Zoom* → `ZoomMode` (Auto/Manual) toggle + `ZoomDetectionOptions` sensitivity sliders
    (ZoomScale, HoldMs) for Auto; "Open timeline" for Manual.
- **Aspect ratio switcher:** prominent segmented control → `AspectRatioMode` presets; on change,
  call `AspectRatioConverter.Convert` and the preview re-renders (research §7 — social creators).
- **WinUI:** Mica window background; preview in a rounded card; Style panel as `NavigationView`
  or tabbed `Pivot`.

### 6.3 TimelinePanel (power-user, hidden by default)
- **Visibility:** collapsed until "Edit zoom" toggled; slides in below the preview.
- **Bindings:** `RecordingTimeline.Segments` (kept spans), `ZoomProgram.Keyframes` (zoom dots),
  `RecordingTimeline.PlayheadMs`.
- **Interactions:**
  - Drag a keyframe in time → `ZoomProgram.ReplaceAt(oldTime, k with { TimeMs = newT })` (snap via
    `SnapToKeyframe`).
  - Drag a keyframe's focus/scale → update `RelativeFocus`/`Scale`.
  - Trim handles at segment edges → `TrimEdges`.
  - Razor/cut tool → `Cut(from,to)`.
  - Scrub playhead → `SetPlayhead`; preview seeks.
- **WinUI:** custom `Canvas`-based track (WinUI has no built-in timeline control); ruler drawn on
  a `Canvas`; keyframes as draggable `Thumb`-style elements. Keep it genuinely light — this is
  *not* Camtasia's multi-track editor.

### 6.4 ExportSheet
- **Form:** `ContentDialog` or a flyout panel over PostRecordWindow.
- **Bindings:** `ExportSettingsViewModel` (the one existing INPC type — two-way bind every prop).
- **Layout:** preset chips across the top (one-click apply → `ApplyPreset`); "Custom" expands
  codec/resolution/fps/bitrate fields; output-path picker; validation text bound to
  `ValidationError`; Export button `IsEnabled = CanExport`.
- **Progress:** on Export, `ExportPipeline.RunAsync` yields `ExportProgress`; bind `Percent` to a
  `ProgressBar`, show frame `X/Total`, Cancel button wired to the `CancellationToken`.
- **WinUI:** `ContentDialog` with `DefaultButton=Primary`; progress as `ProgressBar` +
  indeterminate state while finalizing.

### 6.5 SettingsWindow
- **Scope:** app-level defaults that persist across sessions (the engine has no persistence layer
  today — see §9 gap). Hosts: default smoothing/zoom/codec, hotkey reassignment, mic device picker,
  about/license.
- **WinUI:** standard `SettingsPage` with `SettingsCard` controls (Fluent).

## 7. State & data flow (how the UI wires the engine)

```
RecordingOverlay
   │ user clicks Start
   ▼
RecordingSession.Start(nowMs) ──► IScreenCapture.Start(CaptureOptions)
   │                                 │ frames + cursor events stream (IObservable)
   │                                 ▼
   │                              CursorEventLogger.Log(each cursor event)
   │
   │ user clicks Stop
   ▼
RecordingSession.Stop → PostRecordWindow opens with {raw frames, raw cursor log}
   │
   │ user adjusts Style (smoothing/cursor/aspect)
   ▼
CursorSmoother.Smooth(rawCursor)  ──► ZoomDetector.Detect → ZoomProgram
AspectRatioConverter.Convert (if aspect changed)
   │
   │ preview re-renders via FrameCompositor.Compose per visible frame
   ▼
[user may open TimelinePanel → edits ZoomProgram/Segments]
   │
   │ user clicks Export
   ▼
ExportPipeline.RunAsync(timeline, rawCursor, smoother, cursorStyle, ExportSettings)
   │ yields ExportProgress → ExportSheet progress bar
   ▼
FfmpegEncoder (or MediaFoundationEncoder) → real MP4 on disk
```

## 8. ViewModels to build (the missing MVVM layer)

The engine is mostly non-observable POCOs. The App project must add these ViewModels (all
`INotifyPropertyChanged`, wrapping the engine types — *not* reimplementing them):

| ViewModel | Wraps | Surface it provides |
|---|---|---|
| `RecordingViewModel` | `RecordingSession` + `IScreenCapture` | Start/Pause/Stop commands; live `State` + `Elapsed`; mic/target selection; raises state-changed events the engine lacks |
| `TimelineViewModel` | `RecordingTimeline` + `ZoomProgram` | observable keyframe & segment collections; drag/trim/cut commands; playhead |
| `CursorSettingsViewModel` | `CursorCustomization` | size/auto-hide/loop/custom-image two-way props |
| `ZoomSettingsViewModel` | `ZoomDetector.Options` + `ZoomMode` | auto/manual toggle; sensitivity sliders |
| `ExportSettingsViewModel` | *(exists)* | already INPC — use directly |
| `ExportProgressViewModel` | `ExportPipeline.RunAsync` stream | Percent, Frame/Total, Cancel command |

## 9. Engine gaps the UI requires (cross-team follow-ups)

These are surfaced by the UI design but don't yet exist in Core/Native — they're prerequisites for
a fully-wired UI, not UI work itself:

1. **Persistence layer** — `ExportSettings`, `CursorCustomization`, `ZoomDetectionOptions`,
   hotkey map, default device choices are all in-memory only. Need a settings store (JSON file or
   `ApplicationData.Current.LocalSettings`) for SettingsWindow to load/save.
2. **Observable state on `RecordingSession`** — it exposes only a polling `State` property with no
   changed event; `RecordingViewModel` needs an event or must poll on a timer.
3. **Device enumeration** — `CaptureOptions.TargetHandle`/`CaptureTarget.Window` exist but there's
   no window/monitor enumeration API. `FfmpegAudioCapture.ListMicrophones()` exists for mics; the
   WASAPI/WGC path has none. Need an enumeration surface for the target + mic pickers.
4. **Background / device-frame rendering** — `AspectRatioConverter` handles framing math, but
   there's no engine module to render a background color/image or device frame around the capture.
   PostRecordWindow's Background tab depends on it (can ship as v1.1).
5. **Webcam capture** — no webcam PiP surface at all (PRD P1). Defer.
6. **WinUI project + WindowsAppSDK reference** — `src/ScreenStudio.App/` is empty, not in the .sln,
   and needs VS Build Tools / the Windows App SDK workload to compile XAML (PRI-generation
   targets require VS Appx tooling). This is the hard prerequisite to start UI build.

## 10. Accessibility (WinUI)

- Full keyboard navigation (Tab/Shift-Tab order, Enter/Space activation, Esc cancels dialogs).
- `AutomationProperties.Name` on every interactive control; recording state announced via
  `LiveRegion`/`Peer`.
- High-contrast theme support (Fluent does this by default if we don't hardcode colors).
- Min target size 32×32 (Fluent standard); timer/REC indicator not color-only (pair with icon/text
  for colorblind users — Nielsen #1 robustly).

## 11. Success metrics

- **Time-to-first-MP4** on a clean install: < 2 minutes (G1).
- **Default-path recording requires 0 non-permission clicks** between "open app" and "a polished
  MP4 exists" (G2).
- **Power-user path** (manual zoom edit) reachable in ≤ 2 clicks from PostRecordWindow (G4).
- No UI thread blocking during capture/export — all engine calls marshaled off the UI thread
  (capture is `IObservable` on a background thread; export is `IAsyncEnumerable` + `CancellationToken`).

## 12. Phasing (indicative — not a task list)

- **Phase U1 — App shell + RecordingOverlay + tray.** Get to "can record raw on Windows via the
  UI." Resolves gap §9.6 (App project + workload) first.
- **Phase U2 — PostRecordWindow + ExportSheet.** "Record → polished MP4 via UI," auto effects
  only (no manual timeline). Uses `ExportSettingsViewModel` as-is.
- **Phase U3 — Style panel + aspect switcher + live preview.** Cursor/background/smoothing/zoom
  controls; `CursorSettingsViewModel`/`ZoomSettingsViewModel`.
- **Phase U4 — TimelinePanel (power-user).** Keyframe drag, trim/cut.
- **Phase U5 — SettingsWindow + persistence.** Resolves gap §9.1; device pickers (§9.3).

Each phase is independently shippable and demoable.

## Open questions
1. **Pricing gate in-UI?** Free local export, paywall for pro (4K, device frames, batch)? Or fully
   free v1 with no gate? *Recommendation: no gate in v1; decide before public release.*
2. **Region picker UX:** click-drag overlay vs. numeric fields vs. preset regions? *Recommendation:
   click-drag overlay (Screen Studio model), numeric fields in Advanced.*
3. **Preview renderer:** `SwapChainPanel` (GPU, fast, complex) vs. `SoftwareBitmapSource`
   (CPU, simple, slower)? *Recommendation: SoftwareBitmapSource for Phase U2/U3; SwapChainPanel
   when perf demands it.*
4. **Single-window vs. multi-window:** separate windows for PostRecord/Timeline/Settings, or tabs
   in one window? *Recommendation: one main window with the timeline as an inline collapsible
   panel; Settings as a separate window (standard Windows idiom).*
