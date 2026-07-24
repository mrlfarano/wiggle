# PRD: Screen Studio Clone

## Project Overview
**Name:** screenstudio-clone
**Goal:** Build a professional screen recorder for Windows 11 that automatically makes recordings look engaging and polished
**Platform:** Windows 11
**Tech Stack:** C# / .NET + WinUI 3 (native — see Architecture)

## Problem Statement
Creating professional screen recordings typically requires:
- Manual video editing work (zooms, cuts, effects)
- Multiple tools (screen recorder + video editor)
- Time and skill to produce engaging content
- Difficulty creating content for different platforms (vertical vs horizontal)

Screen Studio automates these tasks, making professional-looking recordings accessible to anyone.

## Core Value Proposition
*Record once, get a polished video automatically.* The app applies professional effects (zooms, smooth cursor, animations) that would otherwise take hours of manual editing.

## Target Users
1. **Product marketers** - Creating product demos and promotional content
2. **Course creators** - Recording tutorials and lessons
3. **Content creators** - Making social media content (TikTok, Instagram, YouTube Shorts)
4. **Customer support** - Creating how-to guides and walkthroughs
5. **Developers** - Recording app demos and technical tutorials

## Key Features (Priority Order)

### P0 - Must Have (MVP)
1. **Screen Recording Engine**
   - Record full screen or selected area
   - High quality capture (60fps target)
   - Low CPU overhead during recording

2. **Automatic Cursor Zoom**
   - Detect cursor actions and automatically zoom in
   - Smooth zoom transitions
   - Timeline-based zoom editing

3. **Smooth Cursor Movement**
   - Transform shaky/jerky cursor motion into smooth curves
   - Configurable smoothing intensity
   - Real-time preview

4. **Basic Export**
   - Export as MP4 (H.264)
   - Resolution options (1080p, 4K)
   - Progress indicator

5. **Timeline Editor**
   - Visual timeline of recording
   - Trim/cut functionality
   - Drag to adjust zoom points

### P1 - Important (Post-MVP)
1. **Cursor Customization**
   - Adjust cursor size
   - High-resolution cursor replacement
   - Auto-hide static cursor
   - Loop cursor position (for looping videos)

2. **Audio Recording**
   - Microphone recording with noise reduction
   - System audio capture
   - Audio normalization
   - Volume control

3. **Webcam Recording**
   - Overlay webcam on recording
   - Picture-in-picture mode
   - Automatic zoom out when cursor active

4. **Aspect Ratio Modes**
   - Horizontal (16:9, landscape)
   - Vertical (9:16, portrait)
   - Auto-adjust all zooms when switching
   - Social media presets

5. **Visual Customization**
   - Background color/image
   - Outer spacing/padding
   - Drop shadows
   - Inset/border effects

### P2 - Nice to Have
1. **Motion Blur**
   - Natural motion blur on cursor
   - Configurable intensity

2. **Keyboard Shortcut Display**
   - Detect and display pressed keys
   - Stylish keycap visualization
   - Position customization

3. **iOS Device Recording**
   - Connect iPhone/iPad via USB
   - Auto-detect device model/color
   - Device frame overlays
   - Sync with screen recording

4. **Advanced Export**
   - GIF export
   - WebM export
   - Custom bitrate/quality settings
   - Export presets (social media, web, etc.)

5. **Share & Collaboration**
   - Generate shareable links
   - Copy to clipboard
   - Direct upload to platforms
   - Cloud save/sync

6. **Transcript Generation**
   - Local speech-to-text
   - Subtitle/caption generation
   - Export transcripts

7. **Additional Effects**
   - Hide desktop icons
   - Crop recording area
   - Speed up/slow down sections
   - Picture-in-picture adjustments

## Technical Architecture (Draft)

### Core Components
```
┌─────────────────────────────────────────────────┐
│                    UI Layer                       │
│  (Recording UI, Timeline, Preview, Export)     │
└─────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────┐
│              Recording Engine                    │
│  (Screen capture, Cursor tracking, Audio)        │
└─────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────┐
│            Processing Pipeline                   │
│  (Zoom detection, Smoothing, Effects render)     │
└─────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────┐
│               Export Engine                      │
│  (Encoding, Format conversion, Upload)           │
└─────────────────────────────────────────────────┘
```

### Technology Options
- **C# / .NET 8 + WinUI 3 (CHOSEN)**: Native Windows 11 stack. First-class access to Windows Graphics Capture (WGC) for 60fps GPU-accelerated capture, Media Foundation for HW video encode (NVENC/QSV/AMF), and WASAPI for system + mic audio. Best performance, fully buildable/runnable on Windows. Tradeoff: Windows-only.
- **Tauri + React/Next.js**: React UI (great for the timeline), Rust backend hits Windows APIs via `windows-rs`, cross-platform potential. More bridging effort for capture/encode.
- **Electron + React**: Largest ecosystem, ffmpeg-based capture/encode, but heaviest runtime overhead — works against the "responsive during recording" success metric.
- **Flutter**: Cross-platform desktop; weaker native-media story on Windows (needs platform channels to WinRT/C++).

### Key Technical Challenges
1. **Real-time cursor tracking** - High-precision cursor position logging via `WH_MOUSE_LL` low-level hook or `GetCursorInfo` polling, timestamped for processing
2. **Smooth path algorithms** - Catmull-Rom splines or similar for cursor smoothing
3. **Zoom detection** - Heuristics for determining when/where to zoom
4. **Rendering performance** - Effects should be fast, not blocking UI; leverage GPU (DirectX/Direct2D via WinUI swap chain) for compositing
5. **Export optimization** - Balance quality vs file size vs encoding time; Media Foundation HW encode (NVENC/QSV/AMF) when available

## Success Metrics
- Time from recording to polished export < 5 minutes
- App remains responsive during recording
- Export quality indistinguishable from manual editing
- User can create first successful video within 15 minutes of first use

## Open Questions
1. **Platform strategy** - Windows 11 only for v1 (decided); cross-platform (macOS/Linux) is explicitly deferred.
2. **Business model** - One-time purchase, subscription, or freemium?
3. **Target release date** - When do we want MVP ready?
4. ~~Tech stack final decision~~ - **RESOLVED: C# / .NET + WinUI 3** (see Architecture).
