# User Journey: Screen Studio Clone (Windows 11)
**Companion to:** `ui-prd.md`, `research.md`
**Date:** 2026-07-24

This document describes **what the end user expects to experience** when using the app, mapped to
the engine and UI surfaces that deliver each moment. It is written from the user's point of view
(emotions + expectations + the single action they're trying to accomplish), then annotated with
the technical touchpoints. Use it to validate that the UI PRD covers the real journey end-to-end.

---

## 0. The one-sentence expectation

> *"I want to record my screen and get a professional-looking video without knowing anything
> about video editing."*

Everything else is a variation on this. The journey is successful when, at the end, the user has
a polished MP4 and **did not have to think about zoom keyframes, splines, or bitrates**.

---

## 1. Personas (fuller detail than the PRD's summary table)

### Maya — Product Marketer (low technical skill)
- **Job:** produce a 60-second product-demo clip for a launch tweet.
- **Prior tool:** was about to pay a freelancer, or fight with OBS + a separate editor.
- **Expectation:** "press record, talk, stop, post." She has zero interest in timelines.
- **Tolerance for friction:** very low. If the first recording isn't usable in 5 minutes, she leaves.
- **Emotional win:** *"I made this myself? It looks professional."*

### Eli — Course Creator (medium skill)
- **Job:** record a 10-minute software tutorial module.
- **Prior tool:** Camtasia (too heavy) or just a raw recording (too ugly).
- **Expectation:** clear zooms where he clicks, clean audio, ability to trim mistakes.
- **Tolerance:** moderate — will explore a light editor, won't tolerate a learning curve.
- **Emotional win:** *"My students will actually follow along because the zooms show exactly where to look."*

### Cory — Social Content Creator (medium skill)
- **Job:** a vertical (9:16) walkthrough for TikTok / Reels / Shorts.
- **Prior tool:** recorded landscape, cropped badly in a phone editor.
- **Expectation:** one-click vertical mode that re-frames the zooms automatically; click highlights.
- **Emotional win:** *"It's already vertical and the zooms still land — no manual reframing."*

### Dana — Developer (high skill)
- **Job:** a crisp app demo for a PR review or a bug repro.
- **Prior tool:** OBS (powerful but unpolished output) or a basic recorder (no zooms).
- **Expectation:** keyboard-driven, fast, precise; wants to nudge a zoom or two manually.
- **Emotional win:** *"This is the control of OBS with the output of Screen Studio."*

> All four must succeed on the **default path**. Only Dana needs the advanced surfaces — and even
> she should get a great result without touching them.

---

## 2. The primary journey — "happy path" (Maya, first-time user)

This is the journey the app is optimized for. Time budget: **< 2 minutes to a finished MP4.**

### Step 1 — Discover & install (< 1 min)
- **User expectation:** "I heard this is the Screen Studio for Windows. Let me try it."
- **Experience:** downloads/installs from the Microsoft Store or website; launches.
- **Engine/UI:** App launches to a welcoming first-run state. WinUI Mica window, minimal.
- **What must NOT happen:** a settings wall, a forced account signup, a feature tour.
  *(research §5: benefit-first, not feature-first.)*

### Step 2 — First-run permission grant (~30 sec)
- **User expectation:** "Why isn't it recording? Oh, permissions."
- **Experience:** the app proactively requests **Microphone** and **Screen capture** permissions.
  If denied, an inline message explains why and offers a one-click **"Open Windows Settings"**
  deep-link.
- **Emotional beat:** mild annoyance at the OS prompt → relief that the app made it easy.
- **Engine/UI:** `CaptureOptions.CaptureMicrophone`/screen; `IAudioCapture.IsAvailable()` /
  `IScreenCapture.IsAvailable()` gate the Start button.
- **Pitfall to avoid:** letting the user hit Start with permissions missing and getting a cryptic
  error. Surface the requirement *before* the button.

### Step 3 — Choose what to record (10 sec)
- **User expectation:** "Just record my screen. ... Actually, let me pick just this window."
- **Experience:** the **RecordingOverlay** pill shows a target selector (FullScreen / Display /
  Window / Region). Default = full screen. Maya clicks "Window" → picks her product app.
- **Engine/UI:** `CaptureOptions.Target` / `TargetHandle`. Region/Window open a click-drag picker.
- **Design note:** the default (full screen) must work with zero clicks — Maya may never touch this.

### Step 4 — Press record & present (the recording itself)
- **User expectation:** "Now I just talk through my product. Don't distract me."
- **Experience:** she hits Start (or the global hotkey). The overlay shrinks to a minimal **REC dot
  + timer + Pause/Stop**. She presents her product for 60 seconds, clicking through features.
- **Emotional beat:** confidence — the chrome is out of the way; she forgets it's recording.
  *(research §2.7: Loom's "stays out of the way" is the gold standard here.)*
- **Engine/UI:** `RecordingSession.Start` → `IScreenCapture.Start`; cursor events stream to
  `CursorEventLogger`; frames stream off the capture observable. The REC indicator is unmistakable
  (Nielsen #1 — visibility of system status).
- **What must NOT happen:** dropped frames, audio drift, UI stutter on the captured app.

### Step 5 — Stop & the "magic moment" (the value reveal)
- **User expectation:** "Okay, now I have a raw recording... wait, it already zoomed?!"
- **Experience:** she hits Stop. The **PostRecordWindow** opens with a **preview already showing
  auto-applied zooms** on her clicks and a smooth, enlarged cursor.
- **Emotional beat:** **delight and surprise** — this is the entire product thesis landing. The
  preview must look obviously better than the raw capture within 2 seconds.
- **Engine/UI:** `ZoomDetector.Detect` ran over her cursor log → `ZoomProgram`;
  `CursorSmoother.Smooth` → smoothed path; `FrameCompositor.Compose` renders the preview frame.
  Preview seeks to ~25% in (where a zoom likely occurred) to show off the effect immediately.
- **Critical success factor:** the default auto settings must produce visibly good output. If the
  first preview looks unpolished, the user assumes the app is broken.

### Step 6 — Tweak (optional, light)
- **User expectation:** "Can I make the cursor a bit bigger? ...Nice."
- **Experience:** Maya notices the Style panel. She bumps the cursor size slider; the preview
  updates live. She maybe switches the background. She does **not** open the timeline.
- **Engine/UI:** `CursorCustomization.SizeMultiplier` (with `SizePresets` detents);
  `SmoothingIntensity` segmented control; changes re-run the compositor for the visible frame.
- **Design note:** every tweak shows instant preview feedback (recognition over recall, Nielsen #6).

### Step 7 — Export (30 sec)
- **User expectation:** "Just give me the MP4."
- **Experience:** she clicks Export → **ExportSheet** → picks the "1080p / 60fps" preset chip →
  Export. A progress bar fills; a notification offers "Show in folder" when done.
- **Emotional beat:** satisfaction + completion.
- **Engine/UI:** `ExportSettingsViewModel.ApplyPreset`; `CanExport` gates the button;
  `ExportPipeline.RunAsync` yields `ExportProgress` → progress bar; `FfmpegEncoder`/`MediaFoundationEncoder` writes the MP4.
- **What must NOT happen:** a wall of codec/bitrate settings by default (progressive disclosure —
  presets first, Custom collapsed).

### Step 8 — Share
- **User expectation:** "Now I post it."
- **Experience:** "Show in folder" reveals the MP4; she drags it to her tweet.
- **Out of scope:** we do NOT build cloud sharing (research §7.6 — that's Loom/Tella's product).

**End state:** Maya has a professional demo clip. Total elapsed: ~3 minutes. She touched the
timeline zero times. **This is success.**

---

## 3. The power-user journey — Dana, manual zoom edit

Diverges from the happy path at Step 6. Dana wants control.

### Step 6′ — Open the timeline
- **Expectation:** "The auto-zoom is close, but I want to adjust this one zoom's timing."
- **Experience:** she clicks **"Edit zoom"** → the **TimelinePanel** slides in below the preview,
  showing her cursor path + zoom-keyframe dots at the detected click points.
- **Engine/UI:** `RecordingTimeline` + `ZoomProgram.Keyframes` rendered on a Canvas track.

### Step 7′ — Drag a keyframe
- **Expectation:** "Make this zoom start a touch earlier."
- **Experience:** she drags a keyframe dot left; it snaps to the ruler; the preview scrubs to that
  moment so she sees the change.
- **Engine/UI:** `ZoomProgram.ReplaceAt(oldTime, k with { TimeMs = snappedT })`;
  `RecordingTimeline.SnapToKeyframe`; `SetPlayhead`.

### Step 8′ — Trim a mistake
- **Expectation:** "Cut the first 5 seconds, I rambled."
- **Experience:** she drags the trim handle, or uses the razor tool on a selection → the kept span
  shortens; export duration updates.
- **Engine/UI:** `RecordingTimeline.TrimEdges` / `Cut`; `KeptDurationMs` reflects the edit.

She then proceeds to export as in the happy path. **The timeline never imposes itself on Maya —
it's opt-in for Dana.** (research §7.2)

---

## 4. The vertical-content journey — Cory, aspect-ratio switch

Diverges at Step 6 for social creators.

### Step 6″ — Switch to vertical
- **Expectation:** "I need this for TikTok — 9:16 — but the zooms should still work."
- **Experience:** Cory clicks the **aspect-ratio switcher** → "TikTok (9:16)". The preview
  re-frames to vertical **and the zoom keyframes auto-adjust** to the new framing. He sees the
  focus stays on his clicks.
- **Engine/UI:** `AspectRatioMode.TikTok` → `AspectRatioConverter.Convert(ZoomProgram, …)`; preview
  re-renders.
- **Emotional win:** *"I didn't have to redo anything."* (research §3 — this is table-stakes parity
  but feels magical because competitors make it manual.)

---

## 5. Edge-case journeys (must not break)

| Journey | Expectation | Engine/UI handling |
|---|---|---|
| **Permission revoked mid-session** | graceful, no crash | overlay shows "Microphone off" pill; recording continues video-only; restore re-enables |
| **Pause then resume** | the paused gap is gone in output | `RecordingSession.Pause/Resume`; export uses `IsRecordedTime` to skip it |
| **Cancel export mid-way** | clean abort, no broken file | `CancellationToken` to `RunAsync`; encoder `FinalizeStream` or delete partial |
| **Very long recording (1hr)** | stays responsive, doesn't OOM | streaming capture→disk via `CursorEventLogger`; export reads back, doesn't hold all frames |
| **First run, no mic plugged in** | still works, video-only | `IAudioCapture.IsAvailable()` false → mic toggle disabled, not an error |
| **User clicks Stop immediately (<1s)** | no zero-duration crash | `RecordingSession`/export guard against degenerate durations |

---

## 6. Emotional arc summary

```
install → permission(prompt annoyance)
       → choose target(confidence)
       → record(flow, "forgot it was recording")
       → STOP → preview reveal(DELIGHT, "it already zoomed!")
       → tweak(curiosity → satisfaction, live feedback)
       → export(anticipation)
       → file ready(completion, "I made this")
```

The **single most important moment** is the preview reveal at Step 5. If that moment doesn't
deliver visible delight, the whole product thesis fails — every design/engine decision should
protect it (fast preview seek to a zoom moment; auto settings that obviously look good).

---

## 7. Mapping journey → PRD sections

| Journey step | Delivered by (UI PRD §) | Engine touchpoint |
|---|---|---|
| 1 Discover/install | §6 App shell | — |
| 2 Permissions | §6.1 RecordingOverlay | `IsAvailable()` probes |
| 3 Choose target | §6.1 | `CaptureOptions` |
| 4 Record | §6.1 | `RecordingSession`, `IScreenCapture`, `CursorEventLogger` |
| 5 Preview reveal | §6.2 PostRecordWindow | `ZoomDetector`, `CursorSmoother`, `FrameCompositor` |
| 6 Tweak (Maya) | §6.2 Style panel | `CursorCustomization`, `SmoothingIntensity` |
| 6′ Timeline (Dana) | §6.3 | `RecordingTimeline`, `ZoomProgram` |
| 6″ Aspect (Cory) | §6.2 switcher | `AspectRatioConverter` |
| 7 Export | §6.4 ExportSheet | `ExportSettingsViewModel`, `ExportPipeline`, encoders |

The journey and the PRD are 1:1 complete — no step in the journey lacks a specified UI surface,
and no major UI surface exists without a journey purpose.

---

## 8. Success criteria for the journey (measurable)

- A first-time user (Maya) reaches a finished MP4 in **< 2 minutes** including permission grants.
- The default path requires **0 clicks on advanced controls** (timeline, custom export, zoom
  sensitivity) between "open app" and "MP4 on disk."
- The preview reveal (Step 5) shows a zoomed frame within **2 seconds** of Stop.
- Dana's manual-zoom edit (Step 6′–8′) adds **< 60 seconds** to her flow vs. the happy path.
- No journey edge-case (§5) produces a crash, hang, or broken file.
