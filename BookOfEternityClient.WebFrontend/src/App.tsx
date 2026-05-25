import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties, FormEvent, ReactNode } from 'react';
import { browserApi, browserApiContractSummary } from './api/client';
import type {
  BrowserApiFailure,
  BrowserApiResult,
  BrowserAudioAssetDto,
  BrowserAudioPlaylistDto,
  BrowserAudioSettingsDto,
  BrowserAudioSettingsUpdateRequest,
  BrowserGameScreenDto,
  BrowserLifecycleDashboardDto,
  BrowserMainMenuDto,
  BrowserPlayerCommandActionDto,
  BrowserPlayerCommandMenuDto,
  BrowserPlayerCommandSectionDto,
  ExplorerCommandResult,
  JsonValue,
  LocalWebUiSessionStatus,
  UiBlock,
  UiPrompt
} from './api/contracts';

type RouteId = 'home' | 'game' | 'soul' | 'world' | 'media' | 'settings';

type BrowserShellState =
  | { status: 'loading' }
  | { status: 'ready'; menu: BrowserApiResult<BrowserMainMenuDto>; session: BrowserApiResult<LocalWebUiSessionStatus>; game: BrowserApiResult<BrowserGameScreenDto>; audio: BrowserApiResult<BrowserAudioSettingsDto>; lifecycle: BrowserApiResult<BrowserLifecycleDashboardDto> | null }
  | { status: 'error'; playerMessage: string; technicalDetails?: string };

type PromptAnswers = Record<string, JsonValue | undefined>;

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
  { id: 'game', label: 'Игра', description: 'Нарратив, ход ГМа, быстрые сцены и основной художественный ввод.', icon: '📖' },
  { id: 'soul', label: 'Душа', description: 'Душа, герой, состояние и текущий слой мира.', icon: '🕯️' },
  { id: 'world', label: 'Мир', description: 'Карта, журнал, квесты, фракции и действия.', icon: '🗺️' },
  { id: 'media', label: 'Медиа', description: 'Галерея, быстрые сцены и игровые материалы.', icon: '🎞️' },
  { id: 'settings', label: 'Настройки', description: 'Локальный профиль, звук, язык и комфорт клиента.', icon: '⚙️' }
];

const fallbackTheme: RealmTheme = {
  key: 'mortal-world',
  label: 'Мир смертных',
  icon: '🌘',
  accent: '#d8b36a'
};

const browserApiEndpoints = browserApiContractSummary.endpointDocs;

const playerCopyReplacements: Array<[RegExp, string]> = [
  [/\bMortal World\b/gi, 'Мир смертных'],
  [/\bChaos Sea\b/gi, 'Море Хаоса'],
  [/\bShining Abode\b/gi, 'Сияющая Обитель'],
  [/\bGM[- ]?turn\b/g, 'ход ГМа'],
  [/\bGM\b/g, 'ГМ'],
  [/QTE action resolved\.?/gi, 'Быстрая сцена завершена.'],
  [/\bQTE\b/g, 'быстрая сцена'],
  [/Slash-команды/gi, 'служебные команды'],
  [/\bslash commands?\b/gi, 'служебные команды'],
  [/Нужен repair pending turn/gi, 'Нужна починка ожидающего хода'],
  [/repair pending turn/gi, 'починка ожидающего хода'],
  [/нужен repair/gi, 'нужна починка'],
  [/\bpending[- ]turn\b/gi, 'ожидающий ход'],
  [/\bturn[- ]writer\b/gi, 'запись хода'],
  [/\bBrowser[- ]write\b/gi, 'запись из браузера'],
  [/\bbrowser write\b/gi, 'запись из браузера'],
  [/\blocal[- ]write\b/gi, 'локальная запись'],
  [/\bprompt[- ]session\b/gi, 'игровая форма'],
  [/\brollback\b/gi, 'откат'],
  [/blocked by/gi, 'заблокировано из-за'],
  [/\bblocked\b/gi, 'заблокировано'],
  [/\bby\b/gi, 'из-за'],
  [/\bSpectre\.Console\b/g, 'консольный интерфейс'],
  [/state\/contract/gi, 'файлы состояния и контракта'],
  [/snapshot artifact/gi, 'снимок состояния'],
  [/game_session/gi, 'сохранение игры'],
  [/write-flow/gi, 'запись хода'],
  [/manual_saves/gi, 'ручные сохранения'],
  [/autosaves/gi, 'автосохранения'],
  [/--web/g, 'браузерный режим'],
  [/\boffer\b/gi, 'предложение'],
  [/\bsnapshot\b/gi, 'снимок'],
  [/\bartifact\b/gi, 'файл состояния'],
  [/Browser Client/gi, 'браузерный клиент'],
  [/sound-notification/gi, 'звуковая подсказка'],
  [/\brealm\b/gi, 'царство'],
  [/repair\/validation/gi, 'починка и проверка'],
  [/UI-блокировка/gi, 'блокировка интерфейса'],
  [/\bvalidation\b/gi, 'проверка'],
  [/game_state\/meta\/soul_state\.json/gi, 'файл души'],
  [/soul_state\.json/gi, 'файл души'],
  [/game_state/gi, 'папка состояния игры'],
  [/локальный запись хода/gi, 'локальную запись хода'],
  [/тот же локальную/gi, 'ту же локальную'],
  [/\bUI\b/g, 'интерфейс'],
  [/\baction\b/gi, 'действие'],
  [/\bresolved\b/gi, 'завершена'],
  [/\brepair\b/gi, 'починка'],
  [/\blifecycle\b/gi, 'состояние хода'],
  [/\bruntime\b/gi, 'игровой слой'],
  [/\bendpoint(s)?\b/gi, 'разделы локального интерфейса'],
  [/\bAPI\b/g, 'локальный интерфейс'],
  [/\bDTO\b/g, 'данные интерфейса'],
  [/\bNPC\b/g, 'персонажи мира']
];

export default function App() {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });
  const [activeRoute, setActiveRoute] = useState<RouteId>('home');
  const [advancedEnabled, setAdvancedEnabled] = useState(false);
  const [composerText, setComposerText] = useState('');
  const [composerNotice, setComposerNotice] = useState('');

  const loadBrowserState = useCallback(async () => {
    setShellState({ status: 'loading' });

    try {
      const [menu, session, game, audio] = await Promise.all([
        browserApi.getMainMenu(),
        browserApi.getSessionStatus(),
        browserApi.getGameScreen(),
        browserApi.getAudioSettings()
      ]);
      const lifecycle = advancedEnabled ? await browserApi.getLifecycleDashboard() : null;

      setShellState({ status: 'ready', menu, session, game, audio, lifecycle });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Unknown browser shell error.';
      setShellState({
        status: 'error',
        playerMessage: 'Браузерный клиент не смог собрать состояние игры.',
        technicalDetails: message
      });
    }
  }, [advancedEnabled]);

  useEffect(() => {
    void loadBrowserState();
  }, [loadBrowserState]);

  const readyState = shellState.status === 'ready' ? shellState : null;
  const gameScreen = readyState && isSuccess(readyState.game) ? readyState.game.data : null;
  const menu = readyState && isSuccess(readyState.menu) ? readyState.menu.data : null;
  const session = readyState && isSuccess(readyState.session) ? readyState.session.data : null;
  const lifecycle = readyState && readyState.lifecycle && isSuccess(readyState.lifecycle) ? readyState.lifecycle.data : null;
  const realmTheme = useMemo(() => resolveRealmTheme(gameScreen), [gameScreen]);

  function submitComposer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = composerText.trim();

    if (normalized.startsWith('/')) {
      setComposerNotice('Служебные команды не выполняются из основного поля. Откройте «Расширенный режим» отдельной кнопкой, если хотите перенести команду в техническую панель и подтвердить её там.');
      return;
    }

    setComposerNotice('Художественный ввод подготовлен. Запись хода будет подключена отдельной задачей безопасной локальной записи.');
  }

  return (
    <main className="browser-shell" data-theme-key={realmTheme.key} style={{ '--realm-accent': realmTheme.accent } as CSSProperties}>
      <section className="shell-hero" aria-labelledby="browser-client-title">
        <p className="eyebrow">Книга Вечности: Перерождение · локальный клиент</p>
        <div className="hero-layout">
          <div>
            <h1 id="browser-client-title">Локальный игровой клиент</h1>
            <p className="lead">
              Локальный клиент остаётся источником истины: браузер показывает игру, маршруты и состояние интерфейса,
              но не переносит правила, сохранения или посмертные контракты в отдельный слой.
            </p>
          </div>
          <div className="hero-status" aria-label="Текущий слой мира">
            <span className="theme-icon" aria-hidden="true">{realmTheme.icon}</span>
            <strong>{realmTheme.label}</strong>
            <span>{gameScreen ? formatTurnStateTitle(gameScreen.turnState) : menu?.session.validationLabel ?? 'Загрузка состояния'}</span>
          </div>
        </div>
      </section>

      <nav className="route-grid" aria-label="Игровые разделы браузерного клиента">
        {playerRoutes.map((route) => (
          <button
            key={route.id}
            type="button"
            className={`route-card route-card--${route.id}${activeRoute === route.id ? ' is-active' : ''}`}
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
          <ShellPanel title="Сессия" eyebrow="локальная книга">
            {session ? (
              <dl className="kv-list">
                <div><dt>Статус</dt><dd>{formatSessionStatus(session.status)}</dd></div>
                <div><dt>Локально</dt><dd>{session.localOnly ? 'Да' : 'Нет'}</dd></div>
                <div><dt>Запись хода</dt><dd>{session.canStartBrowserWrite ? 'Можно' : 'Заблокирована'}</dd></div>
                <div><dt>Книга</dt><dd>{session.gameSessionExists ? 'сохранение найдено' : 'сохранение не найдено'}</dd></div>
              </dl>
            ) : readyState ? (
              <ApiFailure title="Сессия недоступна" result={readyState.session} advancedEnabled={advancedEnabled} />
            ) : (
              <p className="muted">Ждём локальный клиент…</p>
            )}
          </ShellPanel>

          <ShellPanel title="Ход и ремонт" eyebrow="безопасность хода">
            {gameScreen ? (
              <>
                <p className="status-pill">{formatTurnStateTitle(gameScreen.turnState)}</p>
                <p>{formatTurnStateMessage(gameScreen.turnState)}</p>
                <p className="muted">Проверка: {toPlayerFacingText(gameScreen.turnState.validationLabel, 'состояние проверяется')}</p>
              </>
            ) : readyState ? (
              <ApiFailure title="Игровой экран недоступен" result={readyState.game} advancedEnabled={advancedEnabled} />
            ) : (
              <p className="muted">Загрузка…</p>
            )}
          </ShellPanel>

          {readyState && <AudioSettingsPanel result={readyState.audio} activeRoute={activeRoute} />}

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
        <p>{toPlayerFacingText(menu.session.realmLabel, 'царство уточняется')} · {toPlayerFacingText(menu.session.turnLabel, 'ход уточняется')}</p>
        <p>{toPlayerFacingText(menu.session.continueReason, 'Продолжение будет доступно, когда локальная книга будет готова.')}</p>
      </div>
      <div className="action-grid">
        {menu.actions.map((action) => (
          <button key={action.id} type="button" disabled={!action.enabled} className="game-action">
            <strong>{toPlayerFacingText(action.label, 'Игровое действие')}</strong>
            <span>{action.enabled ? toPlayerFacingText(action.description, 'Действие доступно.') : toPlayerFacingText(action.disabledReason, 'Действие сейчас недоступно.')}</span>
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
      <article className="narrative-card is-featured">
        <h2>{game.theme.icon} {game.theme.label}</h2>
        <p>{game.narrative.text || 'Последний нарратив пока не найден в локальной книге.'}</p>
      </article>

      <div className="split-grid">
        <ShellPanel title="Состояние хода" eyebrow={formatTurnStateLabel(game.turnState.phase || game.turnState.state)} nested variant="turn">
          <p className={`status-pill turn-phase turn-phase--${game.turnState.severity}`}>{formatTurnStateTitle(game.turnState)}</p>
          <p>{formatTurnStateMessage(game.turnState)}</p>
          <p className="muted">{toPlayerFacingText(game.turnState.playerGuidance, 'Следуйте безопасному состоянию хода.')}</p>
          <TurnLifecycleActions turnState={game.turnState} />
          <p className="muted">Быстрая сцена: {formatQteStateLabel(game.qte)}</p>
        </ShellPanel>
        <ShellPanel title="Варианты" eyebrow="для игрока" nested variant="choices">
          {game.narrative.dialogueOptions.length > 0 ? (
            <ul className="choice-list">
              {game.narrative.dialogueOptions.map((option) => (
                <li key={option.id}><strong>{option.text}</strong><span>{formatDialogueCategory(option.category)}</span></li>
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
          placeholder={getComposerPlaceholder(game.actionComposer)}
          disabled={!game.actionComposer.canSubmit}
        />
        <p className="muted">{getComposerGuidance(game.actionComposer)}</p>
        {!game.actionComposer.canSubmit && <p className="warning-text">{getComposerDisabledReason(game.actionComposer)}</p>}
        <button type="submit" disabled={!composerText.trim()}>Подготовить действие</button>
        {composerNotice && <p className="composer-notice">{composerNotice}</p>}
      </form>

      <section className="summary-card" aria-label="Жизненный цикл хода">
        <h3>Жизненный цикл хода</h3>
        <p className="muted">{toPlayerFacingText(game.turnState.phaseLabel, 'Текущее состояние хода')}</p>
        <div className="phase-chip-grid">
          {game.turnState.knownPhases.map((phase) => (
            <span key={phase.id} className={phase.id === game.turnState.phase ? 'status-pill' : 'status-pill is-muted'}>
              {toPlayerFacingText(phase.label, 'Этап')}
            </span>
          ))}
        </div>
      </section>
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
          <p>{formatRealmName(soul.realm)} · инкарнация {soul.incarnation}</p>
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
        <div className="summary-card"><h2>Журнал</h2><p>Квесты, архив и история разворачиваются в игровых разделах без знания ручных команд.</p></div>
        <div className="summary-card"><h2>Фракции</h2><p>Панели фракций и стражей используют общие игровые данные и не дублируют правила.</p></div>
      </div>
      <ActionMenu menu={game.actionMenu} />
    </ShellPanel>
  );
}

function ActionMenu({ menu }: { menu: BrowserPlayerCommandMenuDto }) {
  const sections = menu.sections.filter((section) => section.playerDefault && section.actions.length > 0);

  return (
    <section className="action-menu" aria-labelledby="contextual-actions-title">
      <div className="action-menu-header">
        <p className="panel-eyebrow">игровые действия</p>
        <h2 id="contextual-actions-title">Игровые действия</h2>
        <p className="muted">
          Персонаж / Душа, Мир, Квесты, Карта, Фракции, Хранители, Посмертие, Бой, Архив и Настройки
          собираются из игрового каталога действий. Технические имена команд остаются в расширенном режиме.
        </p>
      </div>
      <div className="action-section-grid">
        {sections.map((section) => (
          <ActionSection key={section.id} section={section} />
        ))}
      </div>
    </section>
  );
}

function TurnLifecycleActions({ turnState }: { turnState: BrowserGameScreenDto['turnState'] }) {
  const playerActions = turnState.recommendedActions.filter((action) => action.surface === 'player-default');
  const advancedActions = turnState.recommendedActions.filter((action) => action.surface === 'advanced-only');

  return (
    <div className="turn-lifecycle-actions" aria-label="Рекомендуемые действия состояния хода">
      {playerActions.length > 0 && (
        <ul className="choice-list">
          {playerActions.map((action) => (
            <li key={action.id}>
              <strong>{toPlayerFacingText(action.label, 'Действие')}</strong>
              <span>{formatTurnLifecycleActionDescription(action)}</span>
            </li>
          ))}
        </ul>
      )}
      {advancedActions.length > 0 && (
        <p className="muted">Технические действия для этого состояния доступны только через «Расширенный режим».</p>
      )}
    </div>
  );
}

function formatTurnLifecycleActionDescription(action: BrowserGameScreenDto['turnState']['recommendedActions'][number]): string {
  if (action.enabled) {
    return toPlayerFacingText(action.description, 'Действие доступно для текущего состояния хода.');
  }

  return toPlayerFacingText(action.disabledReason, 'Действие сейчас недоступно.');
}

function ActionSection({ section }: { section: BrowserPlayerCommandSectionDto }) {
  return (
    <section className="action-section" aria-labelledby={`action-section-${section.id}`}>
      <div>
        <h3 id={`action-section-${section.id}`}>{toPlayerFacingText(section.label, 'Игровой раздел')}</h3>
        <p className="muted">{toPlayerFacingText(section.description, 'Действия этого раздела доступны ниже.')}</p>
      </div>
      <div className="action-card-list">
        {section.actions.map((action) => (
          <ActionCard key={action.id} action={action} />
        ))}
      </div>
    </section>
  );
}

function ActionCard({ action }: { action: BrowserPlayerCommandActionDto }) {
  const [notice, setNotice] = useState('');
  const [commandResult, setCommandResult] = useState<BrowserApiResult<ExplorerCommandResult> | null>(null);
  const [promptAnswers, setPromptAnswers] = useState<PromptAnswers>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isGuidedForm = action.formMode !== 'none';

  async function submitGuidedForm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setNotice(isGuidedForm ? 'Открываем игровую форму…' : 'Открываем игровой раздел…');

    const result = await browserApi.executeExplorerCommand({ command: action.advancedCommand, ownerLabel: 'Игровое меню' });
    setCommandResult(result);
    if (isSuccess(result)) {
      setPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
      setNotice(toCommandNotice(result.data));
    } else {
      setNotice(toPlayerFacingText(result.playerMessage, 'Игровое действие сейчас недоступно.'));
    }
    setIsSubmitting(false);
  }

  async function submitPromptAnswers(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!commandResult || !isSuccess(commandResult) || !commandResult.data.interactiveSession) {
      return;
    }

    setIsSubmitting(true);
    setNotice('Отправляем заполненную форму…');
    const session = commandResult.data.interactiveSession;
    const result = await browserApi.submitPromptSession({
      sessionId: session.sessionId,
      ownerId: session.ownerId,
      answers: promptAnswers
    });
    setCommandResult(result);
    if (isSuccess(result)) {
      setPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
      setNotice(toCommandNotice(result.data));
    } else {
      setNotice(toPlayerFacingText(result.playerMessage, 'Игровое действие сейчас недоступно.'));
    }
    setIsSubmitting(false);
  }

  return (
    <article className={action.enabled ? 'action-card' : 'action-card is-disabled'}>
      <header>
        <h4>{toPlayerFacingText(action.label, 'Игровое действие')}</h4>
        <span className="availability-pill">{toPlayerFacingText(action.realmAvailability, 'Доступность уточняется.')}</span>
      </header>
      <p>{toPlayerFacingText(action.description, 'Описание действия появится здесь.')}</p>
      <p className={action.mutationMode === 'local-turn' ? 'warning-text' : 'muted'}>{toPlayerFacingText(action.mutationWarning, 'Состояние игры не изменится без подтверждения.')}</p>
      {!action.enabled && <p className="warning-text">{toPlayerFacingText(action.disabledReason, 'Действие сейчас недоступно.')}</p>}
      <form className="guided-form" onSubmit={submitGuidedForm}>
        <label htmlFor={`action-form-${action.id}`}>{isGuidedForm ? toPlayerFacingText(action.formLabel, 'Открыть форму') : 'Открыть раздел'}</label>
        <p id={`action-form-${action.id}`} className="muted">{toPlayerFacingText(action.formPrompt, 'Откройте игровой раздел без изменения состояния.')}</p>
        <button type="submit" disabled={!action.enabled || isSubmitting}>
          {isSubmitting ? 'Выполняем…' : isGuidedForm ? 'Подготовить форму' : 'Открыть раздел'}
        </button>
      </form>
      {notice && <p className="composer-notice">{notice}</p>}
      {commandResult && (
        <ActionCommandResult
          result={commandResult}
          promptAnswers={promptAnswers}
          onPromptAnswerChange={(promptId, value) => setPromptAnswers((current) => ({ ...current, [promptId]: value }))}
          onPromptSubmit={submitPromptAnswers}
          isSubmitting={isSubmitting}
        />
      )}
    </article>
  );
}

function ActionCommandResult({
  result,
  promptAnswers,
  onPromptAnswerChange,
  onPromptSubmit,
  isSubmitting
}: {
  result: BrowserApiResult<ExplorerCommandResult>;
  promptAnswers: PromptAnswers;
  onPromptAnswerChange: (promptId: string, value: JsonValue | undefined) => void;
  onPromptSubmit: (event: FormEvent<HTMLFormElement>) => void;
  isSubmitting: boolean;
}) {
  if (!isSuccess(result)) {
    return <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Игровое действие сейчас недоступно.')}</p>;
  }

  const command = result.data;
  return (
    <section className="command-result" aria-label="Результат игрового действия">
      <p className="status-pill">{commandStateLabel(command.state)}</p>
      {command.notifications.map((notification, index) => (
        <p key={`${notification.title}-${index}`} className="composer-notice">
          <strong>{toPlayerFacingText(notification.title, 'Уведомление')}</strong> — {toPlayerFacingText(notification.message, 'Игровое действие изменило состояние.')}
        </p>
      ))}
      {command.blocks.map((block, index) => (
        <div key={`${block.kind}-${index}`}>{renderCommandBlock(block)}</div>
      ))}
      {command.interactiveSession && command.prompts.length > 0 && (
        <form className="prompt-form" onSubmit={onPromptSubmit}>
          <h5>Заполните игровую форму</h5>
          {command.prompts.map((prompt) => renderPromptControl(prompt, promptAnswers[prompt.id], onPromptAnswerChange))}
          <button type="submit" disabled={isSubmitting}>Отправить форму</button>
        </form>
      )}
    </section>
  );
}

function renderCommandBlock(block: UiBlock): ReactNode {
  switch (block.kind) {
    case 'text':
      return <p>{toPlayerFacingText(block.text, 'Текст игрового действия недоступен.')}</p>;
    case 'panel':
      return (
        <div className="summary-card">
          <h5>{toPlayerFacingText(block.title, 'Игровая панель')}</h5>
          {block.blocks.map((child, index) => <div key={`${child.kind}-${index}`}>{renderCommandBlock(child)}</div>)}
        </div>
      );
    case 'table':
      return <p>{toPlayerFacingText(block.title, 'Таблица')}: {block.rows.length} строк.</p>;
    case 'list':
      return <ul>{block.items.map((item) => <li key={item}>{toPlayerFacingText(item, 'пункт списка')}</li>)}</ul>;
    case 'keyValueGrid':
      return <dl className="kv-list">{block.items.map((item) => <div key={item.key}><dt>{toPlayerFacingText(item.key, 'параметр')}</dt><dd>{toPlayerFacingText(item.value, 'значение')}</dd></div>)}</dl>;
    case 'message':
      return <p className="composer-notice"><strong>{toPlayerFacingText(block.title, 'Сообщение')}</strong> — {toPlayerFacingText(block.message, 'Игровое действие изменило состояние.')}</p>;
    case 'image':
      return <p>{toPlayerFacingText(block.title, 'Изображение')}: изображение готово к просмотру.</p>;
    case 'map':
      return <p>{toPlayerFacingText(block.title, 'Карта')}: карта содержит {block.map.nodes.length} точек.</p>;
    case 'rawJson':
      return <p className="muted">{toPlayerFacingText(block.title, 'Подробные данные')}: подробные данные доступны в расширенном режиме.</p>;
  }
}

function renderPromptControl(
  prompt: UiPrompt,
  value: JsonValue | undefined,
  onPromptAnswerChange: (promptId: string, value: JsonValue | undefined) => void
): ReactNode {
  const controlId = `prompt-${prompt.id}`;
  switch (prompt.kind) {
    case 'confirmation':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control checkbox-control">
          <input
            id={controlId}
            type="checkbox"
            checked={typeof value === 'boolean' ? value : prompt.defaultValue}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.checked)}
          />
          <span>{toPlayerFacingText(prompt.prompt, 'Подтвердите действие')}</span>
        </label>
      );
    case 'selection': {
      const selectedValue = typeof value === 'string' ? value : '';
      const selectedKnownOption = prompt.options.some((option) => option.value === selectedValue);
      const customValue = selectedValue && !selectedKnownOption ? selectedValue : '';

      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{toPlayerFacingText(prompt.prompt, 'Подтвердите действие')}</span>
          <select
            id={controlId}
            value={selectedKnownOption ? selectedValue : ''}
            required={prompt.required && !customValue}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
          >
            <option value="">Выберите вариант…</option>
            {prompt.options.map((option) => (
              <option key={option.value} value={option.value} disabled={option.disabled}>{toPlayerFacingText(option.label, 'вариант')}</option>
            ))}
          </select>
          {prompt.allowCustom && (
            <input
              type="text"
              value={customValue}
              placeholder="Или впишите свой вариант…"
              onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
            />
          )}
        </label>
      );
    }
    case 'longTextInput':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{toPlayerFacingText(prompt.prompt, 'Подтвердите действие')}</span>
          <textarea
            id={controlId}
            rows={prompt.minLines ?? 3}
            value={typeof value === 'string' ? value : prompt.defaultValue}
            placeholder={toPlayerFacingText(prompt.placeholder, '')}
            required={prompt.required}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
          />
        </label>
      );
    case 'textInput':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{toPlayerFacingText(prompt.prompt, 'Подтвердите действие')}</span>
          <input
            id={controlId}
            type="text"
            value={typeof value === 'string' ? value : prompt.defaultValue}
            placeholder={toPlayerFacingText(prompt.placeholder, '')}
            required={prompt.required}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
          />
        </label>
      );
  }
}

function buildDefaultPromptAnswers(prompts: UiPrompt[]): PromptAnswers {
  return Object.fromEntries(prompts.map((prompt) => [prompt.id, defaultPromptValue(prompt)]));
}

function defaultPromptValue(prompt: UiPrompt): JsonValue | undefined {
  switch (prompt.kind) {
    case 'confirmation':
      return prompt.defaultValue;
    case 'selection':
      return undefined;
    case 'longTextInput':
    case 'textInput':
      return prompt.defaultValue;
  }
}

function toCommandNotice(result: ExplorerCommandResult): string {
  switch (result.state) {
    case 'RequiresInput':
      return 'Форма открыта. Заполните поля ниже и отправьте её из браузера.';
    case 'Completed':
      return 'Игровое действие выполнено.';
    case 'Pending':
      return 'Действие ожидает ответа или завершения текущего хода.';
    case 'Blocked':
      return 'Действие сейчас заблокировано состоянием игры.';
    case 'Failed':
      return 'Действие не удалось выполнить; подробности показаны ниже.';
  }
}


function toPlayerFacingText(value: string | null | undefined, fallback: string): string {
  const source = value?.trim();
  if (!source) {
    return fallback;
  }

  const normalized = playerCopyReplacements.reduce(
    (text, [pattern, replacement]) => text.replace(pattern, replacement),
    source
  );

  return normalized.trim() || fallback;
}

function formatRealmName(realm: string): string {
  switch (realm.trim().toLowerCase()) {
    case 'mortal world':
    case 'mortal-world':
      return 'Мир смертных';
    case 'chaos sea':
    case 'chaos-sea':
      return 'Море Хаоса';
    case 'shining abode':
    case 'shining-abode':
      return 'Сияющая Обитель';
    default:
      return toPlayerFacingText(realm, 'царство уточняется');
  }
}

function formatDialogueCategory(category: string): string {
  switch (category.trim().toLowerCase()) {
    case 'exploration':
      return 'исследование';
    case 'dialogue':
    case 'social':
      return 'диалог';
    case 'combat':
      return 'бой';
    case 'lore':
      return 'знание';
    case 'world':
      return 'мир';
    case 'afterlife':
      return 'посмертие';
    default:
      return toPlayerFacingText(category, 'вариант выбора');
  }
}

function formatTurnStateTitle(turnState: BrowserGameScreenDto['turnState']): string {
  return toPlayerFacingText(turnState.title, formatTurnStateLabel(turnState.phase || turnState.state));
}

function formatTurnStateMessage(turnState: BrowserGameScreenDto['turnState']): string {
  return toPlayerFacingText(
    turnState.message,
    turnState.canStartBrowserWrite
      ? 'Опишите следующий ход персонажа в художественной форме.'
      : 'Запись хода сейчас недоступна; дождитесь безопасного состояния игры.'
  );
}

function getComposerPlaceholder(actionComposer: BrowserGameScreenDto['actionComposer']): string {
  return toPlayerFacingText(actionComposer.placeholder, 'Опишите действие персонажа обычным текстом…');
}

function getComposerGuidance(actionComposer: BrowserGameScreenDto['actionComposer']): string {
  return toPlayerFacingText(
    actionComposer.guidance,
    'Пишите действие персонажа обычным текстом; служебные команды доступны только в расширенном режиме.'
  );
}

function getComposerDisabledReason(actionComposer: BrowserGameScreenDto['actionComposer']): string {
  return toPlayerFacingText(actionComposer.disabledReason, 'Ввод временно недоступен по состоянию хода.');
}

function formatSessionStatus(status: string): string {
  switch (status.trim().toLowerCase()) {
    case 'ok':
    case 'ready':
      return 'Клиент готов';
    case 'missing':
    case 'not_found':
    case 'notfound':
      return 'Сохранение не найдено';
    case 'blocked':
      return 'Запись временно заблокирована';
    case 'error':
      return 'Нужна проверка состояния';
    default:
      return status.trim() ? 'Состояние требует внимания' : 'Состояние уточняется';
  }
}

function formatTurnStateLabel(state: string): string {
  switch (state.trim().toLowerCase()) {
    case 'idle':
    case 'ready':
      return 'Готово к ходу';
    case 'composing-action':
    case 'composing_action':
      return 'Игрок готовит действие';
    case 'turn-submitted':
    case 'turn_submitted':
      return 'Ход отправляется';
    case 'waitinggm':
    case 'waiting_gm':
    case 'waiting-gm':
    case 'pending':
    case 'pending-gm-turn':
      return 'Ожидаем ответ ГМа';
    case 'accepted':
      return 'Ответ ГМа принят';
    case 'validationfailed':
    case 'validation_failed':
    case 'validation-failed':
    case 'validation-errors':
      return 'Проверка не прошла';
    case 'repairrequired':
    case 'repair_required':
    case 'repair-required':
    case 'pending-turn-repair':
      return 'Нужна починка';
    case 'error-restored':
    case 'gm-turn-error':
      return 'Ошибка восстановлена';
    case 'cancelled':
      return 'Ход отменён';
    case 'blocked':
      return 'Ход заблокирован';
    case 'error':
      return 'Ошибка хода';
    default:
      return state.trim() ? 'Состояние хода' : 'Ход уточняется';
  }
}

function formatQteStateLabel(qte: BrowserGameScreenDto['qte']): string {
  if (qte.notification) {
    return toPlayerFacingText(qte.notification, 'Быстрая сцена изменила состояние.');
  }

  if (qte.error) {
    return 'Быстрая сцена требует внимания.';
  }

  switch (qte.state.trim().toLowerCase()) {
    case 'noscene':
    case 'none':
    case 'idle':
      return 'Быстрая сцена не активна.';
    case 'offer':
      return 'Доступна быстрая сцена.';
    case 'active':
      return 'Быстрая сцена активна.';
    case 'resolution':
    case 'resolved':
      return 'Быстрая сцена завершилась.';
    case 'completed':
      return 'Итог быстрой сцены записан.';
    default:
      return 'Состояние быстрой сцены уточняется.';
  }
}

function commandStateLabel(state: ExplorerCommandResult['state']): string {
  switch (state) {
    case 'RequiresInput':
      return 'Требуется ввод';
    case 'Completed':
      return 'Выполнено';
    case 'Pending':
      return 'Ожидает';
    case 'Blocked':
      return 'Заблокировано';
    case 'Failed':
      return 'Ошибка';
  }
}

function MediaRoute({ state }: { state: Extract<BrowserShellState, { status: 'ready' }> }) {
  if (!isSuccess(state.game)) {
    return <ApiFailure title="Медиа недоступны" result={state.game} advancedEnabled={false} />;
  }

  const qte = state.game.data.qte;

  return (
    <ShellPanel title="Медиа" eyebrow="галерея и быстрые сцены">
      <div className="split-grid">
        <div className="summary-card"><h2>Быстрые сцены</h2><p>{formatQteStateLabel(qte)}</p></div>
        <div className="summary-card"><h2>Галерея</h2><p>Изображения и кинематик-сцены будут подключаться через безопасный локальный просмотрщик.</p></div>
      </div>
    </ShellPanel>
  );
}

function SettingsRoute({
  state
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
}) {
  if (!isSuccess(state.menu)) {
    return <ApiFailure title="Настройки недоступны" result={state.menu} advancedEnabled={false} />;
  }

  const options = state.menu.data.options;

  return (
    <ShellPanel title="Настройки" eyebrow="локальность клиента">
      <dl className="kv-list">
        <div><dt>Размер шрифта</dt><dd>{options.consoleFontSize}</dd></div>
      </dl>
      <p>{toPlayerFacingText(options.guidance, 'Настройки локального клиента доступны здесь.')}</p>
      <p className="muted">Аудио управляется постоянной панелью в сводке состояния, чтобы музыка продолжала играть при переходах между разделами.</p>
    </ShellPanel>
  );
}

function AudioSettingsPanel({
  result,
  activeRoute
}: {
  result: BrowserApiResult<BrowserAudioSettingsDto>;
  activeRoute: RouteId;
}) {
  const [audioResult, setAudioResult] = useState(result);
  const [notice, setNotice] = useState('');
  const audioElementRef = useRef<HTMLAudioElement | null>(null);
  const audioSettingsUpdateQueueRef = useRef<Promise<void>>(Promise.resolve());

  useEffect(() => {
    setAudioResult(result);
  }, [result]);

  useEffect(() => () => {
    audioElementRef.current?.pause();
    audioElementRef.current = null;
  }, []);

  if (!isSuccess(audioResult)) {
    return <ApiFailure title="Аудио-настройки недоступны" result={audioResult} advancedEnabled={false} />;
  }

  const audio = audioResult.data;
  const playlist = selectPreferredPlaylist(audio, activeRoute);
  const hasMusic = Boolean(playlist?.tracks.length);
  const notificationCue = audio.cues.find((cue) => cue.id === 'turn-ready' && cue.asset) ?? audio.cues.find((cue) => cue.asset);

  function updateAudioSettings(request: BrowserAudioSettingsUpdateRequest) {
    audioSettingsUpdateQueueRef.current = audioSettingsUpdateQueueRef.current
      .catch(() => undefined)
      .then(async () => {
        try {
          const updated = await browserApi.updateAudioSettings(request);
          setAudioResult(updated);
          if (isSuccess(updated)) {
            const currentElement = audioElementRef.current;
            if (currentElement) {
              currentElement.volume = volumeToUnit(updated.data.musicVolume);
              if (!updated.data.musicEnabled) {
                currentElement.pause();
              }
            }
            setNotice('Настройки звука сохранены в общей конфигурации клиента.');
          } else {
            setNotice(toPlayerFacingText(updated.playerMessage, 'Не удалось сохранить настройки звука.'));
          }
        } catch {
          setNotice('Не удалось сохранить настройки звука. Попробуйте ещё раз или проверьте локальный клиент.');
        }
      });
    return audioSettingsUpdateQueueRef.current;
  }

  async function unlockBrowserMusic() {
    if (!audio.musicEnabled) {
      setNotice('Музыка выключена в общих настройках клиента. Включите её переключателем ниже.');
      return;
    }

    const track = playlist?.tracks[0];
    if (!track) {
      setNotice(toPlayerFacingText(audio.missingAssetsMessage, 'Аудиофайлы для выбранного плейлиста не найдены. Клиент продолжит игру без музыки.'));
      return;
    }

    const element = audioElementRef.current ?? new Audio();
    audioElementRef.current = element;
    element.loop = true;
    element.volume = volumeToUnit(audio.musicVolume);
    if (element.src !== new URL(track.url, window.location.href).href) {
      element.src = track.url;
    }

    try {
      await element.play();
      setNotice(`Музыка включена: ${toPlayerFacingText(playlist?.label ?? track.label, 'выбранный плейлист')}. Управление громкостью сохраняется в общих настройках.`);
    } catch {
      setNotice('Браузер не дал запустить музыку автоматически. Нажмите кнопку ещё раз или проверьте разрешения вкладки.');
    }
  }

  async function previewCue(asset: BrowserAudioAssetDto | null | undefined) {
    if (!asset) {
      setNotice('Файл звуковой подсказки не найден, поэтому предпросмотр недоступен.');
      return;
    }

    if (!audio.soundEnabled) {
      setNotice('Звуковые подсказки выключены в общих настройках клиента.');
      return;
    }

    const cueAudio = new Audio();
    cueAudio.src = asset.url;
    cueAudio.volume = volumeToUnit(audio.soundVolume);
    try {
      await cueAudio.play();
      setNotice(`Звуковая подсказка воспроизведена: ${toPlayerFacingText(asset.label, 'подсказка')}.`);
    } catch {
      setNotice('Браузер не дал запустить звуковую подсказку. Нажмите кнопку ещё раз или проверьте разрешения вкладки.');
    }
  }

  return (
    <section className="audio-control-panel" aria-labelledby="browser-audio-title">
      <div>
        <p className="panel-eyebrow">музыка и звук</p>
        <h2 id="browser-audio-title">Аудио браузерного клиента</h2>
        <p>{toPlayerFacingText(audio.autoplayGuidance, 'Музыка запускается только после вашего нажатия.')}</p>
        {audio.missingAssetsMessage && <p className="warning-text">{toPlayerFacingText(audio.missingAssetsMessage, 'Локальные аудиофайлы не найдены.')}</p>}
      </div>

      <div className="split-grid">
        <div className="summary-card">
          <h3>Музыка</h3>
          <p>{playlist ? `${toPlayerFacingText(playlist.label, 'Плейлист')}: ${toPlayerFacingText(playlist.usage, 'музыка для текущего раздела')}` : 'Плейлисты пока недоступны.'}</p>
          <button type="button" onClick={unlockBrowserMusic} disabled={!audio.musicEnabled || !hasMusic}>
            Включить музыку в браузере
          </button>
          {!hasMusic && <p className="muted">Когда в локальной папке появятся треки, браузер сможет включить их после вашего нажатия.</p>}
        </div>
        <div className="summary-card">
          <h3>Звуковые подсказки</h3>
          <p>{notificationCue?.usage ? toPlayerFacingText(notificationCue.usage, 'Быстрые сцены и уведомления будут звучать, если локальные файлы найдены.') : 'Быстрые сцены и уведомления будут звучать, если локальные файлы найдены.'}</p>
          <button type="button" onClick={() => void previewCue(notificationCue?.asset)} disabled={!audio.soundEnabled || !notificationCue?.asset}>
            Проверить подсказку
          </button>
        </div>
      </div>

      <div className="audio-settings-grid">
        <label className="audio-toggle">
          <input
            type="checkbox"
            checked={audio.musicEnabled}
            onChange={(event) => void updateAudioSettings({ musicEnabled: event.currentTarget.checked })}
          />
          <span>Музыка включена</span>
        </label>
        <label className="audio-toggle">
          <input
            type="checkbox"
            checked={audio.soundEnabled}
            onChange={(event) => void updateAudioSettings({ soundEnabled: event.currentTarget.checked })}
          />
          <span>Звуковые подсказки включены</span>
        </label>
        <label className="audio-slider">
          <span>Громкость музыки: {audio.musicVolume}%</span>
          <input
            type="range"
            min="0"
            max="100"
            value={audio.musicVolume}
            onChange={(event) => void updateAudioSettings({ musicVolume: Number(event.currentTarget.value) })}
          />
        </label>
        <label className="audio-slider">
          <span>Громкость подсказок: {audio.soundVolume}%</span>
          <input
            type="range"
            min="0"
            max="100"
            value={audio.soundVolume}
            onChange={(event) => void updateAudioSettings({ soundVolume: Number(event.currentTarget.value) })}
          />
        </label>
      </div>

      <div className="audio-catalog" aria-label="Доступные плейлисты и подсказки">
        {audio.playlists.map((item) => (
          <span key={item.id} className={item.available ? 'status-pill' : 'status-pill is-muted'}>
            {toPlayerFacingText(item.label, 'Плейлист')}: {item.available ? `${item.tracks.length} трек(ов)` : 'файлы не найдены'}
          </span>
        ))}
        {audio.cues.map((cue) => (
          <span key={cue.id} className={cue.available ? 'status-pill' : 'status-pill is-muted'}>
            {toPlayerFacingText(cue.label, 'Звуковая подсказка')}: {cue.available ? 'готово' : 'нет файла'}
          </span>
        ))}
      </div>
      {notice && <p className="composer-notice">{notice}</p>}
    </section>
  );
}

function selectPreferredPlaylist(audio: BrowserAudioSettingsDto, activeRoute: RouteId): BrowserAudioPlaylistDto | null {
  const preferredId = activeRoute === 'home' ? 'main-menu' : 'in-game';
  return audio.playlists.find((playlist) => playlist.id === preferredId && playlist.available)
    ?? audio.playlists.find((playlist) => playlist.available)
    ?? null;
}

function volumeToUnit(value: number): number {
  return Math.min(1, Math.max(0, value / 100));
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
          {lifecycle.validation.groups.length > 0 && (
            <ul className="endpoint-list validation-group-list" aria-label="Группы проверки состояния">
              {lifecycle.validation.groups.map((group) => (
                <li key={`${group.severity}-${group.category}-${group.section}`}>
                  <strong>{group.severity} · {group.category}</strong>
                  <span>{group.section} · {group.count}</span>
                </li>
              ))}
            </ul>
          )}
          {lifecycle.validation.issues.length > 0 && (
            <details>
              <summary>Raw validation details</summary>
              <ul className="endpoint-list validation-issue-list">
                {lifecycle.validation.issues.map((issue, index) => (
                  <li key={`${issue.filePath}-${issue.code}-${index}`}>
                    <strong>{issue.filePath}</strong>
                    <span>{issue.severity} · {issue.category} · {issue.section}</span>
                    <span>{issue.message}</span>
                    <span>Ожидалось: {issue.expected || '—'} · Сейчас: {issue.actual || '—'}</span>
                    <span>Repair: {issue.repairHint || '—'}</span>
                  </li>
                ))}
              </ul>
            </details>
          )}
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
      <p>{toPlayerFacingText(failure.playerMessage, 'Игровое состояние сейчас недоступно.')}</p>
      {failure.technicalDetails && advancedEnabled && (
        <details open>
          <summary>Подробности</summary>
          <pre>{failure.technicalDetails}</pre>
        </details>
      )}
      {failure.technicalDetails && !advancedEnabled && (
        <p className="muted">Технические подробности доступны после явного включения расширенного режима.</p>
      )}
    </section>
  );
}

function LoadingCard() {
  return (
    <ShellPanel title="Загрузка" eyebrow="локальный клиент">
      <p>Собираем главное меню, сессию, игровой экран и состояние хода из локального клиента…</p>
    </ShellPanel>
  );
}

function ShellPanel({
  title,
  eyebrow,
  children,
  nested = false,
  variant
}: {
  title: string;
  eyebrow: string;
  children: ReactNode;
  nested?: boolean;
  variant?: string;
}) {
  const className = ['shell-panel', nested ? 'is-nested' : '', variant ? `panel-${variant}` : '']
    .filter(Boolean)
    .join(' ');

  return (
    <section className={className} data-panel={variant ?? title}>
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
