// Patch the imperial-commander MCP server to accept Windows absolute paths.
// Upstream bug: resolveAgentProjectRoot only accepts POSIX "/"-leading paths,
// rejecting "C:\..." and "C:/...". This adds drive-letter + UNC acceptance.
import { readFileSync, writeFileSync } from 'node:fs';

const SRV = 'C:/Users/me/AppData/Roaming/npm/node_modules/imperial-commander/dist/mcp-server.js';

const OLD = 'if (!decoded.startsWith("/")) throw new Error(`Project root must be absolute: ${decoded}`);';
// Accept: POSIX "/", Windows drive "X:\" or "X:/", or UNC "\\"
const NEW = 'const _isAbs = decoded.startsWith("/") || /^[A-Za-z]:[\\\\\\/]/.test(decoded) || decoded.startsWith("\\\\"); if (!_isAbs) throw new Error(`Project root must be absolute: ${decoded}`);';

let s = readFileSync(SRV, 'utf8');
if (s.includes(NEW)) {
  console.log('already patched — no change');
  process.exit(0);
}
if (!s.includes(OLD)) {
  console.error('target string not found — upstream may have changed; aborting');
  process.exit(1);
}
s = s.replace(OLD, NEW);
writeFileSync(SRV, s, 'utf8');
console.log('patched: resolveAgentProjectRoot now accepts Windows absolute paths');
