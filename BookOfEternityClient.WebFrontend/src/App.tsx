import { lazy, Suspense, type CSSProperties, type ComponentType, type LazyExoticComponent } from 'react';
import './styles.css';
import { AdvancedDiagnosticsPanel as AdvancedDiagnostics } from './components/AdvancedDiagnostics';
import { ErrorNotice } from './components/ErrorNotice';
import { LoadingCard } from './components/LoadingCard';
import { NavBar } from './components/NavBar';
import { PlayerStatusSidebar } from './components/PlayerStatusSidebar';
import { ShellProvider, type RouteId, useShell } from './context/ShellContext';

const HomeRoute = lazy(() => import('./routes/HomeRoute'));
const GameRoute = lazy(() => import('./routes/GameRoute'));
const SoulRoute = lazy(() => import('./routes/SoulRoute'));
const WorldRoute = lazy(() => import('./routes/WorldRoute'));
const JournalRoute = lazy(() => import('./routes/JournalRoute'));
const InventoryRoute = lazy(() => import('./routes/InventoryRoute'));
const MediaRoute = lazy(() => import('./routes/MediaRoute'));
const SettingsRoute = lazy(() => import('./routes/SettingsRoute'));

const routeComponents = {
  home: HomeRoute,
  game: GameRoute,
  soul: SoulRoute,
  world: WorldRoute,
  journal: JournalRoute,
  inventory: InventoryRoute,
  media: MediaRoute,
  settings: SettingsRoute
} satisfies Record<RouteId, LazyExoticComponent<ComponentType>>;

export default function App() {
  return (
    <ShellProvider>
      <AppShell />
    </ShellProvider>
  );
}

function AppShell() {
  const { advancedEnabled, clientSettings, readyState, realmTheme, shellState } = useShell();
  const browserShellClassName = [
    'browser-shell',
    clientSettings?.accessibility.reducedMotion ? 'is-reduced-motion' : '',
    clientSettings?.accessibility.contrastFriendly ? 'is-contrast-friendly' : ''
  ].filter(Boolean).join(' ');
  const browserShellStyle = {
    '--realm-accent': realmTheme.accent,
    '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`
  } as CSSProperties;

  return (
    <main className={browserShellClassName} data-theme-key={realmTheme.key} style={browserShellStyle}>
      <NavBar />
      <section className="workspace-grid" aria-live="polite">
        <div className="workspace-main">
          {shellState.status === 'loading' && <LoadingCard />}
          {shellState.status === 'error' && <ErrorNotice title="Состояние клиента недоступно" failure={shellState} advancedEnabled={advancedEnabled} />}
          {readyState && <ActiveRoute />}
        </div>
        <aside className="workspace-sidebar" aria-label="Сводка книги">
          <PlayerStatusSidebar />
        </aside>
      </section>
      {advancedEnabled && readyState && <AdvancedDiagnostics />}
    </main>
  );
}

function ActiveRoute() {
  const { activeRoute } = useShell();
  const RouteComponent = routeComponents[activeRoute];
  return (
    <Suspense fallback={<LoadingCard />}>
      <RouteComponent />
    </Suspense>
  );
}
