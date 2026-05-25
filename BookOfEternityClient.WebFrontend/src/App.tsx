import { useCallback, useEffect, useMemo, useState } from 'react';
import type { CSSProperties, FormEvent, ReactNode } from 'react';
import { browserApi, browserApiContractSummary } from './api/client';
import type {
  BrowserApiFailure,
  BrowserApiResult,
  BrowserGameScreenDto,
  BrowserLifecycleDashboardDto,
  BrowserMainMenuDto,
  LocalWebUiSessionStatus
} from './api/contracts';

type RouteId = 'home' | 'game' | 'soul' | 'world' | 'media' | 'settings';

type BrowserShellState =
  | { status: 'loading' }
  | { status: 'ready'; menu: BrowserApiResult<BrowserMainMenuDto>; session: BrowserApiResult<LocalWebUiSessionStatus>; game: BrowserApiResult<BrowserGameScreenDto>; lifecycle: BrowserApiResult<BrowserLifecycleDashboardDto> }
  | { status: 'error'; playerMessage: string; technicalDetails?: string };

interface RouteCard {
  id: RouteId;
  label: string;
  description: string;
  icon: string;
}

interface RealmTheme {
  key: string;
  label: string;
  icon: string;
  accent: string;
}

const playerRoutes: RouteCard[] = [
  { id: 'home', label: 'Главная', description: 'Сессия, продолжение, загрузка и безопасные действия.', icon: '✦' },
  { id: 'game', label: 'Игра', description: 'Нарратив, ход ГМа, QTE и основной художественный ввод.', icon: '📖' },
  { id: 'soul', label: 'Душа', description: 'Душа, герой, состояние и текущий realm.', icon: '🕯️' },
  { id: 'world', label: 'Мир', description: 'Карта, журнал, квесты, фракции и действия.', icon: '🗺️' },
  { id: 'media', label: 'Медиа', description: 'Галерея, сцены QTE и игровые материалы.', icon: '🎞️' },
  { id: 'settings', label: 'Настройки', description: 'Локальный профиль, звук, язык и комфорт клиента.', icon: '⚙️' }
];

const fallbackTheme: RealmTheme = {
  key: 'mortal-world',
  label: 'Мир смертных',
  icon: '🌘',
  accent: '#d8b36a'
};

const browserApiEndpoints = browserApiContractSummary.endpointDocs;

export default function App() {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });
  const [activeRoute, setActiveRoute] = useState<RouteId>('home');
  const [advancedEnabled, setAdvancedEnabled] = useState(false);
  const [composerText, setComposerText] = useState('');
  const [composerNotice, setComposerNotice] = useState('');

  const loadBrowserState = useCallback(async () => {
    setShellState({ status: 'loading' });

    try {
      const [menu, session, game, lifecycle] = await Promise.all([
        browserApi.getMainMenu(),
        browserApi.getSessionStatus(),
        browserApi.getGameScreen(),
        browserApi.getLifecycleDashboard()
      ]);

      setShellState({ status: 'ready', menu, session, game, lifecycle });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Unknown browser shell error.';
      setShellState({
        status: 'error',
        playerMessage: 'Браузерный клиент не смог собрать состояние игры.',
        technicalDetails: message
      });
    }
  }, []);

  useEffect(() => {
    void loadBrowserState();
  }, [loadBrowserState]);

  const readyState = shellState.status === 'ready' ? shellState : null;
  const gameScreen = readyState && isSuccess(readyState.game) ? readyState.game.data : null;
  const menu = readyState && isSuccess(readyState.menu) ? readyState.menu.data : null;
  const session = readyState && isSuccess(readyState.session) ? readyState.session.data : null;
  const lifecycle = readyState && isSuccess(readyState.lifecycle) ? readyState.lifecycle.data : null;
  const realmTheme = useMemo(() => resolveRealmTheme(gameScreen), [gameScreen]);

  function submitComposer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = composerText.trim();

    if (normalized.startsWith('/')) {
      setComposerNotice('Slash-команды не выполняются из основного поля. Включите «Расширенный режим», чтобы перенести команду в техническую панель и подтвердить её отдельно.');
      setAdvancedEnabled(true);
      return;
    }

    setComposerNotice('Художественный ввод подготовлен. Запись хода будет подключена отдельной lifecycle-задачей, чтобы не обходить C# local-write coordinator.');
  }

  return (
    <main className="browser-shell" style={{ '--realm-accent': realmTheme.accent } as CSSProperties}>
      <section className="shell-hero" aria-labelledby="browser-client-title">
        <p className="eyebrow">Book of Eternity Reborn · Browser Client</p>
        <div className="hero-layout">
          <div>
            <h1 id="browser-client-title">Локальный игровой клиент</h1>
            <p className="lead">
              C# API остаётся источником истины: React показывает игру, маршруты, запросы и состояние интерфейса,
              но не переносит правила, сохранения или afterlife-контракты в TypeScript.
            </p>
          </div>
          <div className="hero-status" aria-label="Текущий realm">
            <span className="theme-icon" aria-hidden="true">{realmTheme.icon}</span>
            <strong>{realmTheme.label}</strong>
            <span>{gameScreen?.turnState.title ?? menu?.session.validationLabel ?? 'Загрузка состояния'}</span>
          </div>
        </div>
      </section>

      <nav className="route-grid" aria-label="Игровые разделы браузерного клиента">
        {playerRoutes.map((route) => (
          <button
            key={route.id}
            type="button"
            className={activeRoute === route.id ? 'route-card is-active' : 'route-card'}
            onClick={() => setActiveRoute(route.id)}
            aria-pressed={activeRoute === route.id}
          >
            <span aria-hidden="true">{route.icon}</span>
            <strong>{route.label}</strong>
            <small>{route.description}</small>
          </button>
        ))}
      </nav>

      <section className="workspace-grid" aria-live="polite">
        <div className="workspace-main">
          {shellState.status === 'loading' && <LoadingCard />}
          {shellState.status === 'error' && (
            <ErrorNotice title="Состояние клиента недоступно" failure={shellState} advancedEnabled={advancedEnabled} />
          )}
          {readyState && renderActiveRoute(activeRoute, readyState, composerText, setComposerText, composerNotice, submitComposer)}
        </div>

        <aside className="workspace-sidebar" aria-label="Сводка состояния">
          <ShellPanel title="Сессия" eyebrow="локальный runtime">
            {session ? (
              <dl className="kv-list">
                <div><dt>Статус</dt><dd>{session.status}</dd></div>
                <div><dt>Локально</dt><dd>{session.localOnly ? 'Да' : 'Нет'}</dd></div>
                <div><dt>Запись хода</dt><dd>{session.canStartBrowserWrite ? 'Можно' : 'Заблокирована'}</dd></div>
                <div><dt>Путь</dt><dd>{session.gameSessionExists ? 'game_session найден' : 'game_session не найден'}</dd></div>
              </dl>
            ) : readyState ? (
              <ApiFailure title="Сессия недоступна" result={readyState.session} advancedEnabled={advancedEnabled} />
            ) : (
              <p className="muted">Ждём C# host…</p>
            )}
          </ShellPanel>

          <ShellPanel title="Ход и ремонт" eyebrow="GM lifecycle">
            {gameScreen ? (
              <>
                <p className="status-pill">{gameScreen.turnState.title}</p>
                <p>{gameScreen.turnState.message}</p>
                <p className="muted">Валидация: {gameScreen.turnState.validationLabel}</p>
              </>
            ) : readyState ? (
              <ApiFailure title="Игровой экран недоступен" result={readyState.game} advancedEnabled={advancedEnabled} />
            ) : (
              <p className="muted">Загрузка…</p>
            )}
          </ShellPanel>

          <button
            type="button"
            className="advanced-toggle"
            aria-controls="advanced-diagnostics"
            aria-expanded={advancedEnabled}
            onClick={() => setAdvancedEnabled((value) => !value)}
          >
            {advancedEnabled ? 'Скрыть расширенный режим' : 'Расширенный режим'}
          </button>
        </aside>
      </section>

      {advancedEnabled && readyState && <AdvancedDiagnosticsPanel state={readyState} lifecycle={lifecycle} />}
    </main>
  );
}

function renderActiveRoute(
  activeRoute: RouteId,
  state: Extract<BrowserShellState, { status: 'ready' }>,
  composerText: string,
  setComposerText: (value: string) => void,
  composerNotice: string,
  submitComposer: (event: FormEvent<HTMLFormElement>) => void
) {
  switch (activeRoute) {
    case 'home':
      return <HomeRoute state={state} />;
    case 'game':
      return <GameRoute state={state} composerText={composerText} setComposerText={setComposerText} composerNotice={composerNotice} submitComposer={submitComposer} />;
    case 'soul':
      return <SoulRoute state={state} />;
    case 'world':
      return <WorldRoute state={state} />;
    case 'media':
      return <MediaRoute state={state} />;
    case 'settings':
      return <SettingsRoute state={state} />;
  }
}

function HomeRoute({ state }: { state: Extract<BrowserShellState, { status: 'ready' }> }) {
  if (!isSuccess(state.menu)) {
    return <ApiFailure title="Главное меню недоступно" result={state.menu} advancedEnabled={false} />;
  }

  const menu = state.menu.data;

  return (
    <ShellPanel title="Главная" eyebrow="игровое меню">
      <div className="summary-card">
        <h2>{menu.session.soulName || 'Новая душа'}</h2>
        <p>{menu.session.realmLabel} · {menu.session.turnLabel}</p>
        <p>{menu.session.continueReason}</p>
      </div>
      <div className="action-grid">
        {menu.actions.map((action) => (
          <button key={action.id} type="button" disabled={!action.enabled} className="game-action">
            <strong>{action.label}</strong>
            <span>{action.enabled ? action.description : action.disabledReason}</span>
          </button>
        ))}
      </div>
    </ShellPanel>
  );
}

function GameRoute({
  state,
  composerText,
  setComposerText,
  composerNotice,
  submitComposer
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  composerText: string;
  setComposerText: (value: string) => void;
  composerNotice: string;
  submitComposer: (event: FormEvent<HTMLFormElement>) => void;
}) {
  if (!isSuccess(state.game)) {
    return <ApiFailure title="Игровой экран недоступен" result={state.game} advancedEnabled={false} />;
  }

  const game = state.game.data;

  return (
    <ShellPanel title="Игра" eyebrow="нарратив и ход">
      <article className="narrative-card">
        <h2>{game.theme.icon} {game.theme.label}</h2>
        <p>{game.narrative.text || 'C# runtime пока не вернул последний нарратив.'}</p>
      </article>

      <div className="split-grid">
        <ShellPanel title="Состояние хода" eyebrow={game.turnState.state} nested>
          <p className="status-pill">{game.turnState.title}</p>
          <p>{game.turnState.message}</p>
          <p className="muted">QTE: {game.qte.notification ?? game.qte.state}</p>
        </ShellPanel>
        <ShellPanel title="Варианты" eyebrow="player-facing" nested>
          {game.narrative.dialogueOptions.length > 0 ? (
            <ul className="choice-list">
              {game.narrative.dialogueOptions.map((option) => (
                <li key={option.id}><strong>{option.text}</strong><span>{option.category}</span></li>
              ))}
            </ul>
          ) : (
            <p className="muted">Варианты появятся здесь после ответа ГМа.</p>
          )}
        </ShellPanel>
      </div>

      <form className="composer" onSubmit={submitComposer}>
        <label htmlFor="player-action">Основной художественный ввод</label>
        <textarea
          id="player-action"
          name="player-action"
          rows={4}
          value={composerText}
          onChange={(event) => setComposerText(event.currentTarget.value)}
          placeholder={game.actionComposer.placeholder || 'Опишите действие персонажа обычным текстом…'}
          disabled={!game.actionComposer.canSubmit}
        />
        <p className="muted">{game.actionComposer.guidance}</p>
        {!game.actionComposer.canSubmit && <p className="warning-text">{game.actionComposer.disabledReason}</p>}
        <button type="submit" disabled={!composerText.trim()}>Подготовить действие</button>
        {composerNotice && <p className="composer-notice">{composerNotice}</p>}
      </form>
    </ShellPanel>
  );
}

function SoulRoute({ state }: { state: Extract<BrowserShellState, { status: 'ready' }> }) {
  if (!isSuccess(state.game)) {
    return <ApiFailure title="Данные души недоступны" result={state.game} advancedEnabled={false} />;
  }

  const { soul, player } = state.game.data;

  return (
    <ShellPanel title="Душа" eyebrow="персонаж и состояние">
      <div className="split-grid">
        <div className="summary-card">
          <h2>{soul.name || 'Безымянная душа'}</h2>
          <p>{soul.realm} · инкарнация {soul.incarnation}</p>
          <p>Чернильные перья: {soul.inkFeathers}</p>
          <p>Просветление: {soul.enlightenmentTier || 'нет данных'}</p>
          <p>Хранитель: {soul.activeGuardianName || 'не назначен'}</p>
        </div>
        <div className="summary-card">
          <h2>{player.name || 'Герой'}</h2>
          <p>{player.race} · {player.class}</p>
          <p>{player.currentCondition}</p>
          <StatusBar label="Здоровье" value={player.healthPercentage} />
          <StatusBar label="Энергия" value={player.energyPercentage} />
          <StatusBar label="Стойкость" value={player.poisePercentage} />
        </div>
      </div>
    </ShellPanel>
  );
}

function WorldRoute({ state }: { state: Extract<BrowserShellState, { status: 'ready' }> }) {
  if (!isSuccess(state.game)) {
    return <ApiFailure title="Мир недоступен" result={state.game} advancedEnabled={false} />;
  }

  const game = state.game.data;

  return (
    <ShellPanel title="Мир" eyebrow="карта, журнал и действия">
      <div className="split-grid three">
        <div className="summary-card"><h2>Локация</h2><p>{game.world.location}</p><p>{game.world.worldTime}</p></div>
        <div className="summary-card"><h2>Журнал</h2><p>Квесты, архив и история будут разворачиваться в этом регионе без slash-команд.</p></div>
        <div className="summary-card"><h2>Фракции</h2><p>Панели фракций/стражей используют C# DTO и не копируют правила в React.</p></div>
      </div>
    </ShellPanel>
  );
}

function MediaRoute({ state }: { state: Extract<BrowserShellState, { status: 'ready' }> }) {
  if (!isSuccess(state.game)) {
    return <ApiFailure title="Медиа недоступны" result={state.game} advancedEnabled={false} />;
  }

  const qte = state.game.data.qte;

  return (
    <ShellPanel title="Медиа" eyebrow="галерея и QTE">
      <div className="split-grid">
        <div className="summary-card"><h2>QTE</h2><p>{qte.notification ?? qte.error ?? qte.state}</p></div>
        <div className="summary-card"><h2>Галерея</h2><p>Изображения и кинематик-сцены будут подключаться через безопасные C# media endpoints.</p></div>
      </div>
    </ShellPanel>
  );
}

function SettingsRoute({ state }: { state: Extract<BrowserShellState, { status: 'ready' }> }) {
  if (!isSuccess(state.menu)) {
    return <ApiFailure title="Настройки недоступны" result={state.menu} advancedEnabled={false} />;
  }

  const options = state.menu.data.options;

  return (
    <ShellPanel title="Настройки" eyebrow="локальность клиента">
      <dl className="kv-list">
        <div><dt>Музыка</dt><dd>{options.musicEnabled ? 'Включена' : 'Выключена'}</dd></div>
        <div><dt>Звук</dt><dd>{options.soundEnabled ? 'Включён' : 'Выключен'}</dd></div>
        <div><dt>Размер шрифта</dt><dd>{options.consoleFontSize}</dd></div>
      </dl>
      <p>{options.guidance}</p>
    </ShellPanel>
  );
}

function AdvancedDiagnosticsPanel({
  state,
  lifecycle
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  lifecycle: BrowserLifecycleDashboardDto | null;
}) {
  return (
    <section className="advanced-diagnostics" id="advanced-diagnostics" aria-label="Расширенный режим">
      <div>
        <p className="eyebrow">Технический режим</p>
        <h2>Расширенный режим</h2>
        <p>Здесь остаются command/API diagnostics, lifecycle validation и сведения для ремонта. Обычный игрок не обязан видеть эти детали.</p>
      </div>
      <div className="split-grid three">
        <ApiResultCard title={getEndpointLabel('BrowserMainMenuDto')} result={state.menu} />
        <ApiResultCard title={getEndpointLabel('LocalWebUiSessionStatus')} result={state.session} />
        <ApiResultCard title={getEndpointLabel('BrowserGameScreenDto')} result={state.game} />
      </div>
      <ShellPanel title="Typed API contract" eyebrow={browserApiContractSummary.strategy} nested>
        <ul className="endpoint-list">
          {browserApiEndpoints.map((apiEndpoint) => (
            <li key={apiEndpoint.path}>
              <strong>{apiEndpoint.path}</strong>
              <span>{apiEndpoint.method} · {apiEndpoint.response} · {apiEndpoint.playerSurface}</span>
            </li>
          ))}
        </ul>
      </ShellPanel>
      {lifecycle && (
        <ShellPanel title="Панель состояния" eyebrow="validation" nested>
          <p>Статус: {lifecycle.validation.statusLabel}</p>
          <p>Ошибки: {lifecycle.validation.errorCount}; предупреждения: {lifecycle.validation.warningCount}</p>
        </ShellPanel>
      )}
    </section>
  );
}

function ApiResultCard<T>({ title, result }: { title: string; result: BrowserApiResult<T> }) {
  return (
    <div className="summary-card">
      <h3>{title}</h3>
      <p>{isSuccess(result) ? 'Данные получены' : result.playerMessage}</p>
      {!isSuccess(result) && result.technicalDetails && <details><summary>Подробности</summary><pre>{result.technicalDetails}</pre></details>}
    </div>
  );
}

function getEndpointLabel(responseType: string): string {
  return browserApiEndpoints.find((apiEndpoint) => apiEndpoint.response === responseType)?.path ?? responseType;
}

function ApiFailure<T>({ title, result, advancedEnabled }: { title: string; result: BrowserApiResult<T>; advancedEnabled: boolean }) {
  if (isSuccess(result)) {
    return null;
  }

  return <ErrorNotice title={title} failure={result} advancedEnabled={advancedEnabled} />;
}

function ErrorNotice({ title, failure, advancedEnabled }: { title: string; failure: BrowserApiFailure | { playerMessage: string; technicalDetails?: string }; advancedEnabled: boolean }) {
  return (
    <section className="error-notice" role="alert">
      <h2>{title}</h2>
      <p>{failure.playerMessage}</p>
      {failure.technicalDetails && (
        <details open={advancedEnabled}>
          <summary>Подробности</summary>
          <pre>{failure.technicalDetails}</pre>
        </details>
      )}
    </section>
  );
}

function LoadingCard() {
  return (
    <ShellPanel title="Загрузка" eyebrow="локальный host">
      <p>Запрашиваем главное меню, сессию, игровой экран и lifecycle dashboard через typed BrowserApiClient…</p>
    </ShellPanel>
  );
}

function ShellPanel({ title, eyebrow, children, nested = false }: { title: string; eyebrow: string; children: ReactNode; nested?: boolean }) {
  return (
    <section className={nested ? 'shell-panel is-nested' : 'shell-panel'}>
      <p className="panel-eyebrow">{eyebrow}</p>
      <h2>{title}</h2>
      {children}
    </section>
  );
}

function StatusBar({ label, value }: { label: string; value: string }) {
  const numericValue = Number.parseFloat(value);
  const percent = Number.isFinite(numericValue) ? Math.max(0, Math.min(100, numericValue)) : 0;

  return (
    <div className="status-bar">
      <span>{label}</span>
      <div aria-hidden="true"><i style={{ width: `${percent}%` }} /></div>
      <strong>{value || '0%'}</strong>
    </div>
  );
}

function resolveRealmTheme(gameScreen: BrowserGameScreenDto | null): RealmTheme {
  if (!gameScreen) {
    return fallbackTheme;
  }

  return {
    key: gameScreen.theme.key,
    label: gameScreen.theme.label,
    icon: gameScreen.theme.icon,
    accent: gameScreen.theme.accent || fallbackTheme.accent
  };
}

function isSuccess<T>(result: BrowserApiResult<T>): result is Extract<BrowserApiResult<T>, { ok: true }> {
  return result.ok;
}
