# Changelog

All notable changes to **Wiggle** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v0.1.0] — 2026-07-24

### 🎬 Core Recording
- **Screen capture** — full-screen, display, window, or region at up to 60fps
  (FFmpeg gdigrab + Windows Graphics Capture GPU path)
- **Cursor tracking** — WH_MOUSE_LL low-level hook with QueryPerformanceCounter timestamps
- **Recording controls** — start / pause / resume / stop with pause-interval accounting
- **Cursor event logger** — binary append-only stream with replay support

### ✨ Effects
- **Auto cursor-zoom** — detects clicks, drags, and pauses; smoothly zooms with ease-in/out cubic
- **Cursor smoothing** — centripetal Catmull-Rom (Barry–Goldman) with None/Light/Medium/Heavy intensity
- **Click preservation** — exact click positions pinned as hard anchors through smoothing
- **Cursor customization** — size multiplier, auto-hide with fade, loop position, custom cursor image
- **Motion blur** — velocity-based fading cursor trail with configurable intensity

### 🎨 Visual
- **Backgrounds & framing** — background color, padding, drop shadow, border
- **Webcam PiP** — picture-in-picture webcam overlay with alpha compositing
- **Keyboard shortcut display** — keycap overlay showing pressed keys in real-time
- **Aspect-ratio modes** — 16:9 landscape, 9:16 portrait, TikTok/Reels/Shorts/Square presets
  with auto zoom-keyframe recalculation

### 🔊 Audio
- **Microphone + system audio capture** — FFmpeg dshow + WinRT AudioGraph/WASAPI
- **Noise gate** — threshold / hysteresis / attack / release downward expander
- **Level metering** — RMS + peak + dBFS with normalization (peak and RMS)
- **Audio mixer** — multi-track mix with resampling, up/down-mix, soft-clip

### 📝 Timeline Editor
- **Trim & cut** — segment-based editing with kept-duration accounting
- **Zoom keyframe editing** — drag keyframes in time/space, snap-to-keyframe
- **Playhead scrubbing** — scrub through the recording with live preview
- **Speed ramp** — speed up / slow down sections (2x fast-forward, 0.5x slow-mo)

### 📤 Export
- **H.264 / HEVC MP4** — via FFmpeg libx264/libx265 or Media Foundation (NVENC/QSV/AMF)
- **GIF export** — two-pass palette pipeline for high-quality animated GIFs
- **WebM export** — VP9 via libvpx
- **Resolution presets** — 720p, 1080p, 4K at 30/60fps
- **Social presets** — TikTok 9:16, YouTube 1080p, GIF 480p loop, WebM 720p
- **Progress + cancellation** — async export with live progress bar

### 🖥️ UI (WinUI 3)
- **Floating recording overlay** — minimal always-on-top chrome (REC indicator, timer, transport)
- **System tray** — icon with context menu (New Recording / Settings / Quit)
- **Post-record window** — live preview with auto-applied zoom, style panel, export sheet
- **Style panel** — tabbed Cursor / Background / Smoothing / Zoom / Effects controls
- **Timeline panel** — Canvas-based keyframe editor (collapsed by default)
- **Settings window** — persisted preferences (JSON), device pickers, defaults
- **Window target picker** — live enumeration of on-screen windows via Win32 EnumWindows

### 🧪 Testing
- **175 tests** — unit (126) + native integration (8) + E2E pipeline (15) + performance (7)
  + PRD-based user features (14) + packaging (2) + installer (3)
- **Performance validated** — 2-minute 720p clip exports in 47s (PRD target: <5 min)
- **E2E verified** — synthetic recording → smoothing → zoom → composition → real MP4

### 📦 Distribution
- **Self-contained publish** — `publish.cmd` produces an 85MB zip (no .NET runtime required)
- **Portable installer** — `install.ps1` creates Start Menu shortcut + Add/Remove Programs entry
- **Uninstaller** — `uninstall.ps1` removes all traces

### 🔧 Other
- **Transcript generation** — speech-to-text contract (SRT/JSON export); pluggable engine
- **Hide desktop icons** — toggle icon visibility during recording via registry
- **Crop recording area** — extract a sub-region of the captured frame
- **Imperial Commander** — task orchestration integrated (CLI + MCP, 22 tasks tracked)
