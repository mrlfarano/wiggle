// Append the next batch of tasks (016–022) to the impcom task store.
// Derived from the PRD's P1/P2 features that aren't yet built, plus the polish items
// flagged at the end of the UI build. Idempotent.
import { readFileSync, writeFileSync } from 'node:fs';

const STORE = '.imperial-commander/tasks/tasks.json';
const store = JSON.parse(readFileSync(STORE, 'utf8'));
const existing = new Set(store.master.tasks.map(t => t.id));

const cx = (level, score) => ({ score, level, recommendedSubtasks: 0, reasoning: 'Derived from PRD P1/P2 + UI polish items.' });

const newTasks = [
  {
    id: '016',
    title: 'Webcam overlay (PiP) capture + render',
    description: `PRD P1 #3 — Webcam Recording. Overlay the webcam on the recording as a
    picture-in-picture; auto-zoom-out (shrink PiP) when the cursor is active so it doesn't
    occlude content. Engine: add a webcam capture module (MediaFoundation MediaCapture or
    ffmpeg dshow video input) producing frames; FrameCompositor renders the PiP inset.
    UI: a webcam toggle in the RecordingOverlay + a draggable PiP in PostRecordWindow's preview.
    Deliverables: webcam capture module, PiP compositing in FrameCompositor, webcam UI toggle.`,
    dependencies: ['012'],
    status: 'pending', priority: 'medium', complexity: cx('high', 7),
    tags: ['webcam', 'pip', 'features'],
  },
  {
    id: '017',
    title: 'Visual customization: backgrounds, padding, shadows, borders',
    description: `PRD P1 #5 — Visual Customization. Background color/image behind the recording,
    outer spacing/padding, drop shadows, inset/border effects. Engine: extend FrameCompositor
    to render a background layer + padding frame + shadow + border around the captured content
    (the aspect-framing math exists in AspectRatioConverter; this adds the visual render).
    UI: complete the Background tab in StylePanel (controls exist but render was deferred —
    ui-prd §9.4 gap). Deliverables: background/padding/shadow/border render in FrameCompositor,
    Background tab wired in StylePanel.`,
    dependencies: ['013'],
    status: 'pending', priority: 'medium', complexity: cx('medium', 6),
    tags: ['visual', 'background', 'style'],
  },
  {
    id: '018',
    title: 'Motion blur on cursor movement',
    description: `PRD P2 #1 — Motion Blur. Natural motion blur trailing the cursor based on its
    velocity; configurable intensity. Engine: in the CursorSmoother/FrameCompositor path,
    sample recent cursor positions and render a fading trail (alpha-blended ghost cursors)
    proportional to speed. UI: a motion-blur intensity slider in the Cursor tab of StylePanel.
    Deliverables: motion-blur render pass, intensity control.`,
    dependencies: ['013'],
    status: 'pending', priority: 'low', complexity: cx('medium', 5),
    tags: ['effects', 'cursor', 'motion-blur'],
  },
  {
    id: '019',
    title: 'Keyboard shortcut display (keycap overlay)',
    description: `PRD P2 #2 — Keyboard Shortcut Display. Detect pressed keys (Win32
    GetAsyncKeyState / WH_KEYBOARD_LL hook) and render stylish keycap visualizations in the
    recording. Position customization. Engine: a KeyboardHook + key-event logger (parallel to
    CursorHook); FrameCompositor renders keycap glyphs. UI: a shortcuts-display toggle +
    position picker. Deliverables: keyboard hook + logger, keycap render, display controls.`,
    dependencies: ['012'],
    status: 'pending', priority: 'low', complexity: cx('medium', 6),
    tags: ['keyboard', 'overlay', 'features'],
  },
  {
    id: '020',
    title: 'Advanced export: GIF, WebM, presets',
    description: `PRD P2 #4 — Advanced Export. GIF export, WebM export, custom bitrate/quality
    settings, export presets (social media, web). Engine: extend the encoder layer — FfmpegEncoder
    already supports codec switching; add GIF (palettegen + paletteuse) and WebM (VP9) muxing,
    plus named social presets. UI: add GIF/WebM to the ExportSheet codec selector + a preset
    dropdown. Deliverables: GIF + WebM encode paths, social export presets, UI options.`,
    dependencies: ['006'],
    status: 'pending', priority: 'low', complexity: cx('medium', 5),
    tags: ['export', 'gif', 'webm', 'presets'],
  },
  {
    id: '021',
    title: 'Polish: tray context menu + window enumeration wiring',
    description: `UI polish items flagged at the end of the UI build. (1) TrayController: add the
    right-click context menu (New Recording / Recent / Settings / Quit) — the icon + activate
    work today but the menu is missing. (2) Wire WindowEnumeration.ListWindows() into the
    RecordingOverlay's target ComboBox so the "Window" option populates real on-screen windows
    (currently static labels). Deliverables: tray context menu, dynamic window-target picker.`,
    dependencies: ['011'],
    status: 'pending', priority: 'medium', complexity: cx('low', 3),
    tags: ['ui', 'polish', 'tray', 'window-picker'],
  },
  {
    id: '022',
    title: 'Runtime smoke test: launch app, validate GUI flow',
    description: `Launch ScreenStudio.App.exe in a real desktop session and validate the full GUI
    flow: window renders, RecordingOverlay works, record→stop→PostRecordWindow opens, preview
    shows auto-zoom, export produces a real MP4. Everything compiles + unit/E2E tests pass, but
    the GUI itself has never been runtime-validated in a display session. Deliverable: a manual
    or automated smoke-test pass confirming the app runs end-to-end on the desktop.`,
    dependencies: ['015'],
    status: 'pending', priority: 'high', complexity: cx('low', 3),
    tags: ['testing', 'smoke', 'runtime'],
  },
];

let added = 0;
for (const t of newTasks) {
  if (existing.has(t.id)) { console.log(`skip ${t.id} (exists)`); continue; }
  store.master.tasks.push({ ...t, details: t.details ?? '', testStrategy: t.testStrategy ?? '', complexity: { ...t.complexity }, subtasks: [], tags: t.tags });
  console.log(`added ${t.id} "${t.title}" deps=${t.dependencies.join(',')} [${t.priority}]`);
  added++;
}

if (added > 0) {
  store.master.metadata.updated = new Date().toISOString();
  store.master.metadata.description += ' Next-batch tasks (016–022) from PRD P1/P2 + polish.';
  writeFileSync(STORE, JSON.stringify(store, null, 2) + '\n', 'utf8');
  console.log(`\nWrote ${added} tasks (total now ${store.master.tasks.length})`);
} else {
  console.log('\nNo new tasks added.');
}
