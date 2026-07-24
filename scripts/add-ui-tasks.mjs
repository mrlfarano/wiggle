// Append the UI-layer tasks (010–015) to the impcom task store, derived from
// .imperial-commander/docs/ui-prd.md (§12 Phasing, §8 ViewModels, §9 engine gaps).
// Idempotent: skips IDs that already exist.
import { readFileSync, writeFileSync } from 'node:fs';

const STORE = '.imperial-commander/tasks/tasks.json';
const store = JSON.parse(readFileSync(STORE, 'utf8'));
const existing = new Set(store.master.tasks.map(t => t.id));

const cx = (level, score) => ({ score, level, recommendedSubtasks: 0, reasoning: 'Derived from ui-prd.md phasing.' });

const uiTasks = [
  {
    id: '010',
    title: 'Scaffold WinUI App project + workload',
    description: `PREREQUISITE for all UI tasks (ui-prd §9.6). Create src/ScreenStudio.App as a WinUI 3
desktop project (net10.0-windows), add it to ScreenStudio.sln, and reference Microsoft.WindowsAppSDK.
Resolve the VS Build Tools / Windows App SDK workload requirement (PRI-generation targets need VS
Appx tooling, absent from a pure dotnet SDK install). Add a DI/composition root wiring RecordingSession
+ IScreenCapture + ExportPipeline (the app shell host — ui-prd §5, currently missing entirely).
Deliverable: 'dotnet build' produces a launchable (empty) WinUI window; App project is in the .sln.`,
    details: 'App.xaml, MainWindow.xaml, Program.cs/composition root. Empty window shell only.',
    testStrategy: 'App builds and launches an empty WinUI window with Mica background.',
    dependencies: [],
    status: 'pending',
    priority: 'high',
    complexity: cx('high', 7),
    tags: ['ui', 'scaffold', 'windows'],
  },
  {
    id: '011',
    title: 'RecordingOverlay + system tray',
    description: `UI PRD §6.1 + Phase U1. The daily-driver floating control: target selector
(FullScreen/Display/Window/Region → CaptureOptions.Target), mic + system-audio toggles
(→ CaptureOptions.Capture*), Start/Pause/Stop (→ RecordingSession.*), live REC indicator + timer
(→ RecordingSession.State / RecordedDurationMs). System tray icon entry point (New/Recent/Settings/Quit).
Build RecordingViewModel (INPC) wrapping RecordingSession — the engine exposes only polling State
today (ui-prd §9.2 gap), so the VM must surface state-change. Always-on-top acrylic pill.
Permission handling: inline message + Windows-Settings deep-link when mic/screen-capture denied.
Bindings: CaptureOptions, RecordingSession, IScreenCapture, CursorEventLogger.`,
    details: 'RecordingViewModel.cs; RecordingOverlay.xaml; tray icon; region click-drag picker.',
    testStrategy: 'VM state transitions unit-tested; overlay launches, records raw via UI, stops cleanly.',
    dependencies: ['010'],
    status: 'pending',
    priority: 'high',
    complexity: cx('high', 8),
    tags: ['ui', 'recording', 'windows'],
  },
  {
    id: '012',
    title: 'PostRecordWindow + ExportSheet (auto effects)',
    description: `UI PRD §6.2 + §6.4 + Phase U2. The value-reveal window: large preview rendering
auto-applied zoom + smoothed cursor (FrameCompositor.Compose → SoftwareBitmapSource for v1), with
the Export button + collapsed-by-default "Edit zoom" toggle. ExportSheet as ContentDialog over it:
preset chips (→ ExportSettingsViewModel.Presets), Custom expand, validation bound to
ValidationError/CanExport, output-path picker, progress bar (→ ExportPipeline.RunAsync IAsyncEnumerable
<ExportProgress>) + Cancel (CancellationToken). Uses the existing ExportSettingsViewModel as-is.
This delivers the happy path: record → polished MP4 via UI, auto effects only (no manual timeline).`,
    details: 'PostRecordWindow.xaml; ExportSheet.xaml; preview seeks to ~25% to show a zoom immediately.',
    testStrategy: 'End-to-end via UI: record → stop → preview shows zoom → export → valid MP4 on disk.',
    dependencies: ['011'],
    status: 'pending',
    priority: 'high',
    complexity: cx('high', 8),
    tags: ['ui', 'export', 'preview'],
  },
  {
    id: '013',
    title: 'Style panel + aspect switcher + live preview',
    description: `UI PRD §6.2 Style tabs + Phase U3. Cursor tab (→ CursorCustomization: size slider
with SizePresets detents, auto-hide toggle+timeout, loop, custom-image picker), Background tab
(background color/image, padding, device frame — engine gap ui-prd §9.4, can shell controls with
render deferred to a follow-up), Smoothing tab (→ SmoothingIntensity segmented None/Light/Medium/
Heavy, live preview update), Zoom tab (→ ZoomMode Auto/Manual toggle + ZoomDetectionOptions
sensitivity sliders). Prominent aspect-ratio switcher (→ AspectRatioMode presets; on change call
AspectRatioConverter.Convert and re-render preview). Build CursorSettingsViewModel +
ZoomSettingsViewModel (INPC wrappers — ui-prd §8).`,
    details: 'CursorSettingsViewModel.cs; ZoomSettingsViewModel.cs; StylePanel.xaml tabs.',
    testStrategy: 'VM two-way binding unit-tested; each tweak shows instant preview feedback.',
    dependencies: ['012'],
    status: 'pending',
    priority: 'medium',
    complexity: cx('medium', 6),
    tags: ['ui', 'style', 'cursor', 'zoom'],
  },
  {
    id: '014',
    title: 'TimelinePanel (power-user keyframe editor)',
    description: `UI PRD §6.3 + Phase U4. Collapsed until "Edit zoom" toggled; slides in below preview.
Canvas-based track (WinUI has no built-in timeline control): time ruler + video track + zoom-keyframe
track (→ RecordingTimeline.Segments + ZoomProgram.Keyframes). Interactions: drag keyframe in time
(→ ZoomProgram.ReplaceAt + SnapToKeyframe), drag focus/scale, trim handles (→ TrimEdges), razor/cut
(→ Cut), scrub playhead (→ SetPlayhead; preview seeks). Build TimelineViewModel (INPC, observable
keyframe/segment collections — ui-prd §8). Keep genuinely light — NOT Camtasia's multi-track editor.`,
    details: 'TimelineViewModel.cs; TimelinePanel.xaml; custom Canvas track + draggable keyframe thumbs.',
    testStrategy: 'VM commands unit-tested; drag a keyframe, trim, cut — durations update correctly.',
    dependencies: ['012'],
    status: 'pending',
    priority: 'medium',
    complexity: cx('high', 7),
    tags: ['ui', 'timeline', 'editor'],
  },
  {
    id: '015',
    title: 'SettingsWindow + persistence layer',
    description: `UI PRD §6.5 + Phase U5 + resolves ui-prd §9.1 (persistence) and §9.3 (device
enumeration). App-level defaults that persist across sessions (JSON file or ApplicationData
.LocalSettings): default smoothing/zoom/codec, hotkey map, mic device. Standard SettingsPage with
SettingsCard controls (Fluent). Add the persistence layer the engine lacks (ExportSettings,
CursorCustomization, ZoomDetectionOptions are in-memory only). Add window/monitor + mic device
enumeration surfaces for the target/mic pickers (FfmpegAudioCapture.ListMicrophones exists for mics;
WGC/WASAPI path has none — ui-prd §9.3).`,
    details: 'SettingsService (JSON persist); DeviceEnumeration surface; SettingsWindow.xaml.',
    testStrategy: 'Settings round-trip (set → restart → values restored); device list populates.',
    dependencies: ['011'],
    status: 'pending',
    priority: 'medium',
    complexity: cx('medium', 6),
    tags: ['ui', 'settings', 'persistence'],
  },
];

let added = 0;
for (const t of uiTasks) {
  if (existing.has(t.id)) { console.log(`skip ${t.id} (exists)`); continue; }
  store.master.tasks.push({ ...t, complexity: { ...t.complexity }, subtasks: [], tags: t.tags });
  console.log(`added ${t.id} "${t.title}" deps=${t.dependencies.join(',')} [${t.status}]`);
  added++;
}

if (added > 0) {
  store.master.metadata.updated = new Date().toISOString();
  store.master.metadata.description += ' UI-layer tasks (010–015) appended from ui-prd.md phasing.';
  writeFileSync(STORE, JSON.stringify(store, null, 2) + '\n', 'utf8');
  console.log(`\nWrote ${added} UI tasks to ${STORE} (total now ${store.master.tasks.length})`);
} else {
  console.log('\nNo new tasks added.');
}
