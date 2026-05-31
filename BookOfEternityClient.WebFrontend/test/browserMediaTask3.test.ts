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
assert(sceneViewSource.includes("import { SceneHero } from './SceneHero';"), 'SceneView should import SceneHero.');
assert(sceneViewSource.includes("import { useSceneImage } from '../hooks/useSceneImage';"), 'SceneView should import useSceneImage.');
assert(sceneViewSource.includes('const sceneImage = useSceneImage(game.narrative.imagePrompt, game.media.gallery ?? []);'), 'SceneView should derive the scene hero image from useSceneImage.');
assert(sceneViewSource.includes('<SceneHero'), 'SceneView should render SceneHero.');
assert(sceneViewSource.includes('imageUrl={sceneImage.url}'), 'SceneView should pass the derived scene image into SceneHero.');
assert(sceneViewSource.includes('loading={sceneImage.loading}'), 'SceneView should pass the loading state into SceneHero.');
assert(sceneViewSource.includes("eyebrow={`Ход ${game.world.turnNumber}`}"), 'SceneView should show the current turn in SceneHero.');
assert(sceneViewSource.includes('title={game.theme.label}'), 'SceneView should show the theme label in SceneHero.');
assert(sceneViewSource.includes("subtitle={`${game.world.location || 'Локация уточняется'} · ${game.world.worldTime || ''}`}"), 'SceneView should show location and world time in SceneHero.');
assert(!sceneViewSource.includes('className="narrative-scene-hero"'), 'SceneView should remove the legacy inline narrative hero container.');
assert(!sceneViewSource.includes('scene-generating-indicator'), 'SceneView should remove the legacy scene generating indicator markup.');
