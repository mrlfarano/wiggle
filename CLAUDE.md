# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

This is a **pre-implementation planning workspace**, not a runnable codebase. There is currently **no source code, build system, package manifest, or test suite** — do not invent build/lint/test commands. When asked to "run" or "test," first check whether any code has been added; if not, say so rather than fabricating commands.

The project's purpose and intended behavior live entirely in the planning documents under `.imperial-commander/`. Work here is organized by the **Imperial Commander** task orchestrator (the README's "AI-driven task orchestration"). Treat the task files as the source of truth for what to build and in what order.

## Imperial Commander layout

- `.imperial-commander/config.json` — project metadata + model/provider settings for the orchestrator (e.g. `main` model, `research` model, storage mode `solo`). The model/provider config and the `OPENAI_API_KEY` / `ANTHROPIC_API_KEY` env vars (see `.env.example`) drive the **orchestrator**, not the app being built.
- `.imperial-commander/docs/prd-screenstudio-clone.md` — full Product Requirements Document with feature priorities (P0/P1/P2) and a draft component architecture.
- `.imperial-commander/docs/spec-simple.md` — condensed goals/requirements/platform/success-criteria.
- `.imperial-commander/tasks/NNN-*.yml` — one file per task. These are the unit of work.
- `.imperial-commander/templates/` — blank `spec-simple.md` / `spec-structured.md` templates for new specs.
- `.imperial-commander/runtime-state.json` — orchestrator bookkeeping (current tag/branch mapping). Avoid hand-editing unless intentionally resetting state.

### Task file schema

Each `tasks/*.yml` has: `id` (zero-padded string), `title`, multiline `description` (requirements + technical approach + deliverables), `priority` (`high`/`medium`), `status` (`pending` | …), `complexity` (`medium`/`high`), `tags`, and `dependencies` (list of task `id`s). When you start/finish a task, flip its `status` accordingly so the orchestrator and other contributors see current state. All nine existing tasks are `status: pending` — nothing has been started.

### Task dependency graph (build order)

```
001 Define Technical Architecture            (no deps — stack chosen: C#/.NET + WinUI 3; task finalizes the decision record)
 └─ 002 Recording Engine                     (deps: 001)
     ├─ 003 Cursor Smoothing                 (deps: 002)
     │   └─ 007 Cursor Customization         (deps: 003)
     ├─ 008 Audio Recording                  (deps: 002)
     ├─ 004 Automatic Cursor Zoom            (deps: 002, 003)
     │   └─ 009 Aspect Ratio Modes           (deps: 004, 005)
     └─ 005 Timeline Editor                  (deps: 002, 004)
         └─ 006 Export Engine                (deps: 003, 004, 005)
```

Higher-numbered tasks depend on earlier ones; respect `dependencies` before claiming a task can be worked on.

## Product & intended architecture

**Screen Studio clone** — a professional **Windows 11** screen recorder that automatically applies polished effects (auto-zoom, cursor smoothing, animations) so raw recordings export as finished-looking videos. Target platform is **Windows 11**. This matters for the recording/export engines, which use Windows-native APIs:

- **Screen capture** → Windows Graphics Capture (WGC) API, GPU-accelerated, 60fps target
- **Hardware video encode** → Media Foundation (NVENC / Intel QSV / AMF via the GPU)
- **System + microphone audio** → WASAPI (WASAPI loopback captures system audio)
- **High-precision cursor tracking** → `WH_MOUSE_LL` low-level mouse hook and/or `GetCursorInfo` polling, with timestamped events

The PRD sketches a layered architecture (top → bottom): **UI Layer** (recording UI, timeline, preview, export) → **Recording Engine** (screen capture, cursor tracking, audio) → **Processing Pipeline** (zoom detection, smoothing, effects render) → **Export Engine** (encoding, format conversion).

**Tech stack: C# / .NET 8 + WinUI 3** (decided — see task `001`). This was chosen for first-class access to the Windows media APIs above, best performance, and the ability to build and run everything on the Windows dev machine. macOS/Linux support is explicitly out of scope for v1.

## Working notes

- New specs should follow the templates in `.imperial-commander/templates/`. New tasks should match the existing `tasks/*.yml` schema and declare correct `dependencies`.
- Because the product targets Windows 11 and screen-recording APIs are platform-specific, verify the Windows Graphics Capture / Media Foundation / WASAPI assumptions as each engine task is implemented, and confirm what can actually be built/run on the current dev machine before promising "I've implemented feature X."
- `.gitignore` excludes `.env`, `dist/`, `coverage/` — keep secrets out of files (use the env vars from `.env.example`).
