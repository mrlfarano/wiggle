<div align="center">

# 🎬 Wiggle

### A professional screen recorder for Windows 11

*Record once, get a polished video automatically.*

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-175%20passing-brightgreen)](#testing)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4)](#)

</div>

---

Wiggle captures your screen and automatically applies professional effects — **auto-zoom** on
cursor activity, **smooth cursor motion**, beautiful framing — so raw recordings export as
finished-looking videos. No manual editing required.

It's the Screen Studio experience, **native on Windows**.

## ✨ Features

<details>
<summary><b>🎬 Recording</b></summary>

- Full-screen, display, window, or region capture at up to 60fps
- Microphone + system audio capture with noise gate + normalization
- Webcam picture-in-picture overlay
- System tray integration with context menu
- Pause / resume with accurate duration accounting

</details>

<details>
<summary><b>✨ Auto Effects</b></summary>

- **Auto cursor-zoom** — detects clicks, drags, pauses; smoothly zooms with ease-in/out
- **Cursor smoothing** — centripetal Catmull-Rom spline (None/Light/Medium/Heavy)
- **Motion blur** — velocity-based cursor trail
- **Keyboard shortcut display** — keycap overlay for pressed keys
- **Click preservation** — exact click positions held through smoothing

</details>

<details>
<summary><b>🎨 Visual Styling</b></summary>

- Background color with padding, drop shadow, and border
- Cursor customization (size, auto-hide, loop, custom image)
- Aspect-ratio modes: 16:9, 9:16, TikTok, Reels, Shorts, Square
- Crop recording area to a sub-region

</details>

<details>
<summary><b>📝 Timeline Editor</b></summary>

- Trim and cut with segment-based editing
- Drag-to-adjust zoom keyframes with snapping
- Playhead scrubbing with live preview
- Speed ramp (2x fast-forward, 0.5x slow-mo)

</details>

<details>
<summary><b>📤 Export</b></summary>

- **H.264 / HEVC MP4** — hardware-accelerated (NVENC/QSV/AMF) or software (libx264)
- **GIF** — two-pass palette pipeline
- **WebM** — VP9 via libvpx
- Resolutions: 720p, 1080p, 4K @ 30/60fps
- Social presets: TikTok 9:16, YouTube 1080p, GIF loop, WebM
- Progress bar + cancellation

</details>

<details>
<summary><b>🔊 Audio DSP</b></summary>

- Noise gate (threshold / hysteresis / attack / release)
- Level metering (RMS + peak + dBFS)
- Peak & RMS normalization
- Multi-track mixing (mic + system) with resampling and soft-clip

</details>

## 🚀 Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- `dotnet workload install maui-windows`
- [FFmpeg](https://ffmpeg.org/download.html) on PATH

### Build & Run
```bash
git clone https://github.com/mrlfarano/wiggle.git
cd wiggle
dotnet build ScreenStudio.sln -c Release
dotnet run --project src/ScreenStudio.App --configuration Release
```

### Test
```bash
dotnet test ScreenStudio.sln -c Release
```

### Publish (self-contained zip)
```bash
publish.cmd
# → ScreenStudio-win-x64.zip (~85MB, no runtime needed on target)
```

### Install
```powershell
# Extract the zip, then:
powershell -ExecutionPolicy Bypass -File install.ps1
```

## 🏗️ Architecture

```
ScreenStudio.sln
├── src/
│   ├── ScreenStudio.Core/     Engine: effects, timeline, audio DSP, export, transcription
│   ├── ScreenStudio.Native/   Windows 11: capture, encode, audio, hooks (FFmpeg + WinRT)
│   └── ScreenStudio.App/      WinUI 3: overlay, post-record, timeline, settings
└── tests/
    ├── ScreenStudio.Core.Tests/     Unit tests (126)
    ├── ScreenStudio.Native.Tests/   Native integration (8)
    └── ScreenStudio.E2E.Tests/      End-to-end + performance + user features (41)
```

**Tech stack:** C# / .NET 10 · WinUI 3 (Windows App SDK 2.3.1) · FFmpeg 7.1 · xUnit

## 📊 Testing

175 tests across three suites:

| Suite | Tests | What it covers |
|-------|-------|----------------|
| Core | 126 | Each engine module in isolation (smoothing, zoom, audio DSP, export, etc.) |
| Native | 8 | Real device capture/encode (FFmpeg screen/audio/webcam + MF env-aware) |
| E2E | 41 | Full pipeline integration, performance profiling, PRD-based user features, packaging |

**Performance validated:** a 2-minute 720p clip exports in **47 seconds** (PRD target: <5 min).

## 📖 Documentation

Design docs, competitive research, UI specs, and user-journey maps live in [`.imperial-commander/docs/`](.imperial-commander/docs/).

## 📋 Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full v0.1.0 feature list.

## 📄 License

[MIT](LICENSE) — © 2026 Luis Arano

---

<div align="center">

*Wiggle — because your cursor deserves to look good.*

</div>
