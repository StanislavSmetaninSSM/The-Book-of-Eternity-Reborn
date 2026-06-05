export {};

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { join } = await import(pathSpecifier);
const repoRoot = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = join(repoRoot, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, 'src', ...relativePath), 'utf-8');
}

function assertIncludes(source: string, expected: string, description: string) {
  if (!source.includes(expected)) {
    throw new Error(`${description} Missing snippet: ${expected}`);
  }
}

const connectionBanner = readSource('components', 'ConnectionBanner.tsx');
assertIncludes(connectionBanner, "const { connectionStatus, loadBrowserState } = useShell();", 'ConnectionBanner should read shell connection state and reload action.');
assertIncludes(connectionBanner, "if (connectionStatus === 'connected') {", 'ConnectionBanner should stay hidden when the client is connected.');
assertIncludes(connectionBanner, "const isDisconnected = connectionStatus === 'disconnected';", 'ConnectionBanner should distinguish partial and disconnected states.');
assertIncludes(connectionBanner, "'Книга недоступна. Проверьте, что игра запущена.'", 'ConnectionBanner should show a disconnected warning.');
assertIncludes(connectionBanner, "'Некоторые разделы не загрузились. Часть данных может быть неактуальна.'", 'ConnectionBanner should show a partial-data warning.');
assertIncludes(connectionBanner, "className={`connection-banner ${isDisconnected ? 'is-disconnected' : 'is-partial'}`}", 'ConnectionBanner should expose state-specific classes.');
assertIncludes(connectionBanner, 'role="alert"', 'ConnectionBanner should announce connection issues as alerts.');
assertIncludes(connectionBanner, 'Повторить', 'ConnectionBanner should render a retry button label.');
assertIncludes(connectionBanner, 'onClick={() => void loadBrowserState()}', 'ConnectionBanner retry button should trigger a full reload.');

const app = readSource('App.tsx');
assertIncludes(app, "import { ConnectionBanner } from './components/ConnectionBanner';", 'App should import ConnectionBanner.');
const bannerIndex = app.indexOf('<ConnectionBanner />');
const tabBarIndex = app.indexOf('<TabBar />');
const contentIndex = app.indexOf('<section className="content-area"');
if (bannerIndex === -1 || tabBarIndex === -1 || contentIndex === -1 || bannerIndex > tabBarIndex || tabBarIndex > contentIndex) {
  throw new Error('App should render ConnectionBanner before TabBar and the content area.');
}

const css = readSource('styles', 'components.css');
for (const selector of [
  '.connection-banner {',
  '.connection-banner.is-partial {',
  '.connection-banner.is-disconnected {',
  '.connection-banner button {'
]) {
  assertIncludes(css, selector, 'components.css should style the connection banner.');
}
