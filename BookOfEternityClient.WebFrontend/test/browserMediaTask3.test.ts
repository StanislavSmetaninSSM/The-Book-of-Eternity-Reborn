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

const gameRouteSource = readSource('routes', 'GameRoute.tsx');
assert(gameRouteSource.includes("import { SceneHero } from '../components/SceneHero';"), 'GameRoute should import SceneHero.');
assert(gameRouteSource.includes("import { useSceneImage } from '../hooks/useSceneImage';"), 'GameRoute should import useSceneImage.');
assert(gameRouteSource.includes("const sceneImage = useSceneImage(game?.narrative.imagePrompt, game?.media.gallery ?? []);"), 'GameRoute should derive the scene hero image from useSceneImage even before data is ready.');
const sceneHookIndex = gameRouteSource.indexOf('const sceneImage = useSceneImage(');
const readyGuardIndex = gameRouteSource.indexOf('if (!readyState) {');
assert(sceneHookIndex !== -1 && readyGuardIndex !== -1 && sceneHookIndex < readyGuardIndex, 'GameRoute should call useSceneImage before early returns to preserve hook order.');
assert(gameRouteSource.includes('<SceneHero'), 'GameRoute should render SceneHero.');
assert(gameRouteSource.includes('imageUrl={sceneImage.url}'), 'GameRoute should pass the derived scene image into SceneHero.');
assert(gameRouteSource.includes('loading={sceneImage.loading}'), 'GameRoute should pass the loading state into SceneHero.');
assert(gameRouteSource.includes("eyebrow={`Ход ${game.world.turnNumber}`}"), 'GameRoute should show the current turn in SceneHero.');
assert(gameRouteSource.includes("title={game.theme.label}"), 'GameRoute should show the theme label in SceneHero.');
assert(gameRouteSource.includes("subtitle={`${game.world.location || 'Локация уточняется'} · ${game.world.worldTime || 'время уточняется'}`}"), 'GameRoute should show location and world time in SceneHero with a fallback when time is unknown.');
assert(!gameRouteSource.includes('className="narrative-scene-hero"'), 'GameRoute should remove the legacy inline narrative hero container.');
assert(!gameRouteSource.includes('scene-generating-indicator'), 'GameRoute should remove the legacy scene generating indicator markup.');
