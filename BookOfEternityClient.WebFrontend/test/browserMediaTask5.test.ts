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

const worldRouteSource = readSource('routes', 'WorldRoute.tsx');
assert(worldRouteSource.includes("import { SceneHero } from '../components/SceneHero';"), 'WorldRoute should import SceneHero.');
assert(worldRouteSource.includes("import { useSceneImage } from '../hooks/useSceneImage';"), 'WorldRoute should import useSceneImage.');
assert(worldRouteSource.includes("const locationImage = useSceneImage(game?.narrative.imagePrompt, game?.media.gallery ?? [], 'location', game?.world.location);"), 'WorldRoute should derive a location hero image from useSceneImage using the current location identity.');
const locationHookIndex = worldRouteSource.indexOf('const locationImage = useSceneImage(');
const worldReadyGuardIndex = worldRouteSource.indexOf('if (!readyState) {');
assert(locationHookIndex !== -1 && worldReadyGuardIndex !== -1 && locationHookIndex < worldReadyGuardIndex, 'WorldRoute should call useSceneImage before early returns to preserve hook order.');
assert(worldRouteSource.includes('<SceneHero'), 'WorldRoute should render SceneHero.');
assert(worldRouteSource.includes('imageUrl={locationImage.url}'), 'WorldRoute should pass the derived location image into SceneHero.');
assert(worldRouteSource.includes('loading={locationImage.loading}'), 'WorldRoute should pass the loading state into SceneHero.');
assert(worldRouteSource.includes('eyebrow="Мир"'), 'WorldRoute should show the world eyebrow in SceneHero.');
assert(worldRouteSource.includes("title={game.world.location || 'Локация уточняется'}"), 'WorldRoute should show the current location in SceneHero.');
assert(worldRouteSource.includes("subtitle={`${game.world.worldTime || 'время уточняется'} · Ход ${game.world.turnNumber}`}"), 'WorldRoute should show world time and turn number in SceneHero.');
assert(!worldRouteSource.includes('className="world-location-hero"'), 'WorldRoute should remove the legacy inline location hero container.');
