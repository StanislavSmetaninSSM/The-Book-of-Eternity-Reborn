export {};

const fsModuleName = 'node:fs';
const pathModuleName = 'node:path';
const { readFileSync } = await import(fsModuleName);
const { basename, join } = await import(pathModuleName);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

const statusViewSource = readSource('src', 'components', 'StatusView.tsx');
const appSource = readSource('src', 'App.tsx');
const commandUiSource = readSource('src', 'styles', 'command-ui.css');

assertIncludes(statusViewSource, 'export function StatusView()');
assertIncludes(statusViewSource, 'className="status-view"');
for (const copy of ['🎭 Персонаж', '🕯️ Душа', '🗺️ Мир', '✨ Посмертие']) {
  assertIncludes(statusViewSource, copy);
}
for (const technicalSnippet of ['raw JSON', 'debug', 'control/', 'pending_', 'validationLabel']) {
  assertExcludes(statusViewSource, technicalSnippet);
}

assertIncludes(appSource, "case 'status': return <StatusView />;");
assertExcludes(appSource, 'const [sidebarOpen, setSidebarOpen]');
assertExcludes(appSource, 'className="sidebar-toggle"');
assertExcludes(appSource, 'workspace-sidebar');

assertIncludes(commandUiSource, '.status-view {');
assertIncludes(commandUiSource, '.status-card {');
assertIncludes(commandUiSource, '.status-bars {');
assertIncludes(commandUiSource, '.status-bar__track {');
assertIncludes(commandUiSource, '.status-bar__fill {');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf8');
}

function assertIncludes(source: string, expected: string) {
  if (!source.includes(expected)) {
    throw new Error(`Expected source to include: ${expected}`);
  }
}

function assertExcludes(source: string, unexpected: string) {
  if (source.includes(unexpected)) {
    throw new Error(`Expected source to exclude: ${unexpected}`);
  }
}
