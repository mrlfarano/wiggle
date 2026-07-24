# Phase 0: Foundation - Technical Decisions & Project Setup

## Overview
**Duration:** 1 week
**Complexity:** Low
**Priority:** P0 (Highest)

## Goal
Make all technical decisions and set up project infrastructure to enable all subsequent development phases.

## Tasks

### 1. Tech Stack Decision
**Decision:** Swift/SwiftUI (Recommended)

**Evaluation Criteria:**
- Performance: Native macOS APIs (AVFoundation, Core Animation) are required anyway
- Distribution: Mac App Store vs direct download
- Team skills: Swift vs Rust vs JavaScript/TypeScript
- Cross-platform: macOS only initially, Windows/Linux later?

**Options Analysis:**
| Option | Pros | Cons |
|--------|------|------|
| **Swift/SwiftUI** | Native performance, best macOS integration, direct AVFoundation access | macOS only, steeper learning curve if team lacks Swift experience |
| **Tauri + React** | Lightweight, modern, Rust backend, web tech for UI | More complex build, indirect access to macOS APIs |
| **Electron + React** | Familiar web stack, large ecosystem | Heavy resource usage, slower performance |

**Decision:** Swift/SwiftUI for maximum performance and native macOS integration.

### 2. Project Setup
- Initialize Xcode project with proper bundle identifier
- Set up Git repository with `.gitignore` for Xcode build artifacts
- Configure GitHub Actions or CI/CD for automated builds
- Define code style guide (Swift conventions, naming patterns)
- Set up documentation structure

### 3. Architecture Finalization

**Component Interfaces:**
```
RecordingEngine
  - startRecording()
  - stopRecording()
  - pauseRecording()
  - getFrames() -> [VideoFrame]

CursorTracker
  - startTracking()
  - stopTracking()
  - getEvents() -> [CursorEvent]

ProcessingPipeline
  - applySmoothing(events: [CursorEvent]) -> [SmoothedPath]
  - detectZoomPoints(events: [CursorEvent]) -> [ZoomKeyframe]

ExportEngine
  - export(settings: ExportSettings) -> Progress

TimelineStore
  - trim(start: Time, end: Time)
  - addZoomKeyframe(keyframe: ZoomKeyframe)
```

**Data Structures:**
```swift
struct VideoFrame {
  let timestamp: TimeInterval
  let image: CGImage
  let cursorEvent: CursorEvent?
}

struct CursorEvent {
  let timestamp: TimeInterval
  let location: CGPoint
  let type: EventType // move, click, drag
  let pressure: Float
}

struct ZoomKeyframe {
  let startTime: TimeInterval
  let endTime: TimeInterval
  let zoomLevel: Float
  let focusPoint: CGPoint
  let easing: EasingFunction
}
```

**Technology Choices:**
- **Screen Capture:** CGDisplayStream or Screen Capture Kit (macOS 12.3+)
- **Rendering:** Core Animation for UI, AVFoundation for video encoding
- **Video Encoding:** VideoToolbox (hardware accelerated H.264)
- **Storage:** Circular buffer for frames during recording, file-based for timeline

## Deliverable
- Running app with basic window structure
- All technical decisions documented
- Architecture diagram approved
- CI/CD pipeline functioning

## Success Criteria
- [ ] Tech stack decision made and documented
- [ ] Xcode project builds and runs
- [ ] Component interfaces defined
- [ ] Data structures for cursor, timeline, zoom finalized
- [ ] CI/CD successfully builds the app

## Open Questions
1. Minimum macOS version target? (Recommended: macOS 13.0+ for Screen Capture Kit)
2. Team Swift experience level?
3. Mac App Store distribution required?
