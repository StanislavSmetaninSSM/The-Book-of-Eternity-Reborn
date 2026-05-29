export {};

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { basename, join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSrc(path: string): string {
  return readFileSync(join(frontendDir, 'src', ...path.split('/')), 'utf-8');
}

function assert(condition: unknown, message: string) {
  if (!condition) {
    throw new Error(message);
  }
}

const hook = readSrc('hooks/useShellState.ts');
assert(hook.includes('Promise.allSettled'), 'useShellState should use Promise.allSettled.');
assert(!/Promise\.all\(/.test(hook), 'useShellState should not use Promise.all.');

const banner = readSrc('components/ConnectionBanner.tsx');
assert(banner.includes('is-disconnected'), 'ConnectionBanner should expose the disconnected class.');
assert(banner.includes('loadBrowserState'), 'ConnectionBanner should reload shell state.');

const copy = readSrc('utils/playerCopy.ts');
assert(copy.includes('export function sanitizePlayerMessage'), 'playerCopy should export sanitizePlayerMessage.');
assert(copy.includes('containsTechnicalDetails'), 'playerCopy should keep containsTechnicalDetails.');

const route = readSrc('routes/GameRoute.tsx');
assert(route.includes('turn-state-card'), 'GameRoute should use the turn-state-card presentation.');
assert(!route.includes('TurnLifecycleActions'), 'GameRoute should not render TurnLifecycleActions.');

const result = readSrc('components/CommandResult.tsx');
assert(result.includes('sanitizePlayerMessage'), 'CommandResult should sanitize text blocks.');
