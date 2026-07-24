# Imperial Commander — Setup & Task Store

This repo uses [Imperial Commander](https://github.com/mrlfarano/ImperialCommander) (`impcom`)
for AI-driven task orchestration: PRD/spec workflow, task parsing, dependency management, and a
local visualization board.

## Install

```bash
npm install -g imperial-commander     # provides the `impcom` CLI
impcom --version                       # verify
```

Requires Node.js 22+. This repo was initialized with `impcom init`.

## Provider / model configuration

API keys are read from the **environment** (never committed to `config.json`). Copy
`.env.example` to `.env` (gitignored) and fill in your provider key:

```bash
cp .env.example .env
# then edit .env:
#   ANTHROPIC_API_KEY=sk-ant-...
#   OPENAI_API_KEY=sk-...
```

Models are configured in `.imperial-commander/config.json` under `models.{main,research,fallback}`.
Inspect/update from the CLI:

```bash
impcom models                          # show configured models + key presence
impcom models --set-main <model> --provider anthropic
```

> **Note:** `config.json` deliberately contains NO `apiKey` fields. Keys live only in the
> local `.env` / environment. If a plaintext key ever appears in `config.json`, remove it.

## Task store format

Tasks live in **`.imperial-commander/tasks/tasks.json`** — a tag-keyed JSON store (the real
`impcom` format), e.g.:

```json
{
  "master": {
    "tasks": [ { "id": "001", "title": "...", "description": "...", "status": "done",
                  "dependencies": [], "priority": "high",
                  "complexity": { "score": 7, "level": "medium",
                                  "recommendedSubtasks": 0, "reasoning": "..." } } ],
    "metadata": { "created": "...", "updated": "...", "description": "..." }
  }
}
```

### Legacy YAML → JSON migration

This repo originally held per-task `tasks/*.yml` files (an older format). They were migrated
into the JSON store by `scripts/migrate-tasks.mjs`, which preserves id/title/description/
dependencies/status/priority/complexity. The original `.yml` files are retained for history
(their rich verification notes are duplicated into `tasks.json` descriptions).

To re-run the migration (idempotent — overwrites `tasks.json` from the YAMLs):

```bash
node scripts/migrate-tasks.mjs
```

## Common commands

```bash
impcom list                            # list all tasks for the current tag
impcom table                           # color-coded dashboard w/ deps, status, complexity
impcom show <id>                       # full task detail
impcom set-status <id> <status>        # pending | in-progress | done | ...
impcom add-task --title "..." --description "..." --priority high
impcom board                           # local web visualization (SSE, dependency graph)
impcom generate                        # export all tasks to tasks.generated.yaml
impcom roadmap                         # milestone summary
```

## Current state

9 tasks (001–009), all `done`, migrated from the legacy YAML with the full Screen Studio Clone
dependency graph intact (001 → 002 → {003, 008} → 004 → {005, 007} → 006 → 009).

## MCP server (agent integration)

Imperial Commander ships an MCP server (`impcom-mcp` / `dist/mcp-server.js`) exposing 7 tools
(`list`, `next`, `get-task`, `set-status`, `update-subtask`, …) over stdio, so an agent can
read/mutate the task store directly. It is registered in `~/.zcode/cli/config.json` under
`mcp.servers.imperial_commander`:

```jsonc
"imperial_commander": {
  "command": "C:\\nvm4w\\nodejs\\node.exe",
  "args": ["C:\\Users\\me\\AppData\\Roaming\\npm\\node_modules\\imperial-commander\\dist\\mcp-server.js"],
  "env": {
    "IMPERIAL_PROJECT_ROOT": "C:\\Users\\me\\Dev\\screenstudio",
    "IMPERIAL_MCP_TOOLS": "all",
    "IMPERIAL_MCP_TIMEOUT": "120"
  }
}
```

> **Restart ZCode after changing this config** — MCP servers connect at session start, so edits
> take effect on the next launch. The `mcp__imperial_commander__*` tools then become callable.

### ⚠️ Windows path patch (upstream bug)

The upstream MCP server (`dist/mcp-server.js`) has a POSIX-only check in
`resolveAgentProjectRoot`:

```js
if (!decoded.startsWith("/")) throw new Error(`Project root must be absolute: ${decoded}`);
```

This rejects Windows absolute paths (`C:\…`), so the MCP tools error with *"Project root must be
absolute"* even though the path is absolute. The CLI (`impcom`) is unaffected — it uses a
different, Windows-compatible resolver.

A local patch is applied by `scripts/patch-mcp-windows.mjs`, which widens the check to also
accept drive-letter (`C:\` / `C:/`) and UNC (`\\`) paths. Re-run it after any
`npm update imperial-commander` (the patch is to the installed package, not this repo):

```bash
node scripts/patch-mcp-windows.mjs
```

Verified after patching: `list` and `get-task` return the repo's 9 tasks correctly over MCP.
Consider contributing the one-line fix upstream.
