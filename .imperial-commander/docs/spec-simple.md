# Project Specification

## Summary
Screen Studio Clone - A professional screen recorder for Windows 11 that automatically makes recordings look engaging and polished with automatic zoom, smooth cursor effects, and professional animations.

## Goals
- Create an opinionated screen recorder that applies professional effects automatically
- Eliminate the need for manual video editing to make recordings look good
- Enable anyone to create polished product demos, tutorials, and social media content in minutes
- Support both horizontal and vertical output for different platforms

## Requirements
- Screen Recording Engine: Capture full screen or selected area at 60fps with low CPU overhead
- Automatic Cursor Zoom: Detect cursor actions and automatically zoom in with smooth transitions
- Smooth Cursor Movement: Transform shaky cursor motion into smooth curves using spline interpolation
- Timeline Editor: Visual timeline for editing recordings with zoom keyframes and trim/cut functionality
- Export Engine: Export as MP4 (H.264) with resolution options (1080p, 4K) and progress indicator
- Cursor Customization: Adjust cursor size, high-resolution cursor replacement, auto-hide static cursor
- Audio Recording: Microphone recording with noise reduction and system audio capture
- Aspect Ratio Modes: Horizontal (16:9) and Vertical (9:16) with auto-zoom adjustment
- Visual Customization: Background color/image, outer spacing, drop shadows
- Motion Blur: Natural motion blur on cursor movement
- Keyboard Shortcut Display: Detect and display pressed keys in video
- iOS Device Recording: Record iPhone/iPad via USB with device frames
- Advanced Export: GIF export, custom bitrate settings, export presets
- Share & Collaboration: Generate shareable links, copy to clipboard
- Transcript Generation: Local speech-to-text for subtitles

## Platform
Windows 11 (native — C# / .NET 8 + WinUI 3); cross-platform expansion explicitly deferred.

## Success Criteria
- Time from recording to polished export < 5 minutes
- App remains responsive during recording
- Export quality indistinguishable from manual editing
- User can create first successful video within 15 minutes
