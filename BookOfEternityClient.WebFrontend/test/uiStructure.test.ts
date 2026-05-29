export {};

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { join } = await import(pathSpecifier);
const frontendDir = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, 'src', ...relativePath), 'utf-8');
}

const app = readSource('App.tsx');
if (app.includes('route-grid--primary') || app.includes('route-grid--utility')) {
  throw new Error('App.tsx should not render the old route-grid dashboard layout.');
}
if (!app.includes('NavBar')) {
  throw new Error('App.tsx should render NavBar.');
}

const navBar = readSource('components', 'NavBar.tsx');
if (!navBar.includes('nav-bar')) {
  throw new Error('NavBar.tsx should render the nav-bar component class.');
}

const composer = readSource('components', 'Composer.tsx');
if (!composer.includes('composer-container')) {
  throw new Error('Composer.tsx should render the composer-container class.');
}

const gameRoute = readSource('routes', 'GameRoute.tsx');
if (!gameRoute.includes('Composer')) {
  throw new Error('GameRoute.tsx should render the Composer component.');
}

const worldRoute = readSource('routes', 'WorldRoute.tsx');
if (!worldRoute.includes('action-catalog-toggle')) {
  throw new Error('WorldRoute.tsx should use the action-catalog-toggle container.');
}
if (!worldRoute.includes('showAllActions')) {
  throw new Error('WorldRoute.tsx should keep the action catalog collapsed behind showAllActions state.');
}
