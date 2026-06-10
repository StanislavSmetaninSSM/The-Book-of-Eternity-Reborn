export {};

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { existsSync, readFileSync, statSync } = await import(fsSpecifier);
const { basename, join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, 'src', ...relativePath), 'utf-8');
}

function readPublicAssetStats(...relativePath: string[]) {
  const assetPath = join(frontendDir, 'public', ...relativePath);
  return { assetPath, exists: existsSync(assetPath), size: existsSync(assetPath) ? statSync(assetPath).size : 0 };
}

function readPublicText(...relativePath: string[]) {
  const assetPath = join(frontendDir, 'public', ...relativePath);
  assert(existsSync(assetPath), `Expected public asset note at ${assetPath}.`);
  return { assetPath, text: readFileSync(assetPath, 'utf-8') };
}

function assert(condition: unknown, message: string) {
  if (!condition) {
    throw new Error(message);
  }
}

const launcherSource = readSource('components', 'GameLauncher.tsx');
const appSource = readSource('App.tsx');
const shellContextSource = readSource('context', 'ShellContext.tsx');

assert(appSource.includes("import { GameLauncher } from './components/GameLauncher';"), 'App.tsx should import GameLauncher so the launcher art is reachable from the runtime bundle.');
assert(appSource.includes("activeRoute === 'home'"), 'App.tsx should keep a default home/launcher route before the player enters the shell.');
assert(appSource.includes('<GameLauncher menu={menu} />'), 'App.tsx should render GameLauncher from the ready default home route.');
assert(appSource.includes('{!isLauncherRoute && <TabBar />}'), 'App.tsx should keep tab navigation out of the launcher and restore it after entering the shell.');
assert(appSource.includes('{!isLauncherRoute && !isPracticeRoute && <UnifiedInput />}'), 'App.tsx should keep the command input out of the launcher and practice training route.');
assert(shellContextSource.includes("useState<RouteId>('home')"), 'ShellContext should default the browser client to the home launcher route.');
assert(shellContextSource.includes("'practice'"), 'ShellContext should expose a standalone practice route from the launcher.');
assert(shellContextSource.includes('setActiveRouteState(tabToRoute(tab))'), 'ShellContext tab changes should transition from the launcher into the existing shell routes.');
assert(launcherSource.includes("'practice'"), 'GameLauncher should include QTE practice as a first-screen launcher mode.');
assert(launcherSource.includes("onActiveRouteChange('practice')"), 'GameLauncher should open QTE practice without entering a campaign route.');
assert(launcherSource.includes("action.id === 'qte-practice'"), 'GameLauncher should bind the practice mode to the typed main-menu action.');

assert(launcherSource.includes('<nav className="launcher-menu" aria-label="Действия главного меню">'), 'GameLauncher should render a single launcher-menu nav.');
assert(launcherSource.includes('      <div className="launcher-art-bg" aria-hidden="true">'), 'GameLauncher should render the decorative launcher background wrapper.');
assert(launcherSource.includes('        <img src="/main-menu-bg.webp" alt="" />'), 'GameLauncher should render the decorative launcher background image.');
assert(launcherSource.includes("className={`launcher-menu__item${isActive ? ' is-active' : ''}${mode === primaryAction.mode ? ' is-primary' : ''}`}"), 'GameLauncher should expose active and primary launcher-menu item states.');
assert(launcherSource.includes("aria-current={isActive ? 'true' : undefined}"), 'GameLauncher menu items should expose aria-current for the active mode.');
assert(!launcherSource.includes('launcher-primary-action'), 'GameLauncher should remove the launcher-primary-action button.');
assert(!launcherSource.includes('launcher-mode-tabs'), 'GameLauncher should remove launcher-mode-tabs.');
assert(!launcherSource.includes('launcher-secondary-actions'), 'GameLauncher should remove launcher-secondary-actions.');

const mainMenuBackground = readPublicAssetStats('main-menu-bg.webp');
assert(mainMenuBackground.exists, `Expected launcher background art at ${mainMenuBackground.assetPath}.`);
assert(mainMenuBackground.size > 50 * 1024, `Launcher background art should be larger than 50KB, got ${mainMenuBackground.size} bytes.`);

const mainMenuBackgroundSource = readPublicText('main-menu-bg.source.md');
assert(mainMenuBackgroundSource.text.includes('Pollinations AI API'), 'Launcher background source note should document the generation source.');
assert(mainMenuBackgroundSource.text.includes('model=flux'), 'Launcher background source note should document the model.');
assert(mainMenuBackgroundSource.text.includes('1920x1080'), 'Launcher background source note should document the generated 16:9 dimensions.');
assert(mainMenuBackgroundSource.text.includes('dark library with arcane tomes'), 'Launcher background source note should document the art direction prompt.');
assert(mainMenuBackgroundSource.text.includes('cosmic purple/teal mists'), 'Launcher background source note should document the color/mood prompt.');
assert(mainMenuBackgroundSource.text.includes('No external runtime dependency'), 'Launcher background source note should state the runtime dependency boundary.');
assert(mainMenuBackgroundSource.text.includes('No text, logos, or third-party IP'), 'Launcher background source note should record text/logo/IP safety review.');

const componentsCss = readSource('styles', 'components.css');
assert(componentsCss.includes('.launcher-menu {'), 'components.css should define launcher-menu styles.');
assert(componentsCss.includes('.launcher-art-bg {'), 'components.css should define launcher-art-bg styles.');
assert(componentsCss.includes('.launcher-art-bg img {'), 'components.css should define launcher-art-bg image styles.');
assert(componentsCss.includes('object-fit: cover;'), 'Launcher background image should cover the menu frame responsively.');
assert(componentsCss.includes('object-position: center 30%;'), 'Launcher background image should preserve the intended crop.');
assert(componentsCss.includes('filter: saturate(0.7) brightness(0.5);'), 'Launcher background image should be subdued for readability.');
assert(componentsCss.includes('.launcher-art-bg::after {'), 'Launcher background should include a readability overlay.');
assert(componentsCss.includes('.launcher-menu__item {'), 'components.css should define launcher-menu item styles.');
assert(componentsCss.includes('.launcher-menu__item.is-primary strong {'), 'components.css should highlight the primary launcher-menu item.');
assert(!componentsCss.includes('launcher-primary-action'), 'components.css should remove launcher-primary-action selectors.');
assert(!componentsCss.includes('launcher-mode-tabs'), 'components.css should remove launcher-mode-tabs selectors.');
assert(!componentsCss.includes('launcher-mode-tab'), 'components.css should remove launcher-mode-tab selectors.');
assert(!componentsCss.includes('launcher-secondary-actions'), 'components.css should remove launcher-secondary-actions selectors.');
