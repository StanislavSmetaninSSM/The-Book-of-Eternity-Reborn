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

function assert(condition: unknown, message: string) {
  if (!condition) {
    throw new Error(message);
  }
}

const sceneViewSource = readSource('components', 'SceneView.tsx');
assert(sceneViewSource.includes("formatWorldTimeForPlayer(game.world.worldTime, '')"), 'SceneView should keep localized world time in the primary scene hero.');
assert(sceneViewSource.includes('className="scene-quick-actions"'), 'SceneView should keep player-default action chips near the scene.');
assert(!sceneViewSource.includes('className="world-location-hero"'), 'SceneView should not revive the legacy inline location hero container.');

const hookSource = readSource('hooks', 'useSceneImage.ts');
assert(hookSource.includes("imageKind: 'scene' | 'location' = 'scene'"), 'useSceneImage should still support location image generation for future surfaces.');
assert(hookSource.includes('entityIdentity?: string | null'), 'useSceneImage should accept a stable entity identity for media reuse.');
assert(hookSource.includes('entityType: imageKind'), 'useSceneImage should send the requested image kind to the media API.');
