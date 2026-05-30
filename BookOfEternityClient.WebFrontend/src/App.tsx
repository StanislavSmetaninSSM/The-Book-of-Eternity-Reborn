import { type CSSProperties } from 'react';
import './styles.css';
import { ConnectionBanner } from './components/ConnectionBanner';
import { ErrorNotice } from './components/ErrorNotice';
import { LoadingCard } from './components/LoadingCard';
import { TabBar } from './components/TabBar';
import { SceneView } from './components/SceneView';
import { StatusView } from './components/StatusView';
import { HelpView } from './components/HelpView';
import { SettingsView } from './components/SettingsView';
import { UnifiedInput } from './components/UnifiedInput';
import { ShellProvider, useShell, type TabId } from './context/ShellContext';

export default function App() {
  return (
    <ShellProvider>
      <AppShell />
    </ShellProvider>
  );
}

function AppShell() {
  const { advancedEnabled, clientSettings, readyState, realmTheme, shellState, activeTab } = useShell();
  const browserShellClassName = [
    'browser-shell',
    clientSettings?.accessibility.reducedMotion ? 'is-reduced-motion' : '',
    clientSettings?.accessibility.contrastFriendly ? 'is-contrast-friendly' : ''
  ].filter(Boolean).join(' ');
  const browserShellStyle = {
    '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`
  } as CSSProperties;

  return (
    <main className={browserShellClassName} data-theme-key={realmTheme.key} style={browserShellStyle}>
      <ConnectionBanner />
      <TabBar />
      <section className="content-area" aria-live="polite">
        {shellState.status === 'loading' && <LoadingCard />}
        {shellState.status === 'error' && <ErrorNotice title="Состояние клиента недоступно" failure={shellState} advancedEnabled={advancedEnabled} />}
        {readyState && <TabContent activeTab={activeTab} />}
      </section>
      <UnifiedInput />
    </main>
  );
}

function TabContent({ activeTab }: { activeTab: TabId }) {
  switch (activeTab) {
    case 'scene': return <SceneView />;
    case 'status': return <StatusView />;
    case 'help': return <HelpView />;
    case 'settings': return <SettingsView />;
  }
}
