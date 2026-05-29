import { lazy, Suspense, useMemo, type CSSProperties, type ComponentType, type LazyExoticComponent, type ReactNode } from 'react';
import './styles.css';
import type { BrowserApiFailure, BrowserApiResult } from './api/contracts';
import { AdvancedDiagnosticsPanel } from './components/AdvancedDiagnostics';
import { ErrorNotice } from './components/ErrorNotice';
import { LoadingCard } from './components/LoadingCard';
import { PlayerStatusSidebar } from './components/PlayerStatusSidebar';
import { ShellProvider, type BrowserShellState, type RouteId, isSuccess, useShell } from './context/ShellContext';
import { formatHeroStatusLabel } from './utils/formatters';

type RouteKind = 'primary' | 'utility';
type RouteIconId = 'book' | 'flame' | 'soul' | 'map' | 'journal' | 'satchel' | 'gallery' | 'settings';
type RouteAvailabilityState = 'active' | 'available' | 'locked' | 'loading' | 'attention';

interface RouteCard {
  id: RouteId;
  kind: RouteKind;
  label: string;
  description: string;
  icon: RouteIconId;
}

interface RouteStateDetails {
  state: RouteAvailabilityState;
  label: string;
}

const HomeRoute = lazy(() => import('./routes/HomeRoute'));
const GameRoute = lazy(() => import('./routes/GameRoute'));
const SoulRoute = lazy(() => import('./routes/SoulRoute'));
const WorldRoute = lazy(() => import('./routes/WorldRoute'));
const JournalRoute = lazy(() => import('./routes/JournalRoute'));
const InventoryRoute = lazy(() => import('./routes/InventoryRoute'));
const MediaRoute = lazy(() => import('./routes/MediaRoute'));
const SettingsRoute = lazy(() => import('./routes/SettingsRoute'));

const playerRoutes: RouteCard[] = [
  { id: 'home', kind: 'primary', label: 'Главная', description: 'Сводка партии, продолжение, загрузка и безопасные действия.', icon: 'book' },
  { id: 'game', kind: 'primary', label: 'Игра', description: 'Текущая сцена, нарратив, ход ГМа и основной художественный ввод.', icon: 'flame' },
  { id: 'soul', kind: 'primary', label: 'Душа', description: 'Персонаж, душа, состояние героя и текущий слой мира.', icon: 'soul' },
  { id: 'world', kind: 'primary', label: 'Мир', description: 'Локация, карта, фракции и игровые действия окружения.', icon: 'map' },
  { id: 'journal', kind: 'primary', label: 'Журнал', description: 'Квесты, хроника, заметки, архив и история текущей главы.', icon: 'journal' },
  { id: 'inventory', kind: 'primary', label: 'Инвентарь', description: 'Предметы, экипировка, ремесло и локальные хранилища.', icon: 'satchel' },
  { id: 'media', kind: 'utility', label: 'Медиа', description: 'Галерея, быстрые сцены и игровые материалы.', icon: 'gallery' },
  { id: 'settings', kind: 'utility', label: 'Настройки', description: 'Локальный профиль, звук, язык и комфорт клиента.', icon: 'settings' }
];

const primaryPlayerRoutes = playerRoutes.filter((route) => route.kind === 'primary');
const utilityPlayerRoutes = playerRoutes.filter((route) => route.kind === 'utility');
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

function RouteGlyph({ icon }: { icon: RouteIconId }) {
  const paths: Record<RouteIconId, ReactNode> = {
    book: <path d="M5 5.5c2.5-1.2 4.8-1.2 7 0v13c-2.2-1.2-4.5-1.2-7 0v-13Zm7 0c2.2-1.2 4.5-1.2 7 0v13c-2.5-1.2-4.8-1.2-7 0m0-13v13" />,
    flame: <path d="M12 21c3.6-1.4 5.7-3.9 5.7-7.1 0-2.5-1.4-4.9-4.2-7.3-.2 2.3-1 3.9-2.3 4.9.1-2.7-.9-5-3-6.9.1 3.1-1.1 5.2-2.4 7.1A6 6 0 0 0 12 21Z" />,
    soul: <path d="M12 3.5c2.1 2.4 3.2 4.8 3.2 7.2a3.2 3.2 0 1 1-6.4 0c0-2.4 1.1-4.8 3.2-7.2Zm0 10.5v6m-3 0h6" />,
    map: <path d="m4.5 6.5 5-2 5 2 5-2v13l-5 2-5-2-5 2v-13Zm5-2v13m5-11v13" />,
    journal: <path d="M6 4.5h9.5A2.5 2.5 0 0 1 18 7v12.5H7.5A2.5 2.5 0 0 1 5 17V6.5a2 2 0 0 1 2-2Zm1 12.5h11M9 8h5m-5 3h6" />,
    satchel: <path d="M8 8V6.8A2.8 2.8 0 0 1 10.8 4h2.4A2.8 2.8 0 0 1 16 6.8V8m-9 0h10.5l1 10H5.5l1-10Zm4.5 4h2" />,
    gallery: <path d="M5 6.5A2.5 2.5 0 0 1 7.5 4h9A2.5 2.5 0 0 1 19 6.5v11A2.5 2.5 0 0 1 16.5 20h-9A2.5 2.5 0 0 1 5 17.5v-11Zm3 9 2.4-2.7 2 2 2.2-3.1L17 15.5M9 8.5h.01" />,
    settings: <path d="M12 8.5a3.5 3.5 0 1 1 0 7 3.5 3.5 0 0 1 0-7Zm0-5v2m0 13v2m8.5-8.5h-2m-13 0h-2m14.5-6.5-1.4 1.4M6.9 17.1l-1.4 1.4m0-13 1.4 1.4m10.2 10.2 1.4 1.4" />
  };

  return (
    <svg className="route-card__glyph" viewBox="0 0 24 24" focusable="false" aria-hidden="true">
      {paths[icon]}
    </svg>
  );
}

function AppShell() {
  const { activeRoute, advancedEnabled, clientSettings, gameScreen, menu, readyState, realmTheme, setActiveRoute, shellState } = useShell();
  const browserShellClassName = [
    'browser-shell',
    clientSettings?.accessibility.reducedMotion ? 'is-reduced-motion' : '',
    clientSettings?.accessibility.contrastFriendly ? 'is-contrast-friendly' : ''
  ].filter(Boolean).join(' ');
  const browserShellStyle = {
    '--realm-accent': realmTheme.accent,
    '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`
  } as CSSProperties;
  const routeStates = useMemo(() => resolveRouteStates(playerRoutes, activeRoute, shellState, readyState), [activeRoute, shellState, readyState]);

  return (
    <main className={browserShellClassName} data-theme-key={realmTheme.key} style={browserShellStyle}>
      <section className="shell-hero" aria-labelledby="browser-client-title">
        <p className="eyebrow">Книга Вечности: Перерождение · локальная книга</p>
        <div className="hero-layout">
          <div>
            <h1 id="browser-client-title">Книга Вечности: Перерождение</h1>
            <p className="lead">
              Откройте книгу, продолжите сохранённую главу или подготовьте новую сцену. Браузер показывает локальную партию мягким игровым языком,
              а служебные сведения остаются в расширенном режиме.
            </p>
          </div>
          <div className="hero-status" aria-label="Текущее царство">
            <span className="theme-icon" aria-hidden="true">{realmTheme.icon}</span>
            <strong>{realmTheme.label}</strong>
            <span>{formatHeroStatusLabel(gameScreen, menu)}</span>
          </div>
        </div>
      </section>

      <nav className="route-grid route-grid--primary" aria-label="Основные игровые разделы браузерного клиента">
        {primaryPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, routeStates, setActiveRoute))}
      </nav>

      <nav className="route-grid route-grid--utility" aria-label="Дополнительные игровые разделы браузерного клиента">
        <p className="utility-route-heading">Сводка / Игра / Душа / Мир / Журнал / Инвентарь — основная цепочка игрока. Медиа и настройки доступны отдельно.</p>
        {utilityPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, routeStates, setActiveRoute))}
      </nav>

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

      <AdvancedDiagnosticsPanel />
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

function resolveRouteStates(
  routes: RouteCard[],
  activeRoute: RouteId,
  shellState: BrowserShellState,
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null
): Record<RouteId, RouteStateDetails> {
  return routes.reduce((states, route) => {
    states[route.id] = resolveRouteState(route.id, activeRoute, shellState, readyState);
    return states;
  }, {} as Record<RouteId, RouteStateDetails>);
}

function resolveRouteState(
  routeId: RouteId,
  activeRoute: RouteId,
  shellState: BrowserShellState,
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null
): RouteStateDetails {
  if (activeRoute === routeId) {
    return { state: 'active', label: 'открыто' };
  }
  if (shellState.status === 'loading') {
    return { state: 'loading', label: 'собираем' };
  }
  if (shellState.status === 'error') {
    return { state: 'attention', label: 'нужна проверка' };
  }
  if (routeHasAttention(routeId, readyState)) {
    return { state: 'attention', label: 'нужна проверка' };
  }
  if (routeNeedsGame(routeId) && !hasGameScreen(readyState)) {
    return { state: 'locked', label: 'ждёт главу' };
  }
  return { state: 'available', label: 'доступно' };
}

function renderRouteButton(route: RouteCard, activeRoute: RouteId, routeStates: Record<RouteId, RouteStateDetails>, setActiveRoute: (route: RouteId) => void): ReactNode {
  const routeState = routeStates[route.id];
  return (
    <button
      key={route.id}
      type="button"
      className={`route-card route-card--${route.id} route-card-state--${routeState.state}${activeRoute === route.id ? ' is-active' : ''}`}
      data-route-state={routeState.state}
      onClick={() => setActiveRoute(route.id)}
      aria-pressed={activeRoute === route.id}
      aria-label={`${route.label}. ${route.description} Состояние: ${routeState.label}`}
    >
      <span className="route-card__icon" aria-hidden="true"><RouteGlyph icon={route.icon} /></span>
      <span className="route-card__body">
        <strong>{route.label}</strong>
        <small>{route.description}</small>
      </span>
      <span className="route-card__state" aria-hidden="true">{routeState.label}</span>
    </button>
  );
}

function routeNeedsGame(routeId: RouteId): boolean {
  return routeId === 'game' || routeId === 'soul' || routeId === 'world' || routeId === 'journal' || routeId === 'inventory' || routeId === 'media';
}

function hasGameScreen(readyState: Extract<BrowserShellState, { status: 'ready' }> | null): boolean {
  return Boolean(readyState && isSuccess(readyState.game));
}

function isNoActiveSessionFailure(result: BrowserApiResult<unknown>): result is BrowserApiFailure {
  return !result.ok && result.kind === 'no-active-session';
}

function routeHasAttention(routeId: RouteId, readyState: Extract<BrowserShellState, { status: 'ready' }> | null): boolean {
  if (!readyState) {
    return false;
  }
  if (routeId === 'home') {
    return !isSuccess(readyState.menu) || !isSuccess(readyState.session);
  }
  if (routeId === 'settings') {
    return !isSuccess(readyState.audio) || !isSuccess(readyState.settings);
  }
  if (!isSuccess(readyState.game)) {
    return !isNoActiveSessionFailure(readyState.game) && routeNeedsGame(routeId);
  }
  return routeId === 'game' && (readyState.game.data.turnState.severity === 'error' || readyState.game.data.turnState.severity === 'repair');
}
