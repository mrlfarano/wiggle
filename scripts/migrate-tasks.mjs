// Migrate the legacy .imperial-commander/tasks/*.yml into the real impcom
// tag-keyed JSON store (.imperial-commander/tasks/tasks.json).
// Preserves id, title, full description, priority, status, complexity, tags, dependencies.
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { join, basename } from 'node:path';

const TASKS_DIR = '.imperial-commander/tasks';
const STORE = '.imperial-commander/tasks/tasks.json';

// Minimal YAML field extractor (our YAML is simple key:value + block scalars + flow lists).
function parseTaskYaml(text) {
  const task = { dependencies: [], tags: [], subtasks: [] };
  const lines = text.split('\n');
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const m = line.match(/^([a-zA-Z]+):\s*(.*)$/);
    if (!m) { i++; continue; }
    const [, key, rest] = m;
    if (rest === '|' || rest === '>') {
      // Block scalar: indicator on the SAME line as the key. Collect ALL following lines
      // that are indented (>=2 spaces) OR blank — blank lines are legitimate paragraph
      // breaks inside the block. Stop at the first non-indented, non-blank line (next key).
      let j = i + 1; const buf = [];
      while (j < lines.length) {
        const ln = lines[j];
        if (ln === '') { buf.push(''); j++; continue; }       // blank line: part of the block
        if (/^  /.test(ln)) { buf.push(ln.slice(2)); j++; continue; } // indented content
        break;                                                  // dedent → end of block
      }
      task[key] = buf.join('\n').trimEnd();
      i = j; continue;
    }
    if (rest === '') {
      // empty value (e.g. a key with no content / list header we don't need) — skip
      i++; continue;
    }
    if (key === 'dependencies') {
      // flow list ["001"] or ["001","002"]
      task.dependencies = rest.replace(/[[\]"]/g, '').split(',').map(s => s.trim()).filter(Boolean);
    } else if (key === 'tags') {
      task.tags = rest.replace(/[[\]]/g, '').split(',').map(s => s.trim().replace(/"/g, '')).filter(Boolean);
    } else if (['id', 'title', 'priority', 'status', 'complexity'].includes(key)) {
      task[key] = rest.replace(/^"|"$/g, '');
    }
    i++;
  }
  return task;
}

const files = readdirSync(TASKS_DIR)
  .filter(f => f.endsWith('.yml'))
  .sort();

const tasks = [];
for (const f of files) {
  const text = readFileSync(join(TASKS_DIR, f), 'utf8');
  const t = parseTaskYaml(text);
  if (!t.id) { console.warn(`skip ${f} (no id)`); continue; }
  tasks.push({
    id: t.id,
    title: t.title ?? '',
    description: t.description ?? '',
    details: '',
    testStrategy: '',
    dependencies: t.dependencies ?? [],
    status: t.status ?? 'pending',
    priority: t.priority ?? 'medium',
    // impcom expects complexity as an object with these fields (the AI assessor normally
    // populates it; for migration we derive a sensible structured value from the legacy string).
    complexity: {
      score: (t.priority === 'high') ? 7 : 5,
      level: (t.complexity === 'high') ? 'high' : 'medium',
      recommendedSubtasks: 0,
      reasoning: 'Migrated from legacy per-task YAML (no AI re-assessment).',
    },
    tags: t.tags ?? [],
    subtasks: [],
  });
  console.log(`parsed ${f} -> ${t.id} "${t.title}" [${t.status}] deps=${t.dependencies?.join(',')}`);
}

const store = {
  master: {
    tasks,
    metadata: {
      created: new Date().toISOString(),
      updated: new Date().toISOString(),
      description: 'Migrated from legacy per-task YAML into impcom tag-keyed JSON store.',
    },
  },
};

writeFileSync(STORE, JSON.stringify(store, null, 2) + '\n', 'utf8');
console.log(`\nWrote ${tasks.length} tasks to ${STORE}`);
