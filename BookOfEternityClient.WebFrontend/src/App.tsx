import { type CSSProperties } from 'react';
import './styles.css';
import { ConnectionBanner } from './components/ConnectionBanner';
import { ErrorNotice } from './components/ErrorNotice';
import { LoadingCard } from './components/LoadingCard';
import { TabBar } from './components/TabBar';
import { SceneView } from './components/SceneView';
import { QtePracticeView } from './components/QtePracticeView';
import { StatusView } from './components/StatusView';
import { HelpView } from './components/HelpView';
import { SettingsView } from './components/SettingsView';
import { UnifiedInput } from './components/UnifiedInput';
import { GameLauncher } from './components/GameLauncher';
import { ShellProvider, useShell, type TabId } from './context/ShellContext';

export default function App() {
  return (
    <ShellProvider>
      <AppShell />
    </ShellProvider>
  );
}

function AppShell() {
  const { activeRoute, advancedEnabled, clientSettings, menu, readyState, realmTheme, shellState, activeTab } = useShell();
  const isLauncherRoute = activeRoute === 'home' && menu !== null;
  const isPracticeRoute = activeRoute === 'practice';
  const browserShellClassName = [
    'browser-shell',
    isLauncherRoute ? 'is-launcher-route' : '',
    clientSettings?.accessibility.reducedMotion ? 'is-reduced-motion' : '',
    clientSettings?.accessibility.contrastFriendly ? 'is-contrast-friendly' : ''
  ].filter(Boolean).join(' ');
  const browserShellStyle = {
    '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`,
    '--browser-ui-scale': `${(clientSettings?.accessibility.uiScalePercent ?? 100) / 100}`
  } as CSSProperties;

  return (
    <main className={browserShellClassName} data-theme-key={realmTheme.key} style={browserShellStyle}>
      <ConnectionBanner />
      {!isLauncherRoute && <TabBar />}
      <section className={`content-area${isLauncherRoute ? ' content-area--launcher' : ''}`} aria-live="polite">
        {shellState.status === 'loading' && <LoadingCard />}
        {shellState.status === 'error' && <ErrorNotice title="Книга сейчас недоступна" failure={shellState} advancedEnabled={advancedEnabled} />}
        {readyState && (isLauncherRoute ? <GameLauncher menu={menu} /> : <TabContent activeTab={activeTab} />)}
      </section>
      {!isLauncherRoute && !isPracticeRoute && <UnifiedInput />}
    </main>
  );
}

function TabContent({ activeTab }: { activeTab: TabId }) {
  switch (activeTab) {
    case 'scene': return <SceneView />;
    case 'practice': return <QtePracticeView />;
    case 'status': return <StatusView />;
    case 'help': return <HelpView />;
    case 'settings': return <SettingsView />;
  }
}
