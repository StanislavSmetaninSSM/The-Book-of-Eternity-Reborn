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
  BrowserCommandCoverageDto,
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

type RouteId = 'home' | 'game' | 'soul' | 'world' | 'journal' | 'inventory' | 'media' | 'settings';
type RouteKind = 'primary' | 'utility';
type LauncherMode = 'continue' | 'load' | 'new-game' | 'settings' | 'about';

type BrowserShellState =
  | { status: 'loading' }
  | { status: 'ready'; menu: BrowserApiResult<BrowserMainMenuDto>; session: BrowserApiResult<LocalWebUiSessionStatus>; game: BrowserApiResult<BrowserGameScreenDto>; audio: BrowserApiResult<BrowserAudioSettingsDto>; lifecycle: BrowserApiResult<BrowserLifecycleDashboardDto> | null; commandCoverage: BrowserApiResult<BrowserCommandCoverageDto> | null }
  | { status: 'error'; playerMessage: string; technicalDetails?: string };

type PromptAnswers = Record<string, JsonValue | undefined>;

interface RouteCard {
  id: RouteId;
  kind: RouteKind;
  label: string;
  description: string;
  icon: string;
}

interface EmptyStateCopy {
  title: string;
  message: string;
  action: string;
}

interface RealmTheme {
  key: string;
  label: string;
  icon: string;
  accent: string;
}

interface LauncherPrimaryAction {
  mode: LauncherMode;
  label: string;
  description: string;
  enabled: boolean;
  disabledReason: string;
}

const playerRoutes: RouteCard[] = [
  { id: 'home', kind: 'primary', label: 'Главная', description: 'Сводка партии, продолжение, загрузка и безопасные действия.', icon: '✦' },
  { id: 'game', kind: 'primary', label: 'Игра', description: 'Текущая сцена, нарратив, ход ГМа и основной художественный ввод.', icon: '📖' },
  { id: 'soul', kind: 'primary', label: 'Душа', description: 'Персонаж, душа, состояние героя и текущий слой мира.', icon: '🕯️' },
  { id: 'world', kind: 'primary', label: 'Мир', description: 'Локация, карта, фракции и игровые действия окружения.', icon: '🗺️' },
  { id: 'journal', kind: 'primary', label: 'Журнал', description: 'Квесты, хроника, заметки, архив и история текущей главы.', icon: '✍️' },
  { id: 'inventory', kind: 'primary', label: 'Инвентарь', description: 'Предметы, экипировка, ремесло и локальные хранилища.', icon: '🎒' },
  { id: 'media', kind: 'utility', label: 'Медиа', description: 'Галерея, быстрые сцены и игровые материалы.', icon: '🎞️' },
  { id: 'settings', kind: 'utility', label: 'Настройки', description: 'Локальный профиль, звук, язык и комфорт клиента.', icon: '⚙️' }
];

const primaryPlayerRoutes = playerRoutes.filter((route) => route.kind === 'primary');
const utilityPlayerRoutes = playerRoutes.filter((route) => route.kind === 'utility');

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
  [/debug shell/gi, 'служебная оболочка'],
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
  [/C\x23\s*/g, ''],
  [/\blifecycle\b/gi, 'состояние хода'],
  [/\bruntime\b/gi, 'игровой слой'],
  [/\bendpoint(s)?\b/gi, 'разделы локального интерфейса'],
  [/\bAPI\b/g, 'локальный интерфейс'],
  [/\bDTO\b/g, 'данные интерфейса'],
  [/\bNPC\b/g, 'персонажи мира']
];

const launcherAboutCopyReplacements: Array<[RegExp, string]> = [
  [/\bdebug\b/gi, 'служебная'],
  [/\bdiagnostics?\b/gi, 'проверочные сведения'],
  [/\btechnical details?\b/gi, 'служебные сведения'],
  [/\btechnical\b/gi, 'служебный'],
  [/\bdeveloper\b/gi, 'служебный'],
  [/\braw JSON\b/gi, 'подробные данные']
];

const launcherModes: LauncherMode[] = ['continue', 'load', 'new-game', 'settings', 'about'];

const launcherModeDetails: Record<LauncherMode, { label: string; description: string }> = {
  continue: {
    label: 'Продолжить главу',
    description: 'Вернуться к текущей сохранённой главе.'
  },
  load: {
    label: 'Загрузить сохранение',
    description: 'Выбрать одну из доступных локальных записей.'
  },
  'new-game': {
    label: 'Начать новую главу',
    description: 'Подготовить новую историю через управляемую форму браузера.'
  },
  settings: {
    label: 'Настроить клиент',
    description: 'Открыть настройки локального клиента и звука.'
  },
  about: {
    label: 'Сведения о книге',
    description: 'Показать краткое описание книги и браузерного клиента.'
  }
};

function GameLauncher({
  menu,
  onActiveRouteChange,
  onStateRefresh
}: {
  menu: BrowserMainMenuDto;
  onActiveRouteChange: (route: RouteId) => void;
  onStateRefresh: () => Promise<void>;
}) {
  const primaryAction = useMemo(() => selectPrimaryLauncherAction(menu), [menu]);
  const [activeMode, setActiveMode] = useState<LauncherMode>(primaryAction.mode);
  const [launcherNotice, setLauncherNotice] = useState('');
  const [loadingSaveId, setLoadingSaveId] = useState<string | null>(null);
  const isLauncherMountedRef = useRef(true);

  useEffect(() => {
    isLauncherMountedRef.current = true;
    return () => {
      isLauncherMountedRef.current = false;
    };
  }, []);

  function activateLauncherMode(mode: LauncherMode) {
    setLauncherNotice('');
    if (mode === 'continue') {
      onActiveRouteChange('game');
      return;
    }

    if (mode === 'settings') {
      onActiveRouteChange('settings');
      return;
    }

    setActiveMode(mode);
  }

  async function loadSaveSlot(slot: BrowserMainMenuDto['saves'][number]) {
    setLoadingSaveId(slot.saveId);
    setLauncherNotice('Загружаем выбранное сохранение…');

    try {
      const result = await browserApi.loadSave({ saveId: slot.saveId });
      if (!isLauncherMountedRef.current) {
        return;
      }

      if (isSuccess(result) && result.data.success) {
        setLauncherNotice(`Сохранение «${toPlayerFacingText(slot.displayName, 'выбранная запись')}» загружено. Открываем главу…`);
        onActiveRouteChange('game');
        await onStateRefresh();
        return;
      }

      if (isSuccess(result)) {
        setLauncherNotice(toLauncherSaveFailureNotice(result.data.error));
        return;
      }

      setLauncherNotice(toLauncherSaveFailureNotice(result.playerMessage));
    } catch {
      if (!isLauncherMountedRef.current) {
        return;
      }

      setLauncherNotice('Сохранение не удалось загрузить. Проверьте локальный клиент и попробуйте ещё раз.');
    } finally {
      if (isLauncherMountedRef.current) {
        setLoadingSaveId(null);
      }
    }
  }

  function renderModeContent(): ReactNode {
    const modeAction = findLauncherMenuAction(menu, activeMode);
    const modeDescription = launcherActionDescription(menu, activeMode);

    switch (activeMode) {
      case 'continue':
        return (
          <section className="launcher-mode-panel" aria-label="Продолжение главы">
            <h3>Продолжить главу</h3>
            <p>{toPlayerFacingText(menu.session.continueReason, 'Книга сообщит, когда текущую главу можно продолжить.')}</p>
            <dl className="kv-list">
              <div><dt>Душа</dt><dd>{menu.session.soulName || 'Новая душа'}</dd></div>
              <div><dt>Царство</dt><dd>{toPlayerFacingText(menu.session.realmLabel, 'царство уточняется')}</dd></div>
              <div><dt>Ход</dt><dd>{toPlayerFacingText(menu.session.turnLabel, 'ход уточняется')}</dd></div>
            </dl>
            {!modeAction?.enabled && <p className="warning-text">{launcherActionDescription(menu, 'continue')}</p>}
          </section>
        );
      case 'load': {
        const loadAction = findLauncherMenuAction(menu, 'load');
        const loadAvailable = Boolean(loadAction?.enabled);
        return (
          <section className="launcher-mode-panel" aria-label="Загрузка сохранения">
            <h3>Загрузить сохранение</h3>
            <p>{modeDescription}</p>
            <div className="launcher-save-list">
              {menu.saves.length > 0 ? menu.saves.map((slot) => (
                <article key={slot.saveId} className="launcher-save-card">
                  <div>
                    <h4>{toPlayerFacingText(slot.displayName, 'Сохранение')}</h4>
                    <p>{toPlayerFacingText(slot.description, 'Локальная запись готова к загрузке.')}</p>
                  </div>
                  <dl className="kv-list">
                    <div><dt>Тип</dt><dd>{toPlayerFacingText(slot.scopeLabel, 'сохранение')}</dd></div>
                    <div><dt>Герой</dt><dd>{slot.characterName || 'не указан'}</dd></div>
                    <div><dt>Ход</dt><dd>{toPlayerFacingText(slot.turnLabel, 'ход уточняется')}</dd></div>
                  </dl>
                  <button
                    type="button"
                    className="launcher-secondary-action"
                    disabled={!loadAvailable || loadingSaveId !== null}
                    onClick={() => void loadSaveSlot(slot)}
                  >
                    {loadingSaveId === slot.saveId ? 'Загружаем…' : 'Загрузить сохранение'}
                  </button>
                </article>
              )) : (
                <p className="muted">Сохранений пока нет. Когда локальная книга найдёт ручные или автоматические записи, они появятся здесь.</p>
              )}
            </div>
            {!loadAvailable && <p className="warning-text">{launcherActionDescription(menu, 'load')}</p>}
          </section>
        );
      }
      case 'new-game':
        return (
          <section className="launcher-mode-panel" aria-label="Новая глава">
            <h3>Начать новую главу</h3>
            <p>{modeDescription}</p>
            <p className="muted">
              Управляемая браузерная форма будет использовать существующий локальный поток подготовки новой игры; браузер не добавляет отдельные игровые правила.
            </p>
            {!modeAction?.enabled && <p className="warning-text">{launcherActionDescription(menu, 'new-game')}</p>}
          </section>
        );
      case 'settings':
        return (
          <section className="launcher-mode-panel" aria-label="Настройки клиента">
            <h3>Настроить клиент</h3>
            <p>{toPlayerFacingText(menu.options.guidance, 'Настройки локального клиента доступны в отдельном разделе.')}</p>
            <button type="button" className="launcher-secondary-action" onClick={() => onActiveRouteChange('settings')}>
              Открыть настройки
            </button>
          </section>
        );
      case 'about':
        return (
          <section className="launcher-mode-panel" aria-label="Сведения о книге">
            <h3>Сведения о книге</h3>
            <h4>{toPlayerFacingText(menu.about.title, 'Книга Вечности: Перерождение')}</h4>
            <p>{playerLauncherAboutText(menu.about.body)}</p>
          </section>
        );
    }
  }

  return (
    <article className="game-launcher" aria-labelledby="browser-launcher-title">
      <div className="launcher-window">
        <div className="launcher-copy">
          <p className="panel-eyebrow">главная книга</p>
          <h2 id="browser-launcher-title">Открыть книгу</h2>
          <p>{toPlayerFacingText(menu.session.continueReason, 'Выберите продолжение, загрузку или новую главу.')}</p>
        </div>

        <button
          type="button"
          className="launcher-primary-action"
          disabled={!primaryAction.enabled}
          onClick={() => {
            activateLauncherMode(primaryAction.mode);
          }}
        >
          <strong>{primaryAction.label}</strong>
          <span>{primaryAction.enabled ? primaryAction.description : primaryAction.disabledReason}</span>
        </button>

        <div className="launcher-mode-tabs" role="tablist" aria-label="Режимы главной книги">
          {launcherModes.map((mode) => {
            const details = launcherModeDetails[mode];
            const action = findLauncherMenuAction(menu, mode);
            return (
              <button
                key={mode}
                type="button"
                role="tab"
                aria-selected={activeMode === mode}
                className={`launcher-mode-tab${activeMode === mode ? ' is-active' : ''}`}
                onClick={() => setActiveMode(mode)}
              >
                <strong>{details.label}</strong>
                <span>{action && !action.enabled ? 'пока недоступно' : 'открыть'}</span>
              </button>
            );
          })}
        </div>

        {renderModeContent()}

        <div className="launcher-secondary-actions">
          {launcherModes.filter((mode) => mode !== primaryAction.mode).map((mode) => {
            const details = launcherModeDetails[mode];
            const action = findLauncherMenuAction(menu, mode);
            const disabled = Boolean(action && !action.enabled && mode !== 'settings' && mode !== 'about');
            return (
              <button
                key={mode}
                type="button"
                className="launcher-secondary-action"
                disabled={disabled}
                onClick={() => activateLauncherMode(mode)}
              >
                <strong>{details.label}</strong>
                <span>{launcherActionDescription(menu, mode)}</span>
              </button>
            );
          })}
        </div>

        {launcherNotice && <p className="composer-notice">{launcherNotice}</p>}
      </div>
    </article>
  );
}

function selectPrimaryLauncherAction(menu: BrowserMainMenuDto): LauncherPrimaryAction {
  const preferredModes: LauncherMode[] = ['continue', 'load', 'new-game'];

  for (const mode of preferredModes) {
    const action = findLauncherMenuAction(menu, mode);
    if (action?.enabled) {
      return {
        mode,
        label: launcherModeDetails[mode].label,
        description: toPlayerFacingText(action.description, launcherModeDetails[mode].description),
        enabled: true,
        disabledReason: ''
      };
    }
  }

  const fallback = preferredModes
    .map((mode) => ({ mode, action: findLauncherMenuAction(menu, mode) }))
    .find((candidate) => candidate.action);
  const disabledReason = fallback?.action
    ? toPlayerFacingText(fallback.action.disabledReason || fallback.action.description, 'Главные действия книги сейчас недоступны.')
    : 'Главные действия книги сейчас недоступны.';

  return {
    mode: fallback?.mode ?? 'continue',
    label: 'Открыть книгу',
    description: 'Выберите продолжение, загрузку или новую главу, когда книга будет готова.',
    enabled: false,
    disabledReason
  };
}

function findLauncherMenuAction(menu: BrowserMainMenuDto, mode: LauncherMode): BrowserMainMenuDto['actions'][number] | undefined {
  switch (mode) {
    case 'continue':
      return menu.actions.find((action) => action.id === 'continue');
    case 'load':
      return menu.actions.find((action) => action.id === 'load');
    case 'new-game':
      return menu.actions.find((action) => action.id === 'new-game');
    case 'settings':
      return menu.actions.find((action) => action.id === 'options' || action.id === 'settings' || action.targetPanel === 'options-panel');
    case 'about':
      return menu.actions.find((action) => action.id === 'about' || action.targetPanel === 'about-panel');
  }
}

function launcherActionDescription(menu: BrowserMainMenuDto, mode: LauncherMode): string {
  const action = findLauncherMenuAction(menu, mode);
  const fallback = launcherModeDetails[mode].description;
  if (!action) {
    return fallback;
  }

  return action.enabled
    ? toPlayerFacingText(action.description, fallback)
    : toPlayerFacingText(action.disabledReason || action.description, fallback);
}

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
      const [lifecycle, commandCoverage] = advancedEnabled ? await Promise.all([
        browserApi.getLifecycleDashboard(),
        browserApi.getCommandCoverage()
      ]) : [null, null];

      setShellState({ status: 'ready', menu, session, game, audio, lifecycle, commandCoverage });
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
  const commandCoverage = readyState ? readyState.commandCoverage : null;
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
            <span>{gameScreen ? formatTurnStateTitle(gameScreen.turnState) : menu?.session.validationLabel ?? 'Книга ждёт открытия'}</span>
          </div>
        </div>
      </section>

      <nav className="route-grid route-grid--primary" aria-label="Основные игровые разделы браузерного клиента">
        {primaryPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, setActiveRoute))}
      </nav>

      <nav className="route-grid route-grid--utility" aria-label="Дополнительные игровые разделы браузерного клиента">
        <p className="utility-route-heading">Сводка / Игра / Душа / Мир / Журнал / Инвентарь — основная цепочка игрока. Медиа и настройки доступны отдельно.</p>
        {utilityPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, setActiveRoute))}
      </nav>

      <section className="workspace-grid" aria-live="polite">
        <div className="workspace-main">
          {shellState.status === 'loading' && <LoadingCard />}
          {shellState.status === 'error' && (
            <ErrorNotice title="Состояние клиента недоступно" failure={shellState} advancedEnabled={advancedEnabled} />
          )}
          {readyState && renderActiveRoute(activeRoute, readyState, composerText, setComposerText, composerNotice, submitComposer, advancedEnabled, setActiveRoute, loadBrowserState)}
        </div>

        <aside className="workspace-sidebar" aria-label="Сводка книги">
          <PlayerStatusSidebar
            readyState={readyState}
            menu={menu}
            session={session}
            gameScreen={gameScreen}
            realmTheme={realmTheme}
            activeRoute={activeRoute}
            advancedEnabled={advancedEnabled}
            setAdvancedEnabled={setAdvancedEnabled}
          />
        </aside>
      </section>

      {advancedEnabled && readyState && <AdvancedDiagnosticsPanel state={readyState} lifecycle={lifecycle} commandCoverage={commandCoverage} />}
    </main>
  );
}


function renderRouteButton(route: RouteCard, activeRoute: RouteId, setActiveRoute: (route: RouteId) => void): ReactNode {
  return (
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
  );
}

function PlayerStatusSidebar({
  readyState,
  menu,
  session,
  gameScreen,
  realmTheme,
  activeRoute,
  advancedEnabled,
  setAdvancedEnabled
}: {
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null;
  menu: BrowserMainMenuDto | null;
  session: LocalWebUiSessionStatus | null;
  gameScreen: BrowserGameScreenDto | null;
  realmTheme: RealmTheme;
  activeRoute: RouteId;
  advancedEnabled: boolean;
  setAdvancedEnabled: (updater: (value: boolean) => boolean) => void;
}) {
  const sidebarEmptyGame = getSidebarEmptyGameMessage(readyState);
  const hasGame = Boolean(gameScreen);
  const sidebarMenuFailure = getSidebarFailure(readyState?.menu);
  const sidebarSessionFailure = getSidebarFailure(readyState?.session);
  const sidebarGameFailure = getSidebarFailure(readyState?.game);
  const saveNeedsAttention = Boolean(sidebarMenuFailure || sidebarSessionFailure);
  const turnNeedsAttention = Boolean(sidebarGameFailure || gameScreen?.turnState.severity === 'error' || gameScreen?.turnState.severity === 'repair');

  return (
    <div className="player-status-sidebar">
      <div className="sidebar-heading">
        <p className="panel-eyebrow">игровая сводка</p>
        <h2>Сводка книги</h2>
        <p className="muted">Мягкая сводка текущей главы без служебных журналов и внутренних проверок.</p>
      </div>

      <StatusSummaryCard title="Слой книги" eyebrow="мир и глава" attention={Boolean(sidebarMenuFailure || sidebarGameFailure)}>
        <p className="status-pill">{realmTheme.label}</p>
        <p>{gameScreen ? `${gameScreen.soul.name || 'Душа'} · ход ${gameScreen.world.turnNumber}` : sidebarEmptyGame}</p>
        {sidebarMenuFailure ? (
          <p className="warning-text">{sidebarMenuFailure}</p>
        ) : (
          <p className="muted">{menu ? toPlayerFacingText(menu.session.validationLabel, 'Книга ждёт открытия') : 'Книга ждёт открытия.'}</p>
        )}
      </StatusSummaryCard>

      <StatusSummaryCard title="Герой и душа" eyebrow="персонаж" soft={!hasGame && !sidebarGameFailure} attention={Boolean(sidebarGameFailure)}>
        {sidebarGameFailure ? (
          <>
            <p className="warning-text">{sidebarGameFailure}</p>
            <p className="muted">Герой и душа появятся снова, когда локальная книга отдаст игровую сводку.</p>
          </>
        ) : gameScreen ? (
          <>
            <p><strong>{gameScreen.player.name || 'Герой'}</strong> · {gameScreen.player.currentCondition}</p>
            <p className="muted">Душа: {gameScreen.soul.name || 'без имени'} · {formatRealmName(gameScreen.soul.realm)}</p>
            <div className="status-summary-grid" aria-label="Состояние героя">
              <span>Здоровье {formatSidebarStatusMetric(gameScreen.player.healthPercentage)}</span>
              <span>Энергия {formatSidebarStatusMetric(gameScreen.player.energyPercentage)}</span>
              <span>Стойкость {formatSidebarStatusMetric(gameScreen.player.poisePercentage)}</span>
            </div>
          </>
        ) : (
          <>
            <p>Душа и герой появятся после открытия или загрузки главы.</p>
            <p className="muted">Это обычное состояние пустой книги, не ошибка клиента.</p>
          </>
        )}
      </StatusSummaryCard>

      <StatusSummaryCard title="Сохранение" eyebrow="локальная партия" soft={!session?.gameSessionExists && !saveNeedsAttention} attention={saveNeedsAttention}>
        {sidebarSessionFailure ? (
          <p className="warning-text">{sidebarSessionFailure}</p>
        ) : (
          <p>{formatSidebarSessionSummary(session, menu)}</p>
        )}
        {sidebarMenuFailure ? (
          <p className="warning-text">{sidebarMenuFailure}</p>
        ) : (
          <p className="muted">{formatSidebarSaveSummary(menu)}</p>
        )}
      </StatusSummaryCard>

      <StatusSummaryCard title="Ожидание ГМа" eyebrow="ход" attention={turnNeedsAttention}>
        {sidebarGameFailure ? (
          <>
            <p className="warning-text">{sidebarGameFailure}</p>
            <p className="muted">Глава сохранена; подробности ремонта и проверки остаются в расширенном режиме.</p>
          </>
        ) : gameScreen ? (
          <>
            <p className={`status-pill turn-phase turn-phase--${gameScreen.turnState.severity}`}>{formatTurnStateTitle(gameScreen.turnState)}</p>
            <p>{formatTurnStateMessage(gameScreen.turnState)}</p>
            <p className="muted">Подробности ремонта, проверки и команд скрыты до явного включения.</p>
          </>
        ) : (
          <>
            <p>{sidebarEmptyGame}</p>
            <p className="muted">Когда появится ожидающий ход или ответ ГМа, книга покажет это здесь игровым языком.</p>
          </>
        )}
      </StatusSummaryCard>

      {readyState && <AudioSettingsPanel result={readyState.audio} activeRoute={activeRoute} advancedEnabled={advancedEnabled} />}

      <section className="advanced-sidebar-entry" aria-label="Служебная панель">
        <div>
          <p className="panel-eyebrow">по запросу</p>
          <h3>Служебная панель</h3>
          <p className="muted">Служебные проверки и сведения для ремонта остаются вторичным режимом.</p>
        </div>
        <button
          type="button"
          className="advanced-toggle"
          aria-controls="advanced-diagnostics"
          aria-expanded={advancedEnabled}
          onClick={() => setAdvancedEnabled((value) => !value)}
        >
          {advancedEnabled ? 'Скрыть расширенный режим' : 'Открыть расширенный режим'}
        </button>
      </section>
    </div>
  );
}

function StatusSummaryCard({
  title,
  eyebrow,
  children,
  soft = false,
  attention = false
}: {
  title: string;
  eyebrow: string;
  children: ReactNode;
  soft?: boolean;
  attention?: boolean;
}) {
  const className = `status-summary-card${soft ? ' is-soft' : ''}${attention ? ' is-attention' : ''}`;
  return (
    <section className={className}>
      <p className="panel-eyebrow">{eyebrow}</p>
      <h3>{title}</h3>
      {children}
    </section>
  );
}

function getSidebarFailure<TData>(result: BrowserApiResult<TData> | null | undefined): string | null {
  if (!result || isSuccess(result) || result.kind === 'no-active-session') {
    return null;
  }

  return toPlayerFacingText(result.playerMessage, 'Книга требует внимания.');
}

function getSidebarEmptyGameMessage(readyState: Extract<BrowserShellState, { status: 'ready' }> | null): string {
  const gameFailure = getSidebarFailure(readyState?.game);
  if (gameFailure) {
    return gameFailure;
  }

  return 'Книга ждёт открытия главы.';
}

function formatSidebarSessionSummary(session: LocalWebUiSessionStatus | null, menu: BrowserMainMenuDto | null): string {
  if (session?.gameSessionExists) {
    return session.canStartBrowserWrite
      ? 'Локальная партия найдена, запись следующего хода доступна.'
      : 'Локальная партия найдена, но ход сейчас ждёт безопасного момента.';
  }

  if (menu?.session.gameSessionExists || menu?.session.canContinue) {
    return 'Есть глава, которую можно продолжить с главной страницы.';
  }

  return 'Активной главы пока нет — начните новую или загрузите сохранение.';
}

function formatSidebarSaveSummary(menu: BrowserMainMenuDto | null): string {
  if (!menu) {
    return 'Список сохранений появится после ответа локальной книги.';
  }

  if (menu.saves.length > 0) {
    return `Доступно сохранений: ${menu.saves.length}. Последние записи доступны на главной странице.`;
  }

  return 'Сохранений пока не найдено; можно начать новую главу.';
}

function formatSidebarStatusMetric(value: string): string {
  const normalized = value.trim();
  if (!normalized) {
    return '—';
  }

  return normalized.endsWith('%') ? normalized : `${normalized}%`;
}

function formatSidebarAudioSummary(audio: BrowserAudioSettingsDto): string {
  const availablePlaylists = audio.playlists.filter((playlist) => playlist.available).length;
  const availableCues = audio.cues.filter((cue) => cue.available).length;
  return `Музыка ${audio.musicEnabled ? 'включена' : 'выключена'}; плейлистов найдено: ${availablePlaylists}; подсказок: ${availableCues}.`;
}

function renderActiveRoute(
  activeRoute: RouteId,
  state: Extract<BrowserShellState, { status: 'ready' }>,
  composerText: string,
  setComposerText: (value: string) => void,
  composerNotice: string,
  submitComposer: (event: FormEvent<HTMLFormElement>) => void,
  advancedEnabled: boolean,
  setActiveRoute: (route: RouteId) => void,
  loadBrowserState: () => Promise<void>
) {
  switch (activeRoute) {
    case 'home':
      return <HomeRoute state={state} advancedEnabled={advancedEnabled} onActiveRouteChange={setActiveRoute} onStateRefresh={loadBrowserState} />;
    case 'game':
      return <GameRoute state={state} composerText={composerText} setComposerText={setComposerText} composerNotice={composerNotice} submitComposer={submitComposer} advancedEnabled={advancedEnabled} />;
    case 'soul':
      return <SoulRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'world':
      return <WorldRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'journal':
      return <JournalRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'inventory':
      return <InventoryRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'media':
      return <MediaRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'settings':
      return <SettingsRoute state={state} advancedEnabled={advancedEnabled} />;
  }
}

function HomeRoute({
  state,
  advancedEnabled,
  onActiveRouteChange,
  onStateRefresh
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  advancedEnabled: boolean;
  onActiveRouteChange: (route: RouteId) => void;
  onStateRefresh: () => Promise<void>;
}) {
  if (!isSuccess(state.menu)) {
    return <EmptyOrFailure result={state.menu} advancedEnabled={advancedEnabled} errorTitle="Главное меню требует внимания" empty={{
      title: 'Книга ждёт открытия',
      message: 'Главная страница появится, когда локальная книга подготовит меню продолжения.',
      action: 'Откройте книгу: начните новую главу, продолжите сохранение или загрузите партию из доступных действий клиента.'
    }} />;
  }

  return (
    <ShellPanel title="Главная" eyebrow="игровое меню">
      <GameLauncher menu={state.menu.data} onActiveRouteChange={onActiveRouteChange} onStateRefresh={onStateRefresh} />
    </ShellPanel>
  );
}

function GameRoute({
  state,
  composerText,
  setComposerText,
  composerNotice,
  submitComposer,
  advancedEnabled
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  composerText: string;
  setComposerText: (value: string) => void;
  composerNotice: string;
  submitComposer: (event: FormEvent<HTMLFormElement>) => void;
  advancedEnabled: boolean;
}) {
  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Игровой экран требует внимания" empty={{
      title: 'Глава ещё не открыта',
      message: 'Нарратив и ход ГМа появятся после выбора или загрузки игровой сессии.',
      action: 'Вернитесь на главную страницу и откройте книгу, чтобы продолжить историю.'
    }} />;
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

function SoulRoute({ state, advancedEnabled }: { state: Extract<BrowserShellState, { status: 'ready' }>; advancedEnabled: boolean }) {
  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Данные души требуют внимания" empty={{
      title: 'Душа ещё не проявилась',
      message: 'Данные героя, души и слоя мира появятся после открытия главы.',
      action: 'Начните или загрузите игру, затем вернитесь к разделу души.'
    }} />;
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

function WorldRoute({ state, advancedEnabled }: { state: Extract<BrowserShellState, { status: 'ready' }>; advancedEnabled: boolean }) {
  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Мир требует внимания" empty={{
      title: 'Мир ждёт первой записи',
      message: 'Карта, журнал и фракции заполнятся из текущей главы после открытия книги.',
      action: 'Откройте или загрузите сессию, чтобы увидеть состояние мира.'
    }} />;
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


const journalSectionMatchers = ['quest', 'квест', 'journal', 'журнал', 'archive', 'архив', 'chronicle', 'хроника', 'story', 'история', 'faction', 'фракц', 'guardian', 'хранител'];
const inventorySectionMatchers = ['inventory', 'инвентар', 'item', 'предмет', 'craft', 'ремес', 'equip', 'экип', 'storage', 'хранилищ'];

function JournalRoute({ state, advancedEnabled }: { state: Extract<BrowserShellState, { status: 'ready' }>; advancedEnabled: boolean }) {
  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Журнал требует внимания" empty={{
      title: 'Журнал ждёт главу',
      message: 'Квесты, хроника и заметки появятся после открытия или загрузки игровой сессии.',
      action: 'Откройте книгу на главной странице, затем вернитесь в журнал.'
    }} />;
  }

  const game = state.game.data;
  const sections = filterActionSections(game.actionMenu, journalSectionMatchers);

  return (
    <ShellPanel title="Журнал" eyebrow="квесты, хроника и заметки">
      <div className="split-grid">
        <div className="summary-card">
          <h2>Текущая глава</h2>
          <p>{game.narrative.text || 'Последний нарратив пока не найден в локальной книге.'}</p>
        </div>
        <div className="summary-card">
          <h2>Ориентир игрока</h2>
          <p>{game.narrative.dialogueOptions.length > 0 ? `Доступно вариантов: ${game.narrative.dialogueOptions.length}.` : 'Варианты выбора появятся после ответа ГМа.'}</p>
          <p className="muted">Журнал показывает игровые разделы из каталога действий без служебных команд.</p>
        </div>
      </div>
      <FilteredActionSections sections={sections} emptyMessage="Квестовые, архивные и фракционные разделы появятся здесь, когда каталог действий отдаст их для текущей главы." />
    </ShellPanel>
  );
}

function InventoryRoute({ state, advancedEnabled }: { state: Extract<BrowserShellState, { status: 'ready' }>; advancedEnabled: boolean }) {
  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Инвентарь требует внимания" empty={{
      title: 'Инвентарь ждёт главу',
      message: 'Предметы, экипировка, ремесло и хранилища появятся после открытия или загрузки игровой сессии.',
      action: 'Откройте книгу на главной странице, затем вернитесь к инвентарю.'
    }} />;
  }

  const game = state.game.data;
  const sections = filterActionSections(game.actionMenu, inventorySectionMatchers);

  return (
    <ShellPanel title="Инвентарь" eyebrow="предметы, ремесло и хранилища">
      <div className="split-grid">
        <div className="summary-card">
          <h2>Герой</h2>
          <p>{game.player.name || 'Герой'} · {game.player.currentCondition}</p>
          <p className="muted">Здоровье {formatSidebarStatusMetric(game.player.healthPercentage)} · энергия {formatSidebarStatusMetric(game.player.energyPercentage)} · стойкость {formatSidebarStatusMetric(game.player.poisePercentage)}</p>
        </div>
        <div className="summary-card">
          <h2>Ремесло и предметы</h2>
          <p>Инвентарь использует существующие игровые действия и не добавляет отдельные правила предметов в React.</p>
        </div>
      </div>
      <FilteredActionSections sections={sections} emptyMessage="Инвентарные, ремесленные и складские разделы появятся здесь, когда каталог действий отдаст их для текущей главы." />
    </ShellPanel>
  );
}

function FilteredActionSections({ sections, emptyMessage }: { sections: BrowserPlayerCommandSectionDto[]; emptyMessage: string }) {
  if (sections.length === 0) {
    return <p className="muted">{emptyMessage}</p>;
  }

  return (
    <section className="action-menu" aria-label="Игровые разделы страницы">
      <div className="action-section-grid">
        {sections.map((section) => <ActionSection key={section.id} section={section} />)}
      </div>
    </section>
  );
}

function filterActionSections(menu: BrowserPlayerCommandMenuDto, matchers: string[]): BrowserPlayerCommandSectionDto[] {
  return menu.sections.flatMap((section) => {
    if (!section.playerDefault || section.actions.length === 0) {
      return [];
    }

    const matchingActions = section.actions.filter((action) => matchesActionSectionOrAction(section, action, matchers));
    if (matchingActions.length === 0) {
      return [];
    }

    return [{ ...section, actions: matchingActions }];
  });
}

function matchesActionSectionOrAction(
  section: BrowserPlayerCommandSectionDto,
  action: BrowserPlayerCommandActionDto,
  matchers: string[]
): boolean {
  const haystack = [
    section.id,
    section.label,
    section.description,
    action.id,
    action.label,
    action.description,
    action.formLabel,
    action.formPrompt,
    action.advancedCommand
  ].join(' ').toLocaleLowerCase('ru-RU');
  const normalizedMatchers = matchers.map((matcher) => matcher.toLocaleLowerCase('ru-RU'));

  return normalizedMatchers.some((matcher) => haystack.includes(matcher));
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

function playerLauncherAboutText(text: string): string {
  const fallback = 'Браузерный клиент открывает локальную книгу и оставляет игровые решения в основном клиенте.';
  const playerText = toPlayerFacingText(text, fallback);
  const sanitized = launcherAboutCopyReplacements.reduce(
    (copy, [pattern, replacement]) => copy.replace(pattern, replacement),
    playerText
  );

  return sanitized.trim() || fallback;
}

function toLauncherSaveFailureNotice(message: string): string {
  return message.trim()
    ? 'Сохранение не удалось загрузить. Выберите другую запись или попробуйте ещё раз; служебные подробности можно проверить в расширенном режиме.'
    : 'Сохранение не удалось загрузить. Выберите другую запись или попробуйте ещё раз.';
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

function MediaRoute({ state, advancedEnabled }: { state: Extract<BrowserShellState, { status: 'ready' }>; advancedEnabled: boolean }) {
  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Медиа требуют внимания" empty={{
      title: 'Медиа появятся вместе со сценой',
      message: 'Галерея и быстрые сцены станут доступны, когда активная глава предоставит игровые материалы.',
      action: 'Откройте книгу и продолжите историю; этот раздел заполнится по мере появления сцен.'
    }} />;
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
  state,
  advancedEnabled
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  advancedEnabled: boolean;
}) {
  if (!isSuccess(state.menu)) {
    return <EmptyOrFailure result={state.menu} advancedEnabled={advancedEnabled} errorTitle="Настройки требуют внимания" empty={{
      title: 'Настройки готовятся',
      message: 'Параметры локального клиента появятся, когда меню книги будет доступно.',
      action: 'Если вы только открыли клиент, подождите загрузки или вернитесь на главную страницу.'
    }} />;
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
  activeRoute,
  advancedEnabled
}: {
  result: BrowserApiResult<BrowserAudioSettingsDto>;
  activeRoute: RouteId;
  advancedEnabled: boolean;
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
    return <EmptyOrFailure result={audioResult} advancedEnabled={advancedEnabled} errorTitle="Музыка требует внимания" empty={{
      title: 'Музыка ждёт локальные настройки',
      message: 'Панель звука появится, когда клиент отдаст общие настройки аудио.',
      action: 'Игра продолжит работать без музыки; технические подробности остаются в расширенном режиме.'
    }} />;
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
        <p className="muted">{formatSidebarAudioSummary(audio)}</p>
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
  lifecycle,
  commandCoverage
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  lifecycle: BrowserLifecycleDashboardDto | null;
  commandCoverage: BrowserApiResult<BrowserCommandCoverageDto> | null;
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
      <CommandCoverageMatrix result={commandCoverage} />
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

function CommandCoverageMatrix({ result }: { result: BrowserApiResult<BrowserCommandCoverageDto> | null }) {
  if (!result) {
    return (
      <ShellPanel title="Покрытие команд" eyebrow="browser parity" nested>
        <p className="muted">Матрица команд загружается только после включения расширенного режима.</p>
      </ShellPanel>
    );
  }

  if (!isSuccess(result)) {
    return (
      <ShellPanel title="Покрытие команд" eyebrow="browser parity" nested>
        <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Матрица покрытия команд сейчас недоступна.')}</p>
      </ShellPanel>
    );
  }

  const coverage = result.data;
  return (
    <ShellPanel title="Покрытие команд Explorer" eyebrow={`schema ${coverage.schemaVersion}`} nested>
      <p>
        Дескрипторы: {coverage.summary.descriptorCount}; псевдонимы: {coverage.summary.aliasCount};
        подкоманды: {coverage.summary.subcommandCount}; browser-ready: {coverage.summary.browserExecutableCount}.
      </p>
      <ul className="endpoint-list command-coverage-list" aria-label="Матрица покрытия команд Explorer">
        {coverage.commands.map((command) => (
          <li key={command.id}>
            <strong>{command.primaryActionLabel} · {command.id}</strong>
            <span>{command.surface} · {command.uxDecision} · {command.browserStatus} · {command.formMode}</span>
            <span>{command.group} · {command.mutationMode} · {command.handlerKind}</span>
            <span>Команда: {command.primaryCommand}; aliases: {command.aliases.join(', ')}</span>
            {command.subcommands.length > 0 && (
              <ul className="endpoint-list command-subcoverage-list" aria-label={`Подкоманды ${command.id}`}>
                {command.subcommands.map((subcommand) => (
                  <li key={subcommand.id}>
                    <strong>{subcommand.primaryActionLabel} · {subcommand.id}</strong>
                    <span>{subcommand.surface} · {subcommand.uxDecision} · {subcommand.browserStatus} · {subcommand.formMode}</span>
                    <span>{subcommand.group} · {subcommand.mutationMode} · {subcommand.handlerKind}</span>
                    <span>Команда: {subcommand.canonicalCommand}; aliases: {subcommand.aliases.join(', ')}</span>
                    {(subcommand.followUpIssue || subcommand.reason) && (
                      <span>{subcommand.followUpIssue || 'follow-up не указан'} · {subcommand.reason || 'причина не указана'}</span>
                    )}
                  </li>
                ))}
              </ul>
            )}
            {(command.followUpIssue || command.reason) && (
              <span>{command.followUpIssue || 'follow-up не указан'} · {command.reason || 'причина не указана'}</span>
            )}
          </li>
        ))}
      </ul>
    </ShellPanel>
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

function EmptyState({ title, message, action }: EmptyStateCopy) {
  return (
    <section className="empty-state" aria-label={title}>
      <p className="panel-eyebrow">ожидание главы</p>
      <h2>{title}</h2>
      <p>{message}</p>
      <p className="muted">{action}</p>
    </section>
  );
}

function EmptyOrFailure<T>({
  result,
  empty,
  errorTitle,
  advancedEnabled
}: {
  result: BrowserApiResult<T>;
  empty: EmptyStateCopy;
  errorTitle: string;
  advancedEnabled: boolean;
}) {
  if (isSuccess(result)) {
    return null;
  }

  if (result.kind === 'no-active-session') {
    return <EmptyState {...empty} />;
  }

  return <ApiFailure title={errorTitle} result={result} advancedEnabled={advancedEnabled} />;
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
