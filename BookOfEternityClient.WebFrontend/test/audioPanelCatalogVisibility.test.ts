export {};

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { basename, join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, 'src', ...relativePath), 'utf-8');
}

function assertIncludes(source: string, expected: string, description: string) {
  if (!source.includes(expected)) {
    throw new Error(`${description} Missing snippet: ${expected}`);
  }
}

const audioPanel = readSource('components', 'AudioPanel.tsx');
assertIncludes(audioPanel, "const allAssetsAvailable = audio.playlists.every((p) => p.available) && audio.cues.every((c) => c.available);", 'AudioPanel should compute allAssetsAvailable.');
assertIncludes(audioPanel, '{advancedEnabled ? (', 'AudioPanel should gate the detailed audio catalog behind advanced mode.');
assertIncludes(audioPanel, ') : !allAssetsAvailable ? (', 'AudioPanel should only show the compact summary when assets are missing.');
assertIncludes(audioPanel, 'Доступно плейлистов: {audio.playlists.filter((item) => item.available).length}/{audio.playlists.length} · Подсказок: {audio.cues.filter((cue) => cue.available).length}/{audio.cues.length}', 'AudioPanel should show compact playlist and cue counts in normal mode.');
