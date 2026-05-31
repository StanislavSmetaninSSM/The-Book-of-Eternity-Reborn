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
for (const staleSnippet of ['route-grid--primary', 'route-grid--utility', 'workspace-grid', '<Sidebar />', './routes/']) {
  if (appWithoutBlockComments.includes(staleSnippet)) {
    throw new Error(`App.tsx should not render the old route/dashboard shell: ${staleSnippet}`);
  }
}
assertIncludes(app, "import { ConnectionBanner } from './components/ConnectionBanner';", 'App.tsx should import ConnectionBanner.');
assertIncludes(app, "import { TabBar } from './components/TabBar';", 'App.tsx should import TabBar.');
assertIncludes(app, "import { SceneView } from './components/SceneView';", 'App.tsx should import SceneView.');
assertIncludes(app, "import { StatusView } from './components/StatusView';", 'App.tsx should import StatusView.');
assertIncludes(app, "import { HelpView } from './components/HelpView';", 'App.tsx should import HelpView.');
assertIncludes(app, "import { SettingsView } from './components/SettingsView';", 'App.tsx should import SettingsView.');
assertIncludes(app, "import { UnifiedInput } from './components/UnifiedInput';", 'App.tsx should import UnifiedInput.');
assertIncludes(app, '<ConnectionBanner />', 'App.tsx should render ConnectionBanner.');
assertIncludes(app, '<TabBar />', 'App.tsx should render TabBar.');
assertIncludes(app, '<section className="content-area" aria-live="polite">', 'App.tsx should render the current content area.');
assertIncludes(app, '<UnifiedInput />', 'App.tsx should render UnifiedInput as the default command surface.');
assertIncludes(app, "case 'scene': return <SceneView />;", 'TabContent should route scene tab to SceneView.');
assertIncludes(app, "case 'status': return <StatusView />;", 'TabContent should route status tab to StatusView.');
assertIncludes(app, "case 'help': return <HelpView />;", 'TabContent should route help tab to HelpView.');
assertIncludes(app, "case 'settings': return <SettingsView />;", 'TabContent should route settings tab to SettingsView.');

const tabBar = readSource('components', 'TabBar.tsx');
assertIncludes(tabBar, '<nav className="tab-bar" role="tablist" aria-label="Навигация">', 'TabBar should expose tablist navigation.');
assertIncludes(tabBar, 'tabNav.map((tab)', 'TabBar should render the shared tab navigation contract.');
assertIncludes(tabBar, 'aria-selected={activeTab === tab.id}', 'TabBar should expose the active tab to assistive tech.');
assertIncludes(tabBar, 'setActiveTab(tab.id)', 'TabBar should switch tabs directly.');

const sceneView = readSource('components', 'SceneView.tsx');
assertIncludes(sceneView, "import { SceneHero } from './SceneHero';", 'SceneView should import SceneHero.');
assertIncludes(sceneView, "import { CommandResultView } from './CommandResultView';", 'SceneView should import CommandResultView.');
assertIncludes(sceneView, '<SceneHero', 'SceneView should render the scene hero.');
assertIncludes(sceneView, 'className="scene-quick-actions"', 'SceneView should expose player-default quick actions.');
assertIncludes(sceneView, 'isCommandView', 'SceneView should swap to command results after slash commands.');
if (sceneView.includes('ActionPalette') || sceneView.includes('<Composer') || sceneView.includes("from './Composer'")) {
  throw new Error('SceneView should not revive the old action palette or composer components.');
}

const unifiedInput = readSource('components', 'UnifiedInput.tsx');
assertIncludes(unifiedInput, 'className="unified-input"', 'UnifiedInput should render the current input shell.');
assertIncludes(unifiedInput, 'submitComposerText(e.currentTarget.value);', 'UnifiedInput should submit Enter without moving gameplay logic into React.');
assertIncludes(unifiedInput, '<CommandAutocomplete', 'UnifiedInput should keep slash command autocomplete available.');

const settingsView = readSource('components', 'SettingsView.tsx');
assertIncludes(settingsView, 'Расширенный режим', 'SettingsView should keep advanced mode explicit and secondary.');
assertIncludes(settingsView, 'Показывать технические данные', 'SettingsView should keep raw technical details behind advanced mode.');

const commandUiCss = readSource('styles', 'command-ui.css');
for (const selector of ['.tab-bar {', '.content-area {', '.scene-view {', '.status-view {', '.help-view {', '.settings-view {', '.unified-input {']) {
  assertIncludes(commandUiCss, selector, 'command-ui.css should style the current minimalist shell.');
}
