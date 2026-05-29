export {};

import { containsTechnicalDetails, sanitizePlayerMessage } from '../src/utils/playerCopy.js';

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

function assert(condition: unknown, message: string) {
  if (!condition) {
    throw new Error(message);
  }
}

assert(containsTechnicalDetails('Файл npc_core.json не найден.'), 'Expected technical-detail detection for raw file errors.');
assert(containsTechnicalDetails('Используйте protocol JSON: mode'), 'Expected technical-detail detection for protocol and JSON diagnostics.');

const technicalOnly = sanitizePlayerMessage('npc_core.json protocol JSON: mode', 'Игровое действие обработано.');
assert(technicalOnly.hasTechnical, 'Expected fully technical text to be flagged.');
assert(technicalOnly.safe === 'Игровое действие обработано.', `Expected fully technical text to fall back, got: ${technicalOnly.safe}`);

const mixed = sanitizePlayerMessage('Игровое действие завершено. npc_core.json protocol JSON: mode', 'Игровое действие обработано.');
assert(mixed.hasTechnical, 'Expected mixed text to be flagged.');
assert(mixed.safe === 'Игровое действие завершено.', `Expected player-safe text to remain after stripping diagnostics, got: ${mixed.safe}`);
assert(!containsTechnicalDetails(mixed.safe), `Expected sanitized text to remove diagnostics, got: ${mixed.safe}`);

const safe = sanitizePlayerMessage('Игровое действие завершено.', 'Игровое действие обработано.');
assert(!safe.hasTechnical, 'Expected ordinary player copy to stay unflagged.');
assert(safe.safe === 'Игровое действие завершено.', `Expected ordinary player copy to stay unchanged, got: ${safe.safe}`);

const commandResultSource = readSource('components', 'CommandResult.tsx');
assert(commandResultSource.includes('sanitizePlayerMessage(block.text,'), 'CommandResult text blocks should sanitize player-facing text.');
assert(commandResultSource.includes('className="muted">{safe}</p>'), 'CommandResult should mute sanitized technical text.');

const actionCardSource = readSource('components', 'ActionCard.tsx');
assert(actionCardSource.includes('const { advancedEnabled } = useShell();'), 'ActionCard should read advanced mode from shell context.');
assert(/const noticeFallback = commandResult && !isSuccess\(commandResult\)\r?\n    \? 'Игровое действие сейчас недоступно\.'\r?\n    : 'Игровое действие обработано\.';/.test(actionCardSource), 'ActionCard should derive a fallback that preserves error semantics.');
assert(actionCardSource.includes('sanitizePlayerMessage(notice, noticeFallback)'), 'ActionCard notices should sanitize player-facing text with context-aware fallback.');
assert(actionCardSource.includes('advancedEnabled && <p className="muted">{notice}</p>'), 'ActionCard should keep raw diagnostics only in advanced mode.');
