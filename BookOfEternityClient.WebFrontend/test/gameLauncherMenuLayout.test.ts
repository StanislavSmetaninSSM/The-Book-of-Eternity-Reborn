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

const launcherSource = readSource('components', 'GameLauncher.tsx');
assert(launcherSource.includes('<nav className="launcher-menu" aria-label="Действия главного меню">'), 'GameLauncher should render a single launcher-menu nav.');
assert(launcherSource.includes("className={`launcher-menu__item${isActive ? ' is-active' : ''}${mode === primaryAction.mode ? ' is-primary' : ''}`}"), 'GameLauncher should expose active and primary launcher-menu item states.');
assert(launcherSource.includes("aria-current={isActive ? 'true' : undefined}"), 'GameLauncher menu items should expose aria-current for the active mode.');
assert(!launcherSource.includes('launcher-primary-action'), 'GameLauncher should remove the launcher-primary-action button.');
assert(!launcherSource.includes('launcher-mode-tabs'), 'GameLauncher should remove launcher-mode-tabs.');
assert(!launcherSource.includes('launcher-secondary-actions'), 'GameLauncher should remove launcher-secondary-actions.');

const componentsCss = readSource('styles', 'components.css');
assert(componentsCss.includes('.launcher-menu {'), 'components.css should define launcher-menu styles.');
assert(componentsCss.includes('.launcher-menu__item {'), 'components.css should define launcher-menu item styles.');
assert(componentsCss.includes('.launcher-menu__item.is-primary strong {'), 'components.css should highlight the primary launcher-menu item.');
assert(!componentsCss.includes('launcher-primary-action'), 'components.css should remove launcher-primary-action selectors.');
assert(!componentsCss.includes('launcher-mode-tabs'), 'components.css should remove launcher-mode-tabs selectors.');
assert(!componentsCss.includes('launcher-mode-tab'), 'components.css should remove launcher-mode-tab selectors.');
assert(!componentsCss.includes('launcher-secondary-actions'), 'components.css should remove launcher-secondary-actions selectors.');
