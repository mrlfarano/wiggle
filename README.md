# Screen Studio Clone

A professional screen recorder for **Windows 11** that automatically applies polished effects — auto-zoom on cursor activity, smooth cursor motion, beautiful framing — so raw recordings export as finished-looking videos.

*Record once, get a polished video automatically.*

## Features

- **Auto cursor-zoom** — detects clicks, drags, and pauses; smoothly zooms in with ease-in/out
- **Cursor smoothing** — centripetal Catmull-Rom spline interpolation with configurable intensity
- **Cursor customization** — size, auto-hide, loop position, custom cursor image
- **Audio** — mic + system audio capture, noise gate, normalization, multi-track mixing
- **Webcam PiP** — picture-in-picture webcam overlay
- **Visual styling** — backgrounds, padding, drop shadows, borders
- **Motion blur** — cursor velocity-based trail
- **Keyboard shortcut display** — keycap overlay for pressed keys
- **Aspect-ratio modes** — 16:9 landscape, 9:16 portrait, TikTok/Reels/Shorts presets
- **Timeline editor** — trim, cut, drag-to-adjust zoom keyframes, playhead scrubbing
- **Export** — H.264/HEVC MP4, GIF, WebM; 720p/1080p/4K; 30/60fps; social presets
- **Transcript generation** — local speech-to-text contract (SRT/JSON export)

## Tech Stack

- **C# / .NET 10** + **WinUI 3** (Windows App SDK 2.3.1)
- **Screen capture**: FFmpeg (gdigrab) + Windows Graphics Capture (GPU path)
- **Video encode**: FFmpeg (libx264/libvpx) + Media Foundation (NVENC/QSV/AMF)
- **Audio**: FFmpeg (dshow) + WinRT AudioGraph/WASAPI
- **Testing**: xUnit — 172 tests (unit + E2E integration + performance + PRD-based user features)

## Getting Started

### Prerequisites
- .NET 10 SDK
- `maui-windows` workload (`dotnet workload install maui-windows`)
- FFmpeg on PATH (for capture/encode fallbacks)

### Build & Run
```bash
dotnet build ScreenStudio.sln -c Release
dotnet run --project src/ScreenStudio.App --configuration Release
```

### Test
```bash
dotnet test ScreenStudio.sln -c Release
```

### Publish (self-contained distributable)
```bash
publish.cmd
# → produces publish/ + ScreenStudio-win-x64.zip (~85MB)
```

## Solution Structure

```
ScreenStudio.sln
├── src/
│   ├── ScreenStudio.Core/     Engine: smoothing, zoom, timeline, export, audio DSP, transcription
│   ├── ScreenStudio.Native/   Windows 11: WGC, MF, WASAPI, FFmpeg adapters, keyboard/tray hooks
│   └── ScreenStudio.App/      WinUI 3: overlay, post-record, timeline, settings
└── tests/
    ├── ScreenStudio.Core.Tests/    Unit tests (126)
    ├── ScreenStudio.Native.Tests/  Native integration tests (8)
    └── ScreenStudio.E2E.Tests/     End-to-end + performance + user-feature tests (38)
```

## Documentation

Planning docs, PRD, competitive research, UI specs, and user-journey maps are in `.imperial-commander/docs/`.

## License

MIT
