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

const commandResultSource = readSource('components', 'CommandResult.tsx');
assert(commandResultSource.includes('figure className="command-result-image"'), 'CommandResult should render image blocks as figure elements.');
assert(commandResultSource.includes('<a href={block.url} target="_blank" rel="noreferrer">'), 'CommandResult should link rendered images to the full asset.');
assert(commandResultSource.includes('<img src={block.url} alt={block.altText || toPlayerFacingText(block.title, \'Изображение сцены\')} loading="lazy" />'), 'CommandResult should render actual image elements with player-facing alt fallback.');
assert(commandResultSource.includes('файл недоступен для отображения'), 'CommandResult should render the missing-image fallback copy.');
assert(commandResultSource.includes('{block.title && <figcaption>{toPlayerFacingText(block.title, \'Изображение\')}</figcaption>}'), 'CommandResult should render the image title as a caption.');

const componentStyles = readSource('styles', 'components.css');
for (const selector of [
  '.command-result-image {',
  '.command-result-image img:hover {',
  '.command-result-image figcaption {'
]) {
  assert(componentStyles.includes(selector), `components.css should include ${selector}`);
}
