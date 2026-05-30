export {};

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { join } = await import(pathSpecifier);
const frontendDir = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, 'src', ...relativePath), 'utf-8');
}

function assertIncludes(source: string, expected: string, description: string) {
  if (!source.includes(expected)) {
    throw new Error(`${description} Missing snippet: ${expected}`);
  }
}

const app = readSource('App.tsx');
const appWithoutBlockComments = app.replace(/\/\*[\s\S]*?\*\//g, '');
if (appWithoutBlockComments.includes('route-grid--primary') || appWithoutBlockComments.includes('route-grid--utility')) {
  throw new Error('App.tsx should not render the old route-grid dashboard layout.');
}
assertIncludes(app, "import { Sidebar } from './components/Sidebar';", 'App.tsx should import Sidebar.');
assertIncludes(app, '<Sidebar />', 'App.tsx should render Sidebar.');

const sidebar = readSource('components', 'Sidebar.tsx');
if (!sidebar.includes('className="sidebar"')) {
  throw new Error('Sidebar.tsx should render the sidebar component class.');
}

const composer = readSource('components', 'Composer.tsx');
if (!composer.includes('composer-container')) {
  throw new Error('Composer.tsx should render the composer-container class.');
}

const homeRoute = readSource('routes', 'HomeRoute.tsx');
assertIncludes(homeRoute, "import { SceneHero } from '../components/SceneHero';", 'HomeRoute should import SceneHero.');
assertIncludes(homeRoute, '<SceneHero', 'HomeRoute should render SceneHero above the launcher.');
assertIncludes(homeRoute, 'eyebrow="Книга Вечности"', 'HomeRoute should use the book eyebrow in SceneHero.');
assertIncludes(homeRoute, 'title="Перерождение"', 'HomeRoute should use the rebirth title in SceneHero.');
assertIncludes(homeRoute, 'subtitle="Бесконечное странствие души через жизни, смерти и перерождения"', 'HomeRoute should use the rebirth subtitle in SceneHero.');
assertIncludes(homeRoute, '<GameLauncher menu={readyState.menu.data} />', 'HomeRoute should keep the launcher after SceneHero.');

const gameRoute = readSource('routes', 'GameRoute.tsx');
if (!gameRoute.includes('Composer')) {
  throw new Error('GameRoute.tsx should render the Composer component.');
}
assertIncludes(gameRoute, "import type { BrowserGameScreenDto } from '../api/contracts';", 'GameRoute should import BrowserGameScreenDto for TurnStateCard typing.');
assertIncludes(gameRoute, 'formatTurnLifecycleActionDescription', 'GameRoute should format recommended turn actions.');
assertIncludes(gameRoute, '<TurnStateCard turnState={game.turnState} advancedEnabled={advancedEnabled} />', 'GameRoute should render the consolidated turn state card.');
assertIncludes(gameRoute, '<h2>{game.theme.icon} Последний нарратив</h2>', 'GameRoute should keep a narrative heading without duplicating the SceneHero title.');
assertIncludes(gameRoute, "const isWaitingForGm = turnState.phase === 'gm-turn' || turnState.phase === 'waiting-for-gm' || turnState.state === 'gm-turn';", 'TurnStateCard should classify GM-waiting states explicitly.');
assertIncludes(gameRoute, "const needsRepair = turnState.severity === 'error' || turnState.severity === 'repair' || turnState.validationState === 'invalid';", 'TurnStateCard should classify repair-needed states explicitly.');
assertIncludes(gameRoute, "const playerActions = turnState.recommendedActions.filter(a => a.surface === 'player-default');", 'TurnStateCard should only show player-default recommended actions.');
assertIncludes(gameRoute, '<details className="turn-state-card__phases">', 'TurnStateCard should hide known phases behind details in advanced mode.');
if (gameRoute.includes('TurnLifecycleActions') || gameRoute.includes('formatQteStateLabel') || gameRoute.includes('turn-status-compact') || gameRoute.includes('Быстрая сцена:')) {
  throw new Error('GameRoute should remove the compact turn lifecycle/QTE presentation in favor of TurnStateCard.');
}

const componentsCss = readSource('styles', 'components.css');
for (const selector of [
  '.turn-state-card {',
  '.turn-state-card--waiting {',
  '.turn-state-card--repair {',
  '.turn-state-card--normal {',
  '.turn-state-card__header {',
  '.turn-state-card__guidance {',
  '.turn-state-card__phases {',
  '.turn-state-card__phases summary {'
]) {
  assertIncludes(componentsCss, selector, 'components.css should style the turn state card variants.');
}

const worldRoute = readSource('routes', 'WorldRoute.tsx');
if (!worldRoute.includes('action-catalog-toggle')) {
  throw new Error('WorldRoute.tsx should use the action-catalog-toggle container.');
}
if (!worldRoute.includes('showAllActions')) {
  throw new Error('WorldRoute.tsx should keep the action catalog collapsed behind showAllActions state.');
}
