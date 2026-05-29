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
assert(gameRouteSource.includes("import { useSceneImage } from '../hooks/useSceneImage';"), 'GameRoute should import useSceneImage.');
assert(gameRouteSource.includes("const sceneImage = useSceneImage(game?.narrative.imagePrompt, game?.media.gallery ?? []);"), 'GameRoute should derive the scene hero image from useSceneImage even before data is ready.');
const sceneHookIndex = gameRouteSource.indexOf('const sceneImage = useSceneImage(');
const readyGuardIndex = gameRouteSource.indexOf('if (!readyState) {');
assert(sceneHookIndex !== -1 && readyGuardIndex !== -1 && sceneHookIndex < readyGuardIndex, 'GameRoute should call useSceneImage before early returns to preserve hook order.');
assert(gameRouteSource.includes('className="narrative-scene-hero"'), 'GameRoute should render a scene hero image container.');
assert(gameRouteSource.includes('Генерация образа сцены'), 'GameRoute should render a scene generation indicator.');

const componentStyles = readSource('styles', 'components.css');
for (const selector of [
  '.narrative-scene-hero {',
  '.scene-generating-indicator {',
  '@keyframes pulse {'
]) {
  assert(componentStyles.includes(selector), `components.css should include ${selector}`);
}
