import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties, FormEvent, ReactNode } from 'react';
import { browserApi, browserApiContractSummary } from './api/client';
import { DetailSurfaceCard } from './components/DetailSurface';
import { sanitizePlayerDefaultCommandResult } from './playerFacingCommandResult';
import type {
  BrowserApiFailure,
  BrowserApiResult,
  BrowserAudioAssetDto,
  BrowserAudioPlaylistDto,
  BrowserAudioSettingsDto,
  BrowserAudioSettingsUpdateRequest,
  BrowserClientSettingsDto,
  BrowserClientSettingsUpdateRequest,
  BrowserCommandCoverageDto,
  BrowserGameScreenAfterlifeDto,
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
type RouteIconId = 'book' | 'flame' | 'soul' | 'map' | 'journal' | 'satchel' | 'gallery' | 'settings';
type RouteAvailabilityState = 'active' | 'available' | 'locked' | 'loading' | 'attention';
type LauncherMode = 'continue' | 'load' | 'new-game' | 'settings' | 'about';

type BrowserShellState =
  | { status: 'loading' }
  | { status: 'ready'; menu: BrowserApiResult<BrowserMainMenuDto>; session: BrowserApiResult<LocalWebUiSessionStatus>; game: BrowserApiResult<BrowserGameScreenDto>; audio: BrowserApiResult<BrowserAudioSettingsDto>; settings: BrowserApiResult<BrowserClientSettingsDto>; lifecycle: BrowserApiResult<BrowserLifecycleDashboardDto> | null; commandCoverage: BrowserApiResult<BrowserCommandCoverageDto> | null }
  | { status: 'error'; playerMessage: string; technicalDetails?: string };

type PromptAnswers = Record<string, JsonValue | undefined>;

type QteGrade = 'success' | 'partial' | 'fail';
type QteAction = NonNullable<NonNullable<BrowserGameScreenDto['qte']['activeScene']>['currentChapter']>['actions'][number];

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
  { id: 'home', kind: 'primary', label: 'Главная', description: 'Сводка партии, продолжение, загрузка и безопасные действия.', icon: 'book' },
  { id: 'game', kind: 'primary', label: 'Игра', description: 'Текущая сцена, нарратив, ход ГМа и основной художественный ввод.', icon: 'flame' },
  { id: 'soul', kind: 'primary', label: 'Душа', description: 'Персонаж, душа, состояние героя и текущий слой мира.', icon: 'soul' },
  { id: 'world', kind: 'primary', label: 'Мир', description: 'Локация, карта, фракции и игровые действия окружения.', icon: 'map' },
  { id: 'journal', kind: 'primary', label: 'Журнал', description: 'Квесты, хроника, заметки, архив и история текущей главы.', icon: 'journal' },
  { id: 'inventory', kind: 'primary', label: 'Инвентарь', description: 'Предметы, экипировка, ремесло и локальные хранилища.', icon: 'satchel' },
  { id: 'media', kind: 'utility', label: 'Медиа', description: 'Галерея, быстрые сцены и игровые материалы.', icon: 'gallery' },
  { id: 'settings', kind: 'utility', label: 'Настройки', description: 'Профиль книги, звук, язык и удобство игры.', icon: 'settings' }
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
  [/debug shell/gi, 'скрытый раздел'],
  [/Slash-команды/gi, 'особые команды'],
  [/\bslash commands?\b/gi, 'особые команды'],
  [/Нужен repair pending turn/gi, 'Нужна починка ожидающего хода'],
  [/repair pending turn/gi, 'починка ожидающего хода'],
  [/нужен repair/gi, 'нужна починка'],
  [/\bpending[- ]turn\b/gi, 'ожидающий ход'],
  [/\bturn[- ]writer\b/gi, 'запись хода'],
  [/\bBrowser[- ]write\b/gi, 'запись книги'],
  [/\bbrowser write\b/gi, 'запись книги'],
  [/\blocal[- ]write\b/gi, 'запись книги'],
  [/\bprompt[- ]session\b/gi, 'игровая форма'],
  [/\brollback\b/gi, 'откат'],
  [/blocked by/gi, 'заблокировано из-за'],
  [/\bblocked\b/gi, 'заблокировано'],
  [/\bby\b/gi, 'из-за'],
  [/\bSpectre\.Console\b/g, 'текстовый интерфейс'],
  [/state\/contract/gi, 'файлы состояния'],
  [/snapshot artifact/gi, 'снимок состояния'],
  [/game_session/gi, 'сохранение игры'],
  [/write-flow/gi, 'запись хода'],
  [/manual_saves/gi, 'ручные сохранения'],
  [/autosaves/gi, 'автосохранения'],
  [/--web/g, 'режим книги'],
  [/\boffer\b/gi, 'предложение'],
  [/\bsnapshot\b/gi, 'снимок'],
  [/\bartifact\b/gi, 'файл состояния'],
  [/Browser Client/gi, 'игровой интерфейс'],
  [/sound-notification/gi, 'звуковая подсказка'],
  [/\brealm\b/gi, 'царство'],
  [/repair\/validation/gi, 'починка и проверка'],
  [/UI-блокировка/gi, 'блокировка интерфейса'],
  [/\bvalidation\b/gi, 'проверка'],
  [/game_state\/meta\/soul_state\.json/gi, 'файл души'],
  [/soul_state\.json/gi, 'файл души'],
  [/game_state/gi, 'папка состояния'],
  [/локальный запись хода/gi, 'запись хода книги'],
  [/тот же локальную/gi, 'ту же запись'],
  [/\bUI\b/g, 'интерфейс'],
  [/\baction\b/gi, 'действие'],
  [/\bresolved\b/gi, 'завершена'],
  [/\brepair\b/gi, 'починка'],
  [/C\x23\s*/g, ''],
  [/\blifecycle\b/gi, 'состояние хода'],
  [/\bruntime\b/gi, 'игровой слой'],
  [/\bendpoint(s)?\b/gi, 'разделы интерфейса'],
  [/\bAPI\b/g, 'игровой интерфейс'],
  [/\bDTO\b/g, 'игровые данные'],
  [/\bNPC\b/g, 'персонажи мира']
];

const launcherAboutCopyReplacements: Array<[RegExp, string]> = [
  [/\bdebug\b/gi, 'скрытая'],
  [/\bdiagnostics?\b/gi, 'проверочные сведения'],
  [/\btechnical details?\b/gi, 'скрытые сведения'],
  [/\btechnical\b/gi, 'скрытый'],
  [/\bdeveloper\b/gi, 'скрытый'],
  [/\braw JSON\b/gi, 'дополнительные данные']
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
    description: 'Открыть подготовку новой главы, когда книга разрешает этот шаг.'
  },
  settings: {
    label: 'Настройки книги',
    description: 'Настройки книги и звука.'
  },
  about: {
    label: 'Сведения о книге',
    description: 'Описание книги и интерфейса.'
  }
};

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
    if (isNoActiveSessionFailure(readyState.game)) {
      return false;
    }

    return routeNeedsGame(routeId);
  }

  return routeId === 'game' && (readyState.game.data.turnState.severity === 'error' || readyState.game.data.turnState.severity === 'repair');
}

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

      setLauncherNotice('Сохранение не удалось загрузить. Попробуйте ещё раз.');
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
        return <NewChapterStartPanel modeAction={modeAction} modeDescription={modeDescription} />;
      case 'settings':
        return (
          <section className="launcher-mode-panel" aria-label="Настройки книги">
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
      {/* Background art overlay (#759) */}
      <div className="launcher-art-bg" aria-hidden="true">
        <img src="/main-menu-bg.webp" alt="" loading="lazy" />
      </div>
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

function NewChapterStartPanel({
  modeAction,
  modeDescription
}: {
  modeAction: BrowserMainMenuDto['actions'][number] | undefined;
  modeDescription: string;
}) {
  const [notice, setNotice] = useState('');
  const [newChapterResult, setNewChapterResult] = useState<BrowserApiResult<ExplorerCommandResult> | null>(null);
  const [newChapterPromptAnswers, setNewChapterPromptAnswers] = useState<PromptAnswers>({});
  const [submissionMode, setSubmissionMode] = useState<'opening' | 'submitting' | null>(null);
  const isSubmitting = submissionMode !== null;
  const isNewChapterMountedRef = useRef(true);
  const startCommand = modeAction?.command.trim() ?? '';
  const canOpenStartFlow = Boolean(modeAction?.enabled && startCommand);
  const unavailableReason = !modeAction
    ? 'Подготовка новой главы пока недоступна. Продолжите текущую главу, загрузите сохранение или проверьте состояние книги.'
    : modeAction.enabled && !startCommand
      ? 'Подготовка новой главы пока не открыла поля ввода. Книга подготовит нужные данные, когда глава будет готова.'
      : launcherModeUnavailableReason(modeAction, modeDescription);

  useEffect(() => {
    return () => {
      isNewChapterMountedRef.current = false;
    };
  }, []);

  async function openNewChapterFlow() {
    if (!canOpenStartFlow) {
      setNotice(unavailableReason);
      return;
    }

    setSubmissionMode('opening');
    setNotice('Открываем форму новой главы…');
    const result = sanitizeNewChapterCommandResult(
      await browserApi.executeExplorerCommand({ command: startCommand, ownerLabel: 'Главная книга' })
    );

    if (!isNewChapterMountedRef.current) {
      return;
    }

    setNewChapterResult(result);
    if (isSuccess(result)) {
      setNewChapterPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
      setNotice(toNewChapterNotice(result.data));
    } else {
      setNotice(toPlayerFacingText(result.playerMessage, 'Подготовка новой главы сейчас недоступна.'));
    }
    setSubmissionMode(null);
  }

  async function submitNewChapterPromptAnswers(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!newChapterResult || !isSuccess(newChapterResult) || !newChapterResult.data.interactiveSession) {
      return;
    }

    setSubmissionMode('submitting');
    setNotice('Отправляем форму новой главы…');
    const session = newChapterResult.data.interactiveSession;
    const result = sanitizeNewChapterCommandResult(
      await browserApi.submitPromptSession({
        sessionId: session.sessionId,
        ownerId: session.ownerId,
        answers: newChapterPromptAnswers
      })
    );

    if (!isNewChapterMountedRef.current) {
      return;
    }

    setNewChapterResult(result);
    if (isSuccess(result)) {
      setNewChapterPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
      setNotice(toNewChapterNotice(result.data));
    } else {
      setNotice(toPlayerFacingText(result.playerMessage, 'Форма новой главы сейчас недоступна.'));
    }
    setSubmissionMode(null);
  }

  return (
    <section className="launcher-mode-panel launcher-new-chapter-flow" aria-label="Новая глава">
      <h3>Начать новую главу</h3>
      <p>{modeDescription}</p>
      <p className="muted">
        Форма новой главы открывается из существующего локального потока книги; браузер только показывает поля и отправляет ответы.
      </p>
      {!canOpenStartFlow && <p className="warning-text">{unavailableReason}</p>}
      <button type="button" className="launcher-secondary-action" disabled={!canOpenStartFlow || isSubmitting} onClick={() => void openNewChapterFlow()}>
        <strong>{submissionMode === 'opening' ? 'Открываем…' : submissionMode === 'submitting' ? 'Отправляем…' : 'Открыть форму новой главы'}</strong>
        <span>{canOpenStartFlow ? 'Показать поля подготовки мира и отправить ответы.' : 'Сейчас доступно только продолжение или загрузка.'}</span>
      </button>
      {notice && <p className="composer-notice">{notice}</p>}
      {newChapterResult && (
        <ActionCommandResult
          result={newChapterResult}
          promptAnswers={newChapterPromptAnswers}
          onPromptAnswerChange={(promptId, value) => setNewChapterPromptAnswers((current) => ({ ...current, [promptId]: value }))}
          onPromptSubmit={submitNewChapterPromptAnswers}
          isSubmitting={isSubmitting}
        />
      )}
    </section>
  );
}

function launcherModeUnavailableReason(modeAction: BrowserMainMenuDto['actions'][number], fallback: string): string {
  return toPlayerFacingText(modeAction.disabledReason || modeAction.description, fallback);
}

function toNewChapterNotice(result: ExplorerCommandResult): string {
  if (result.state === 'RequiresInput') {
    return 'Поля новой главы открыты. Заполните их ниже и отправьте.';
  }

  return toCommandNotice(result);
}

function sanitizeNewChapterCommandResult(result: BrowserApiResult<ExplorerCommandResult>): BrowserApiResult<ExplorerCommandResult> {
  return sanitizePlayerDefaultCommandResult(result, {
    blockedTextFallback: 'Подробности подготовки скрыты в обычном режиме.',
    blockTitleFallback: 'Сведения о новой главе',
    notificationTitleFallback: 'Форма новой главы',
    notificationMessageFallback: 'Форма новой главы готова к заполнению.',
    promptTextFallback: 'Заполните поле формы новой главы',
    failureMessageFallback: 'Форма новой главы сейчас недоступна.'
  });
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

type ComposerMode = 'prose' | 'actions';

type GameSurfaceState =
  | { kind: 'none' }
  | { kind: 'action-result'; actionId: string; result: BrowserApiResult<ExplorerCommandResult>; promptAnswers: PromptAnswers; isSubmitting: boolean };

export default function App() {
  const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });
  const [activeRoute, setActiveRoute] = useState<RouteId>('home');
  const [advancedEnabled, setAdvancedEnabled] = useState(false);
  const [composerText, setComposerText] = useState('');
  const [composerNotice, setComposerNotice] = useState('');
  const [composerMode, setComposerMode] = useState<ComposerMode>('prose');
  const [actionSearch, setActionSearch] = useState('');
  const [gameSurface, setGameSurface] = useState<GameSurfaceState>({ kind: 'none' });

  const loadBrowserState = useCallback(async () => {
    setShellState({ status: 'loading' });

    try {
      const [menu, session, game, audio, settings] = await Promise.all([
        browserApi.getMainMenu(),
        browserApi.getSessionStatus(),
        browserApi.getGameScreen(),
        browserApi.getAudioSettings(),
        browserApi.getClientSettings()
      ]);
      const [lifecycle, commandCoverage] = advancedEnabled ? await Promise.all([
        browserApi.getLifecycleDashboard(),
        browserApi.getCommandCoverage()
      ]) : [null, null];

      setShellState({ status: 'ready', menu, session, game, audio, settings, lifecycle, commandCoverage });
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Unknown browser shell error.';
      setShellState({
        status: 'error',
        playerMessage: 'Книга не смогла собрать состояние игры.',
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
  const clientSettings = readyState && isSuccess(readyState.settings) ? readyState.settings.data : null;
  const lifecycle = readyState && readyState.lifecycle && isSuccess(readyState.lifecycle) ? readyState.lifecycle.data : null;
  const commandCoverage = readyState ? readyState.commandCoverage : null;
  const realmTheme = useMemo(() => resolveRealmTheme(gameScreen), [gameScreen]);
  const browserShellClassName = [
    'browser-shell',
    clientSettings?.accessibility.reducedMotion ? 'is-reduced-motion' : '',
    clientSettings?.accessibility.contrastFriendly ? 'is-contrast-friendly' : ''
  ].filter(Boolean).join(' ');
  const browserShellStyle = {
    '--realm-accent': realmTheme.accent,
    '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`
  } as CSSProperties;
  const routeStates = useMemo(
    () => resolveRouteStates(playerRoutes, activeRoute, shellState, readyState),
    [activeRoute, shellState, readyState]
  );

  function submitComposer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = composerText.trim();

    if (normalized.startsWith('/')) {
      setComposerNotice('Особые команды не выполняются из основного поля. Откройте режим действий, чтобы выбрать команду из каталога.');
      return;
    }

    setComposerNotice('Художественный ввод подготовлен. Ход будет записан при следующем обновлении книги.');
  }

  function openActionSurface(actionId: string, result: BrowserApiResult<ExplorerCommandResult>) {
    const promptAnswers = isSuccess(result) ? buildDefaultPromptAnswers(result.data.prompts) : {};
    setGameSurface({ kind: 'action-result', actionId, result, promptAnswers, isSubmitting: false });
  }

  function closeActionSurface() {
    setGameSurface({ kind: 'none' });
  }

  async function executeAction(action: BrowserPlayerCommandActionDto) {
    if (!action.enabled) return;
    const rawResult = await browserApi.executeExplorerCommand({ command: action.advancedCommand, ownerLabel: 'Палитра действий' });
    const result = advancedEnabled ? rawResult : sanitizePlayerDefaultCommandResult(rawResult);
    openActionSurface(action.id, result);
  }

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

      <nav className="route-grid route-grid--primary" aria-label="Основные игровые разделы книги">
        {primaryPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, routeStates, setActiveRoute))}
      </nav>

      <nav className="route-grid route-grid--utility" aria-label="Дополнительные игровые разделы книги">
        <p className="utility-route-heading">Сводка / Игра / Душа / Мир / Журнал / Инвентарь — основная цепочка игрока. Медиа и настройки доступны отдельно.</p>
        {utilityPlayerRoutes.map((route) => renderRouteButton(route, activeRoute, routeStates, setActiveRoute))}
      </nav>

      <section className="workspace-grid" aria-live="polite">
        <div className="workspace-main">
          {shellState.status === 'loading' && <LoadingCard />}
          {shellState.status === 'error' && (
            <ErrorNotice title="Состояние клиента недоступно" failure={shellState} advancedEnabled={advancedEnabled} />
          )}
          {readyState && renderActiveRoute(activeRoute, readyState, composerText, setComposerText, composerNotice, submitComposer, advancedEnabled, setActiveRoute, loadBrowserState, composerMode, setComposerMode, actionSearch, setActionSearch, gameSurface, setGameSurface, closeActionSurface, executeAction)}
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


function renderRouteButton(
  route: RouteCard,
  activeRoute: RouteId,
  routeStates: Record<RouteId, RouteStateDetails>,
  setActiveRoute: (route: RouteId) => void
): ReactNode {
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
          <p className="muted">{formatSidebarLayerStatus(menu)}</p>
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

      <StatusSummaryCard title={getTurnSidebarTitle(hasGame, sidebarGameFailure, gameScreen?.turnState?.phase ?? null)} eyebrow="ход" attention={turnNeedsAttention}>
        {sidebarGameFailure ? (
          <>
            <p className="warning-text">{sidebarGameFailure}</p>
            <p className="muted">Глава сохранена; подробности ремонта и проверки остаются в расширенном режиме.</p>
          </>
        ) : gameScreen ? (
          <>
            <p className={`status-pill turn-phase turn-phase--${gameScreen.turnState.severity}`}>{formatTurnStateTitle(gameScreen.turnState)}</p>
            <p className="muted">{toPlayerFacingText(gameScreen.turnState.playerGuidance, 'Следуйте текущему состоянию хода.')}</p>
          </>
        ) : (
          <>
            <p>{sidebarEmptyGame}</p>
            <p className="muted">Когда появится ожидающий ход или ответ ГМа, книга покажет это здесь игровым языком.</p>
          </>
        )}
      </StatusSummaryCard>

      {readyState && <AudioSettingsPanel result={readyState.audio} activeRoute={activeRoute} advancedEnabled={advancedEnabled} />}

      <section className="advanced-sidebar-entry" aria-label="Дополнительная панель">
        <div>
          <p className="panel-eyebrow">по запросу</p>
          <h3>Дополнительная панель</h3>
          <p className="muted">Дополнительные проверки и сведения остаются вторичным режимом.</p>
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

function formatHeroStatusLabel(gameScreen: BrowserGameScreenDto | null, menu: BrowserMainMenuDto | null): string {
  if (gameScreen) {
    return formatTurnStateTitle(gameScreen.turnState);
  }

  if (menu && !menu.session.canContinue) {
    return 'Глава ещё не открыта';
  }

  return 'Книга ждёт открытия';
}

function formatSidebarLayerStatus(menu: BrowserMainMenuDto | null): string {
  if (!menu) {
    return 'Книга ждёт открытия.';
  }

  if (!menu.session.canContinue) {
    return 'Откройте новую главу или загрузите сохранение, чтобы увидеть состояние мира.';
  }

  const validationLabel = menu.session.validationLabel;
  return toPlayerFacingText(validationLabel, 'Книга ждёт открытия');
}

function getTurnSidebarTitle(hasGame: boolean, sidebarGameFailure: string | null, turnPhase: string | null): string {
  if (!hasGame && !sidebarGameFailure) {
    return 'Ход ещё не начат';
  }

  // Distinguish GM-waiting from repair/error/validation states (issue #743)
  switch (turnPhase) {
    case 'repair-required': return 'Нужна починка';
    case 'error-restored': return 'Ошибка хода';
    case 'validation-failed': return 'Проверка состояния';
    case 'ready': return 'Ответ ГМа готов';
    case 'waiting-gm': return 'Ожидание ГМа';
    case 'idle': return 'Ваш ход';
    case 'composing-action': return 'Подготовка действия';
    case 'turn-submitted': return 'Ход отправляется';
    case 'accepted': return 'Ответ принят';
    case 'cancelled': return 'Ход отменён';
    default: return 'Состояние хода';
  }
}

function formatSidebarSessionSummary(session: LocalWebUiSessionStatus | null, menu: BrowserMainMenuDto | null): string {
  if (menu && !menu.session.canContinue && !menu.session.hasReadableSoul) {
    return 'Активной главы пока нет — начните новую или загрузите сохранение.';
  }

  if (session?.gameSessionExists) {
    return session.canStartBrowserWrite
      ? 'Локальная партия найдена, запись следующего хода доступна.'
      : 'Партия найдена, но ход сейчас ждёт подходящего момента.';
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
  loadBrowserState: () => Promise<void>,
  composerMode: ComposerMode,
  setComposerMode: (mode: ComposerMode) => void,
  actionSearch: string,
  setActionSearch: (value: string) => void,
  gameSurface: GameSurfaceState,
  setGameSurface: (surface: GameSurfaceState) => void,
  closeActionSurface: () => void,
  executeAction: (action: BrowserPlayerCommandActionDto) => void
) {
  switch (activeRoute) {
    case 'home':
      return <HomeRoute state={state} advancedEnabled={advancedEnabled} onActiveRouteChange={setActiveRoute} onStateRefresh={loadBrowserState} />;
    case 'game':
      return <GameRoute state={state} composerText={composerText} setComposerText={setComposerText} composerNotice={composerNotice} submitComposer={submitComposer} advancedEnabled={advancedEnabled} composerMode={composerMode} setComposerMode={setComposerMode} actionSearch={actionSearch} setActionSearch={setActionSearch} gameSurface={gameSurface} setGameSurface={setGameSurface} closeActionSurface={closeActionSurface} executeAction={executeAction} />;
    case 'soul':
      return <SoulRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'world':
      return <WorldRoute state={state} advancedEnabled={advancedEnabled} executeAction={executeAction} />;
    case 'journal':
      return <JournalRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'inventory':
      return <InventoryRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'media':
      return <MediaRoute state={state} advancedEnabled={advancedEnabled} />;
    case 'settings':
      return <SettingsRoute state={state} advancedEnabled={advancedEnabled} onStateRefresh={loadBrowserState} />;
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
      action: 'Откройте книгу: начните новую главу, продолжите сохранение или загрузите партию.'
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
  advancedEnabled,
  composerMode,
  setComposerMode,
  actionSearch,
  setActionSearch,
  gameSurface,
  setGameSurface,
  closeActionSurface,
  executeAction
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  composerText: string;
  setComposerText: (value: string) => void;
  composerNotice: string;
  submitComposer: (event: FormEvent<HTMLFormElement>) => void;
  advancedEnabled: boolean;
  composerMode: ComposerMode;
  setComposerMode: (mode: ComposerMode) => void;
  actionSearch: string;
  setActionSearch: (value: string) => void;
  gameSurface: GameSurfaceState;
  setGameSurface: (surface: GameSurfaceState) => void;
  closeActionSurface: () => void;
  executeAction: (action: BrowserPlayerCommandActionDto) => void;
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
          <p className="muted">{toPlayerFacingText(game.turnState.playerGuidance, 'Следуйте текущему состоянию хода.')}</p>
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

      {/* Central composer with mode toggle (#755) */}
      <div className="composer">
        <div className="composer-toggle-group">
          <button
            type="button"
            className={`composer-mode-toggle${composerMode === 'prose' ? ' is-active' : ''}`}
            onClick={() => setComposerMode('prose')}
            aria-pressed={composerMode === 'prose'}
          >
            Написать действие
          </button>
          <button
            type="button"
            className={`composer-mode-toggle${composerMode === 'actions' ? ' is-active' : ''}`}
            onClick={() => setComposerMode('actions')}
            aria-pressed={composerMode === 'actions'}
          >
            Каталог действий
          </button>
        </div>

        {composerMode === 'prose' ? (
          <form onSubmit={submitComposer}>
            <label htmlFor="player-action">Что ты хочешь сделать?</label>
            <textarea
              id="player-action"
              name="player-action"
              rows={3}
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
        ) : (
          <ActionPalette
            menu={game.actionMenu}
            search={actionSearch}
            onSearchChange={setActionSearch}
            onActionSelect={executeAction}
            advancedEnabled={advancedEnabled}
          />
        )}
      </div>

      {/* Polished game surface for action results (#757) */}
      {gameSurface.kind === 'action-result' && (
        <GameSurfacePanel
          surface={gameSurface}
          setSurface={setGameSurface}
          onClose={closeActionSurface}
          advancedEnabled={advancedEnabled}
        />
      )}

      {advancedEnabled && (
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
      )}
      {!advancedEnabled && (
        <p className="muted">Текущий этап: {toPlayerFacingText(game.turnState.phaseLabel, 'Неизвестно')}</p>
      )}
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
      <div className="detail-surface-grid">
        <DetailSurfaceCard
          detailSurfaceId="soul-identity"
          eyebrow="душа и царство"
          title="Душа"
          icon="🕯️"
          summary={`${soul.name || 'Безымянная душа'} · ${formatRealmName(soul.realm)}`}
          status={`Перья ${soul.inkFeathers}`}
          detailsTitle="Детали души"
          detailsIntro={<p>Эта панель показывает только текущую игровую сводку души из локальной книги.</p>}
          sections={[
            {
              title: 'Проявление',
              eyebrow: 'имя и слой',
              icon: '✦',
              content: (
                <dl className="kv-list">
                  <div><dt>Имя</dt><dd>{soul.name || 'без имени'}</dd></div>
                  <div><dt>Царство</dt><dd>{formatRealmName(soul.realm)}</dd></div>
                  <div><dt>Инкарнация</dt><dd>{soul.incarnation}</dd></div>
                </dl>
              )
            },
            {
              title: 'Посмертный прогресс',
              eyebrow: 'ресурсы души',
              icon: '✨',
              content: (
                <dl className="kv-list">
                  <div><dt>Чернильные перья</dt><dd>{soul.inkFeathers}</dd></div>
                  <div><dt>Просветление</dt><dd>{soul.enlightenmentTier || 'нет данных'}</dd></div>
                  <div><dt>Хранитель</dt><dd>{soul.activeGuardianName || 'не назначен'}</dd></div>
                </dl>
              )
            }
          ]}
        />
        <DetailSurfaceCard
          detailSurfaceId="player-condition"
          eyebrow="герой"
          title="Герой"
          icon="⚔️"
          summary={`${player.name || 'Герой'} · ${player.currentCondition}`}
          status={`${formatSidebarStatusMetric(player.healthPercentage)} здоровья`}
          detailsTitle="Детали героя"
          detailsIntro={<p>Карточка героя раскрывает состояние персонажа без служебных команд и внутренних файлов.</p>}
          sections={[
            {
              title: 'Личность',
              eyebrow: 'персонаж',
              icon: '☉',
              content: (
                <dl className="kv-list">
                  <div><dt>Имя</dt><dd>{player.name || 'Герой'}</dd></div>
                  <div><dt>Раса</dt><dd>{player.race || 'не указана'}</dd></div>
                  <div><dt>Класс</dt><dd>{player.class || 'не указан'}</dd></div>
                </dl>
              )
            },
            {
              title: 'Состояние',
              eyebrow: 'виталы',
              icon: '♡',
              content: (
                <>
                  <p>{player.currentCondition || 'Состояние уточняется.'}</p>
                  <StatusBar label="Здоровье" value={player.healthPercentage} />
                  <StatusBar label="Энергия" value={player.energyPercentage} />
                  <StatusBar label="Стойкость" value={player.poisePercentage} />
                </>
              )
            }
          ]}
        />
      </div>
    </ShellPanel>
  );
}

function WorldRoute({ state, advancedEnabled, executeAction }: { state: Extract<BrowserShellState, { status: 'ready' }>; advancedEnabled: boolean; executeAction: (action: BrowserPlayerCommandActionDto) => void }) {
  const [catalogOpen, setCatalogOpen] = useState(false);

  if (!isSuccess(state.game)) {
    return <EmptyOrFailure result={state.game} advancedEnabled={advancedEnabled} errorTitle="Мир требует внимания" empty={{
      title: 'Мир ждёт первой записи',
      message: 'Карта, журнал и фракции заполнятся из текущей главы после открытия книги.',
      action: 'Откройте или загрузите сессию, чтобы увидеть состояние мира.'
    }} />;
  }

  const game = state.game.data;
  const allSections = game.actionMenu.sections.filter((section) => section.playerDefault && section.actions.length > 0);
  const allActions = allSections.flatMap((section) => section.actions);
  const contextActions = allActions.filter((action) => action.enabled).slice(0, 5);

  return (
    <ShellPanel title="Мир" eyebrow="карта, журнал и действия">
      <div className="split-grid three">
        <DetailSurfaceCard
          detailSurfaceId="world-location"
          eyebrow="мир и место"
          title="Локация"
          icon="🗺️"
          summary={`${game.world.location || 'Локация уточняется'} · ${game.world.worldTime || 'время уточняется'}`}
          status={`Ход ${game.world.turnNumber}`}
          detailsTitle="Детали локации"
          detailsIntro={<p>Локация раскрывает текущий слой мира без технических путей и служебных журналов.</p>}
          sections={[
            {
              title: 'Текущая сцена',
              eyebrow: 'место и время',
              icon: '⌖',
              content: (
                <dl className="kv-list">
                  <div><dt>Локация</dt><dd>{game.world.location || 'локация уточняется'}</dd></div>
                  <div><dt>Время</dt><dd>{game.world.worldTime || 'время уточняется'}</dd></div>
                  <div><dt>Царство</dt><dd>{game.theme.label}</dd></div>
                </dl>
              )
            },
            {
              title: 'Ориентир главы',
              eyebrow: 'ход и запись',
              icon: '✍️',
              content: (
                <dl className="kv-list">
                  <div><dt>Номер хода</dt><dd>{game.world.turnNumber}</dd></div>
                  <div><dt>Состояние</dt><dd>{formatTurnStateTitle(game.turnState)}</dd></div>
                  <div><dt>Ввод игрока</dt><dd>{game.actionComposer.canSubmit ? 'доступен' : getComposerDisabledReason(game.actionComposer)}</dd></div>
                </dl>
              )
            }
          ]}
        />
        <div className="summary-card"><h2>Журнал</h2><p>Квесты, архив и история разворачиваются в игровых разделах без знания ручных команд.</p></div>
        <div className="summary-card"><h2>Фракции</h2><p>Панели фракций и стражей используют общие игровые данные и не дублируют правила.</p></div>
      </div>

      {/* Context shortlist — immediate actions for current scene (#744) */}
      {contextActions.length > 0 && (
        <section className="world-context-shortlist" aria-label="Доступные действия">
          <p className="panel-eyebrow">сейчас доступно</p>
          <h3>Действия текущей сцены</h3>
          <div className="shortlist-actions">
            {contextActions.map((action) => (
              <button
                key={action.id}
                type="button"
                className="shortlist-action"
                disabled={!action.enabled}
                onClick={() => executeAction(action)}
              >
                <strong>{toPlayerFacingText(action.label, 'Игровое действие')}</strong>
                <small>{toPlayerFacingText(action.description, 'Действие доступно для текущей главы.')}</small>
              </button>
            ))}
          </div>
        </section>
      )}

      <RebornSystemsPanel game={game} />

      {/* Collapsible full catalog (#744) */}
      <button
        type="button"
        className={`action-catalog-toggle${catalogOpen ? ' is-open' : ''}`}
        onClick={() => setCatalogOpen((prev) => !prev)}
        aria-expanded={catalogOpen}
      >
        <span className="toggle-arrow">&#9654;</span>
        <span>Полный каталог действий{allActions.length > 0 ? ` (${allActions.length})` : ''}</span>
      </button>
      <div className={`action-catalog-body${catalogOpen ? ' is-open' : ''}`}>
        <ActionMenu menu={game.actionMenu} advancedEnabled={advancedEnabled} />
      </div>
    </ShellPanel>
  );
}


const journalSectionMatchers = ['quest', 'квест', 'journal', 'журнал', 'archive', 'архив', 'chronicle', 'хроника', 'story', 'история', 'faction', 'фракц', 'guardian', 'хранител'];
const inventorySectionMatchers = ['inventory', 'инвентар', 'item', 'предмет', 'craft', 'ремес', 'equip', 'экип', 'storage', 'хранилищ'];
const rebornSectionMatchers = ['afterlife', 'посмер', 'soul', 'душ', 'shining', 'сияющ', 'abode', 'обител', 'chaos', 'хаос', 'guardian', 'хранител', 'gate', 'врат'];
const shiningAbodeActionMatchers = ['shining', 'сияющ', 'abode', 'обител', 'radiance', 'сияни', 'spark', 'искра', 'hall', 'зал', 'gate', 'врат'];
const chaosSeaActionMatchers = ['chaos', 'хаос', 'sea', 'море', 'guardian', 'хранител', 'abode', 'обител'];

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
      <FilteredActionSections sections={sections} emptyMessage="Квестовые, архивные и фракционные разделы появятся здесь, когда каталог действий отдаст их для текущей главы." advancedEnabled={advancedEnabled} />
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
      <FilteredActionSections sections={sections} emptyMessage="Инвентарные, ремесленные и складские разделы появятся здесь, когда каталог действий отдаст их для текущей главы." advancedEnabled={advancedEnabled} />
    </ShellPanel>
  );
}

function RebornSystemsPanel({ game }: { game: BrowserGameScreenDto }) {
  // UI-only mapping for #729: React renders existing C# game-screen state and action metadata without changing afterlife contracts.
  const rebornSections = filterActionSections(game.actionMenu, rebornSectionMatchers);
  const afterlifeActions = filterActionsForPanel(rebornSections, rebornSectionMatchers);
  const shiningActions = filterActionsForPanel(rebornSections, shiningAbodeActionMatchers);
  const chaosActions = filterActionsForPanel(rebornSections, chaosSeaActionMatchers);
  const isAfterlifeActive = game.flags.isInAfterlifeRealm;
  const isShiningAvailable = game.flags.isInShiningAbode || game.flags.isInAnyShiningAbodeState || game.flags.canReenterShiningAbode;
  const isChaosSeaActive = game.flags.isInChaosSea;

  return (
    <section className="reborn-systems-panel" aria-labelledby="reborn-systems-title">
      <div className="reborn-systems-panel__header">
        <p className="panel-eyebrow">посмертные системы</p>
        <h2 id="reborn-systems-title">Посмертие Reborn</h2>
        <p className="muted">
          Afterlife, Сияющая Обитель и Море Хаоса отделены от смертного мира, но используют тот же
          язык карточек и только безопасные игровые данные текущей книги.
        </p>
      </div>
      <div className="detail-surface-grid">
        <DetailSurfaceCard
          detailSurfaceId="reborn-afterlife-overview"
          eyebrow="душа после смерти"
          title="Посмертие Reborn"
          icon="🕯️"
          summary={isAfterlifeActive ? `${formatRealmName(game.soul.realm)} · ${game.soul.name || 'душа без имени'}` : 'Смертный слой активен'}
          status={formatRebornLockStatus(game)}
          detailsTitle="Детали посмертия"
          detailsIntro={<p>Эта панель показывает, открыт ли посмертный слой, и какие ресурсы души уже можно читать игроку.</p>}
          sections={[
            {
              title: 'Состояние слоя',
              eyebrow: 'доступность',
              icon: '✦',
              content: (
                <dl className="kv-list">
                  <div><dt>Текущее царство</dt><dd>{formatRealmName(game.soul.realm)}</dd></div>
                  <div><dt>Посмертие</dt><dd>{isAfterlifeActive ? 'открыто' : 'ещё закрыто'}</dd></div>
                  <div><dt>Инкарнация</dt><dd>{game.soul.incarnation}</dd></div>
                </dl>
              )
            },
            {
              title: 'Ресурсы души',
              eyebrow: 'прогресс',
              icon: '✨',
              content: (
                <dl className="kv-list">
                  <div><dt>Чернильные перья</dt><dd>{game.soul.inkFeathers}</dd></div>
                  <div><dt>Просветление</dt><dd>{game.soul.enlightenmentTier || 'нет данных'}</dd></div>
                  <div><dt>Хранитель</dt><dd>{game.soul.activeGuardianName || 'не назначен'}</dd></div>
                </dl>
              )
            },
            {
              title: 'Доступные действия',
              eyebrow: 'каталог игрока',
              icon: '☉',
              content: <ActionPreviewList actions={afterlifeActions} emptyMessage="Посмертные действия появятся здесь, когда текущая глава отдаст их как безопасные для игрока." />
            }
          ]}
        />
        <DetailSurfaceCard
          detailSurfaceId="reborn-shining-abode"
          eyebrow="свет и обитель"
          title="Сияющая Обитель"
          icon="✦"
          summary={isShiningAvailable ? 'Светлая область доступна для этой души' : 'Обитель пока закрыта'}
          status={formatShiningGateStatus(game.afterlife)}
          detailsTitle="Детали Сияющей Обители"
          detailsIntro={<p>Сводка Обители остаётся игрокоориентированной: сияние, искры, залы и действия без внутренних файлов.</p>}
          sections={[
            {
              title: 'Сияние',
              eyebrow: 'ресурсы обители',
              icon: '✧',
              content: (
                <dl className="kv-list">
                  <div><dt>Опыт сияния</dt><dd>{game.afterlife.shiningRadianceExperience}</dd></div>
                  <div><dt>Ранг сияния</dt><dd>{game.afterlife.shiningRadianceTier}</dd></div>
                  <div><dt>Искры света</dt><dd>{game.afterlife.shiningLightSparks}</dd></div>
                </dl>
              )
            },
            {
              title: 'Обитель',
              eyebrow: 'структура',
              icon: '🏛️',
              content: (
                <dl className="kv-list">
                  <div><dt>Залы</dt><dd>{game.afterlife.shiningHallCount}</dd></div>
                  <div><dt>Фракции</dt><dd>{game.afterlife.shiningFactionCount}</dd></div>
                  <div><dt>Врата</dt><dd>{formatShiningGateStatus(game.afterlife)}</dd></div>
                </dl>
              )
            },
            {
              title: 'Действия Обители',
              eyebrow: 'безопасные формы',
              icon: '☼',
              content: <ActionPreviewList actions={shiningActions} emptyMessage="Действия Сияющей Обители появятся после открытия соответствующего слоя или формы." />
            }
          ]}
        />
        <DetailSurfaceCard
          detailSurfaceId="reborn-chaos-sea"
          eyebrow="хаос и навигация"
          title="Море Хаоса"
          icon="🌊"
          summary={isChaosSeaActive ? 'Душа находится в Море Хаоса' : 'Навигация Моря Хаоса пока закрыта'}
          status={isChaosSeaActive ? 'Море Хаоса активно' : 'Ожидается подходящее царство'}
          detailsTitle="Детали Моря Хаоса"
          detailsIntro={<p>Панель Моря Хаоса показывает статус навигации и player-safe действия, когда каталог их отдаёт.</p>}
          sections={[
            {
              title: 'Навигация',
              eyebrow: 'статус',
              icon: '⌁',
              content: (
                <dl className="kv-list">
                  <div><dt>Царство</dt><dd>{formatRealmName(game.soul.realm)}</dd></div>
                  <div><dt>Море Хаоса</dt><dd>{isChaosSeaActive ? 'открыто' : 'закрыто'}</dd></div>
                  <div><dt>Хранитель</dt><dd>{game.soul.activeGuardianName || 'ожидает выбора'}</dd></div>
                </dl>
              )
            },
            {
              title: 'Ориентиры',
              eyebrow: 'для игрока',
              icon: '🜁',
              content: (
                <p>{isAfterlifeActive ? 'Посмертный слой активен; действия моря появятся ниже, если они подходят текущему царству.' : 'Посмертные панели откроются, когда душа перейдёт в посмертие.'}</p>
              )
            },
            {
              title: 'Действия Моря',
              eyebrow: 'каталог игрока',
              icon: '☽',
              content: <ActionPreviewList actions={chaosActions} emptyMessage="Действия Моря Хаоса появятся здесь, когда они станут доступны в текущей главе." />
            }
          ]}
        />
      </div>
    </section>
  );
}

function filterActionsForPanel(
  sections: BrowserPlayerCommandSectionDto[],
  matchers: string[]
): BrowserPlayerCommandActionDto[] {
  const normalizedMatchers = matchers.map((matcher) => matcher.toLocaleLowerCase('ru-RU'));
  return sections
    .flatMap((section) => section.actions)
    .filter((action) => {
      const haystack = [
        action.id,
        action.label,
        action.description,
        action.formLabel,
        action.formPrompt
      ].join(' ').toLocaleLowerCase('ru-RU');
      return normalizedMatchers.some((matcher) => haystack.includes(matcher));
    })
    .slice(0, 4);
}

function formatRebornLockStatus(game: BrowserGameScreenDto): string {
  if (game.flags.isInAfterlifeRealm) {
    return `${formatRealmName(game.soul.realm)} · перья ${game.soul.inkFeathers}`;
  }

  return 'Посмертные панели откроются, когда душа перейдёт в посмертие.';
}

function formatShiningGateStatus(afterlife: BrowserGameScreenAfterlifeDto): string {
  if (afterlife.isShiningGatesDraftStale) {
    return 'Черновик врат требует обновления';
  }

  if (afterlife.hasOpenShiningGatesDraft) {
    return 'Черновик врат открыт';
  }

  return 'Врата ждут подходящего момента';
}

function formatActionPreview(action: BrowserPlayerCommandActionDto): string {
  if (action.enabled) {
    return toPlayerFacingText(action.description, 'Действие доступно для текущей главы.');
  }

  return toPlayerFacingText(action.disabledReason, 'Действие сейчас недоступно.');
}

function ActionPreviewList({ actions, emptyMessage }: { actions: BrowserPlayerCommandActionDto[]; emptyMessage: string }) {
  if (actions.length === 0) {
    return <p className="muted">{emptyMessage}</p>;
  }

  return (
    <div className="reborn-systems-panel__actions">
      <ul>
        {actions.map((action) => (
          <li key={action.id}>
            <strong>{toPlayerFacingText(action.label, 'Игровое действие')}</strong>
            <span> — {formatActionPreview(action)}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function FilteredActionSections({ sections, emptyMessage, advancedEnabled }: { sections: BrowserPlayerCommandSectionDto[]; emptyMessage: string; advancedEnabled: boolean }) {
  if (sections.length === 0) {
    return <p className="muted">{emptyMessage}</p>;
  }

  return (
    <section className="action-menu" aria-label="Игровые разделы страницы">
      <div className="action-section-grid">
        {sections.map((section) => <ActionSection key={section.id} section={section} advancedEnabled={advancedEnabled} />)}
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

/* ═══════════════════════════════════════════════════════════════════
   ACTION PALETTE — Searchable player-facing command mode (#756)
   ═══════════════════════════════════════════════════════════════════ */

const actionCategoryMatchers: Array<{ key: string; label: string; matchers: string[] }> = [
  { key: 'travel', label: 'Путешествие / Карта', matchers: ['travel', 'путеш', 'map', 'карт', 'location', 'локац', 'move', 'переход', 'region', 'област'] },
  { key: 'character', label: 'Персонаж / Душа', matchers: ['character', 'персон', 'soul', 'душ', 'health', 'здоров', 'status', 'сост', 'condition', 'состояни'] },
  { key: 'inventory', label: 'Инвентарь / Предметы', matchers: ['inventory', 'инвент', 'item', 'предм', 'craft', 'ремес', 'equip', 'экип', 'storage', 'хранил'] },
  { key: 'journal', label: 'Журнал / Задания', matchers: ['quest', 'квест', 'journal', 'журн', 'archive', 'архив', 'chronicle', 'хроник', 'story', 'истор'] },
  { key: 'faction', label: 'Фракции / Отношения', matchers: ['faction', 'фракц', 'guardian', 'хранител', 'reputation', 'репутац', 'relation', 'отношен'] },
  { key: 'afterlife', label: 'Посмертие / Обитель', matchers: ['afterlife', 'посмерт', 'shining', 'сияющ', 'abode', 'обител', 'chaos', 'хаос', 'radiance', 'сиян'] },
  { key: 'combat', label: 'Бой / Действия', matchers: ['combat', 'бой', 'fight', 'сраж', 'attack', 'атак', 'defend', 'защит', 'action', 'действ'] },
  { key: 'save', label: 'Сохранение / Настройки', matchers: ['save', 'сохран', 'load', 'загруз', 'setting', 'настр', 'option', 'параметр'] }
];

function ActionPalette({
  menu,
  search,
  onSearchChange,
  onActionSelect,
  advancedEnabled
}: {
  menu: BrowserPlayerCommandMenuDto;
  search: string;
  onSearchChange: (value: string) => void;
  onActionSelect: (action: BrowserPlayerCommandActionDto) => void;
  advancedEnabled: boolean;
}) {
  const allSections = menu.sections.filter((section) => section.playerDefault && section.actions.length > 0);
  const allActions = allSections.flatMap((section) => section.actions);
  const playerActions = allActions.filter((action) => action.playerDefault || advancedEnabled);

  const normalizedSearch = search.trim().toLocaleLowerCase('ru-RU');
  const filteredActions = normalizedSearch
    ? playerActions.filter((action) => {
        const haystack = [action.id, action.label, action.description, action.formLabel, action.formPrompt].join(' ').toLocaleLowerCase('ru-RU');
        return haystack.includes(normalizedSearch);
      })
    : playerActions;

  const categorized = actionCategoryMatchers.map((category) => {
    const categoryActions = filteredActions.filter((action) => {
      const haystack = [action.id, action.label, action.description, action.sectionId, action.formLabel].join(' ').toLocaleLowerCase('ru-RU');
      return category.matchers.some((matcher) => haystack.includes(matcher));
    });
    return { ...category, actions: categoryActions };
  }).filter((category) => category.actions.length > 0);

  const uncategorized = filteredActions.filter((action) => {
    const haystack = [action.id, action.label, action.description, action.sectionId, action.formLabel].join(' ').toLocaleLowerCase('ru-RU');
    return !actionCategoryMatchers.some((category) =>
      category.matchers.some((matcher) => haystack.includes(matcher))
    );
  });

  return (
    <section className="action-palette" aria-label="Каталог действий">
      <div className="action-palette-header">
        <p className="panel-eyebrow">каталог действий</p>
        <h3>Выберите действие</h3>
        <p className="muted">Найдите нужное действие по названию или выберите из категорий.</p>
      </div>
      <input
        type="search"
        className="action-palette-search"
        placeholder="Поиск действий…"
        value={search}
        onChange={(event) => onSearchChange(event.currentTarget.value)}
        aria-label="Поиск действий"
      />
      <p className="action-palette-count">Найдено: {filteredActions.length} из {playerActions.length}</p>

      {categorized.length === 0 && uncategorized.length === 0 && (
        <p className="action-palette-empty">Действий не найдено. Попробуйте другой запрос или подождите, пока книга подготовит каталог.</p>
      )}

      {categorized.map((category) => (
        <div key={category.key} className="action-palette-category">
          <p className="action-palette-category-title">{category.label}</p>
          <ul className="action-palette-list">
            {category.actions.map((action) => (
              <li key={action.id}>
                <button
                  type="button"
                  className={`action-palette-item${!action.enabled ? ' is-disabled' : ''}`}
                  disabled={!action.enabled}
                  onClick={() => onActionSelect(action)}
                >
                  <span><strong>{toPlayerFacingText(action.label, 'Игровое действие')}</strong></span>
                  <small>{toPlayerFacingText(action.realmAvailability, '')}</small>
                </button>
              </li>
            ))}
          </ul>
        </div>
      ))}

      {uncategorized.length > 0 && (
        <div className="action-palette-category">
          <p className="action-palette-category-title">Другие действия</p>
          <ul className="action-palette-list">
            {uncategorized.map((action) => (
              <li key={action.id}>
                <button
                  type="button"
                  className={`action-palette-item${!action.enabled ? ' is-disabled' : ''}`}
                  disabled={!action.enabled}
                  onClick={() => onActionSelect(action)}
                >
                  <span><strong>{toPlayerFacingText(action.label, 'Игровое действие')}</strong></span>
                  <small>{toPlayerFacingText(action.realmAvailability, '')}</small>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

/* ═══════════════════════════════════════════════════════════════════
   GAME SURFACE — Polished action result panel (#757)
   ═══════════════════════════════════════════════════════════════════ */

function GameSurfacePanel({
  surface,
  setSurface,
  onClose,
  advancedEnabled
}: {
  surface: Extract<GameSurfaceState, { kind: 'action-result' }>;
  setSurface: (surface: GameSurfaceState) => void;
  onClose: () => void;
  advancedEnabled: boolean;
}) {
  const { result, promptAnswers, isSubmitting } = surface;
  const surfaceRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    surfaceRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }, []);

  async function submitPromptAnswers(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!isSuccess(result) || !result.data.interactiveSession) return;

    setSurface({ ...surface, isSubmitting: true });
    const session = result.data.interactiveSession;
    const rawResult = await browserApi.submitPromptSession({
      sessionId: session.sessionId,
      ownerId: session.ownerId,
      answers: promptAnswers
    });
    const newResult = advancedEnabled ? rawResult : sanitizePlayerDefaultCommandResult(rawResult);
    const newAnswers = isSuccess(newResult) ? buildDefaultPromptAnswers(newResult.data.prompts) : promptAnswers;
    setSurface({ ...surface, kind: 'action-result', actionId: surface.actionId, result: newResult, promptAnswers: newAnswers, isSubmitting: false });
  }

  return (
    <div className="game-surface" ref={surfaceRef} aria-label="Результат действия">
      <div className="game-surface-header">
        <p className="panel-eyebrow">результат действия</p>
        <h3>{isSuccess(result) ? commandStateLabel(result.data.state) : 'Действие недоступно'}</h3>
      </div>
      <div className="game-surface-body">
        {!isSuccess(result) ? (
          <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Игровое действие сейчас недоступно.')}</p>
        ) : (
          <>
            {result.data.notifications.map((notification, index) => (
              <div key={`notif-${index}`} className="summary-card">
                <strong>{toPlayerFacingText(notification.title, 'Уведомление')}</strong>
                <p>{toPlayerFacingText(notification.message, 'Игровое действие изменило состояние.')}</p>
              </div>
            ))}
            {result.data.blocks.map((block, index) => (
              <div key={`block-${index}`}>{renderCommandBlock(block)}</div>
            ))}
            {result.data.interactiveSession && result.data.prompts.length > 0 && (
              <form className="prompt-form" onSubmit={submitPromptAnswers}>
                <h4>Заполните игровую форму</h4>
                {result.data.prompts.map((prompt) => renderPromptControl(prompt, promptAnswers[prompt.id], (promptId, value) => {
                  setSurface({ ...surface, promptAnswers: { ...promptAnswers, [promptId]: value } });
                }))}
                <button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Отправляем…' : 'Отправить форму'}</button>
              </form>
            )}
          </>
        )}
      </div>
      <button type="button" className="game-surface-close" onClick={onClose}>Закрыть</button>
    </div>
  );
}

function ActionMenu({ menu, advancedEnabled }: { menu: BrowserPlayerCommandMenuDto; advancedEnabled: boolean }) {
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
          <ActionSection key={section.id} section={section} advancedEnabled={advancedEnabled} />
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

function ActionSection({ section, advancedEnabled }: { section: BrowserPlayerCommandSectionDto; advancedEnabled: boolean }) {
  return (
    <section className="action-section" aria-labelledby={`action-section-${section.id}`}>
      <div>
        <h3 id={`action-section-${section.id}`}>{toPlayerFacingText(section.label, 'Игровой раздел')}</h3>
        <p className="muted">{toPlayerFacingText(section.description, 'Действия этого раздела доступны ниже.')}</p>
      </div>
      <div className="action-card-list">
        {section.actions.map((action) => (
          <ActionCard key={action.id} action={action} advancedEnabled={advancedEnabled} />
        ))}
      </div>
    </section>
  );
}

function ActionCard({ action, advancedEnabled }: { action: BrowserPlayerCommandActionDto; advancedEnabled: boolean }) {
  const [notice, setNotice] = useState('');
  const [commandResult, setCommandResult] = useState<BrowserApiResult<ExplorerCommandResult> | null>(null);
  const [promptAnswers, setPromptAnswers] = useState<PromptAnswers>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isGuidedForm = action.formMode !== 'none';

  async function submitGuidedForm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setNotice(isGuidedForm ? 'Открываем игровую форму…' : 'Открываем игровой раздел…');

    const rawResult = await browserApi.executeExplorerCommand({ command: action.advancedCommand, ownerLabel: 'Игровое меню' });
    const result = advancedEnabled ? rawResult : sanitizePlayerDefaultCommandResult(rawResult);
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
    const rawResult = await browserApi.submitPromptSession({
      sessionId: session.sessionId,
      ownerId: session.ownerId,
      answers: promptAnswers
    });
    const result = advancedEnabled ? rawResult : sanitizePlayerDefaultCommandResult(rawResult);
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
    <article className={`${commandResult ? 'action-card is-focused' : action.enabled ? 'action-card' : 'action-card is-disabled'}`}>
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
      return 'Поля открыты. Заполните их ниже и отправьте.';
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
  const fallback = 'Книга открывает текущую главу и оставляет игровые решения в основных настройках.';
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
      : 'Запись хода сейчас недоступна; дождитесь подходящего состояния игры.'
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
      return 'Книга готова';
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

  const game = state.game.data;
  const galleryCount = game.media.gallery.length;

  return (
    <ShellPanel title="Медиа" eyebrow="галерея, атлас и быстрые сцены">
      <p className="muted">Материалы текущей главы: {galleryCount > 0 ? `${galleryCount} образов в галерее` : 'галерея ждёт первые образы'}.</p>
      <div className="split-grid three media-section-grid" aria-label={`Медиа текущей главы, изображений ${game.media.gallery.length}`}>
        <QteScenePanel qte={game.qte} />
        <MediaGalleryPanel media={game.media} />
        <MediaAtlasPanel map={game.media.map} realmLabel={game.theme.label} />
      </div>
    </ShellPanel>
  );
}

function QteScenePanel({ qte }: { qte: BrowserGameScreenDto['qte'] }) {
  const [qteState, setQteState] = useState(qte);
  const [selectedGrades, setSelectedGrades] = useState<Record<string, QteGrade>>({});
  const [result, setResult] = useState<BrowserApiResult<BrowserGameScreenDto['qte']> | null>(null);
  const [notice, setNotice] = useState('');
  const [submitting, setSubmitting] = useState<string | null>(null);

  useEffect(() => {
    setQteState(qte);
  }, [qte]);

  async function resolveOffer(decision: 'accept' | 'decline') {
    setSubmitting(`offer-${decision}`);
    setNotice(decision === 'accept' ? 'Принимаем быструю сцену…' : 'Отклоняем быструю сцену…');

    try {
      const response = await browserApi.resolveQteOffer({ decision });
      setResult(response);
      if (isSuccess(response)) {
        setQteState(response.data);
        setNotice(formatQteStateLabel(response.data));
      } else {
        setNotice(toPlayerFacingText(response.playerMessage, 'Не удалось изменить быструю сцену.'));
      }
    } catch {
      setNotice('Не удалось связаться с локальной книгой. Попробуйте ещё раз.');
    } finally {
      setSubmitting(null);
    }
  }

  async function resolveAction(action: QteAction, gradeOverride?: QteGrade) {
    const grade = action.requiresSubmittedGrade ? gradeOverride ?? selectedGrades[action.actionId] ?? qteGradeOptionsForAction(action)[0] ?? 'success' : null;
    setSubmitting(`action-${action.actionId}`);
    setNotice('Записываем выбор быстрой сцены…');

    try {
      const response = await browserApi.resolveQteAction({ actionId: action.actionId, grade });
      setResult(response);
      if (isSuccess(response)) {
        setQteState(response.data);
        setNotice(formatQteStateLabel(response.data));
      } else {
        setNotice(toPlayerFacingText(response.playerMessage, 'Не удалось записать выбор быстрой сцены.'));
      }
    } catch {
      setNotice('Не удалось связаться с локальной книгой. Попробуйте ещё раз.');
    } finally {
      setSubmitting(null);
    }
  }

  function selectGrade(actionId: string, grade: QteGrade) {
    setSelectedGrades((current) => ({ ...current, [actionId]: grade }));
  }

  const activeChapter = qteState.activeScene?.currentChapter;
  const hasVisibleState = Boolean(qteState.offer || qteState.activeScene || qteState.resolution || qteState.completion || qteState.error);

  return (
    <section className="qte-scene-panel" aria-labelledby="qte-scene-panel-title">
      <div>
        <p className="panel-eyebrow">быстрая сцена</p>
        <h2 id="qte-scene-panel-title">Сцена выбора</h2>
        <p>{formatQteStateLabel(qteState)}</p>
        {qteState.lastResolvedReminder && <p className="muted">{toPlayerFacingText(qteState.lastResolvedReminder, 'Последний итог быстрой сцены записан.')}</p>}
      </div>

      {qteState.error && <p className="warning-text">{toPlayerFacingText(qteState.error, 'Быстрая сцена требует внимания.')}</p>}

      {qteState.offer && (
        <article className="summary-card">
          <h3>{toPlayerFacingText(qteState.offer.title, 'Быстрая сцена')}</h3>
          <p>{toPlayerFacingText(qteState.offer.offerText ?? qteState.offer.introNarrative, 'Книга предлагает короткую сцену выбора.')}</p>
          {qteState.offer.cinematicJustification && <p className="muted">{toPlayerFacingText(qteState.offer.cinematicJustification, 'Сцена подходит текущему моменту.')}</p>}
          {qteState.offer.sceneImagePrompt && <p className="muted">Образ сцены: {toPlayerFacingText(qteState.offer.sceneImagePrompt, 'образ уточняется')}</p>}
          {qteState.offer.declineHint && <p className="muted">{toPlayerFacingText(qteState.offer.declineHint, 'Можно отказаться и продолжить обычный ход.')}</p>}
          <div className="phase-chip-grid">
            <button type="button" onClick={() => void resolveOffer('accept')} disabled={Boolean(submitting)}>
              Принять сцену
            </button>
            <button type="button" onClick={() => void resolveOffer('decline')} disabled={Boolean(submitting)}>
              Отказаться
            </button>
          </div>
        </article>
      )}

      {qteState.activeScene && (
        <article className="summary-card">
          <h3>{toPlayerFacingText(qteState.activeScene.title, 'Быстрая сцена активна')}</h3>
          {activeChapter ? (
            <>
              <p>{toPlayerFacingText(activeChapter.narrative ?? activeChapter.title, 'Выберите действие для этой сцены.')}</p>
              {activeChapter.chapterImagePrompt && <p className="muted">Образ главы: {toPlayerFacingText(activeChapter.chapterImagePrompt, 'образ уточняется')}</p>}
              {activeChapter.actions.length > 0 ? (
                <div className="qte-action-list">
                  {activeChapter.actions.map((action) => {
                    const gradeOptions = qteGradeOptionsForAction(action);
                    const selectedGrade = selectedGrades[action.actionId] ?? gradeOptions[0] ?? 'success';
                    return (
                      <article key={action.actionId} className="action-card">
                        <header>
                          <h4>{toPlayerFacingText(action.label, 'Действие сцены')}</h4>
                          <span className="availability-pill">{formatQteActionCheck(action)}</span>
                        </header>
                        {action.requiresSubmittedGrade && (
                          <div className="prompt-control">
                            <label htmlFor={`qte-grade-${action.actionId}`}>Исход проверки</label>
                            <select
                              id={`qte-grade-${action.actionId}`}
                              value={selectedGrade}
                              onChange={(event) => selectGrade(action.actionId, normalizeQteGrade(event.currentTarget.value))}
                              disabled={Boolean(submitting)}
                            >
                              {gradeOptions.map((grade) => (
                                <option key={grade} value={grade}>{formatQteGradeLabel(grade)}</option>
                              ))}
                            </select>
                            <div className="phase-chip-grid" aria-label="Быстрый выбор исхода">
                              {gradeOptions.map((grade) => (
                                <button
                                  key={grade}
                                  type="button"
                                  onClick={() => {
                                    selectGrade(action.actionId, grade);
                                    void resolveAction(action, grade);
                                  }}
                                  disabled={Boolean(submitting)}
                                >
                                  {formatQteGradeLabel(grade)}
                                </button>
                              ))}
                            </div>
                          </div>
                        )}
                        <button type="button" onClick={() => void resolveAction(action)} disabled={Boolean(submitting)}>
                          {action.requiresSubmittedGrade ? 'Подтвердить исход' : 'Выбрать действие'}
                        </button>
                      </article>
                    );
                  })}
                </div>
              ) : (
                <p className="muted">Сцена ждёт следующий фрагмент выбора.</p>
              )}
            </>
          ) : (
            <p className="muted">Книга готовит следующий фрагмент быстрой сцены.</p>
          )}
        </article>
      )}

      {qteState.resolution && (
        <article className="summary-card">
          <h3>Итог выбора</h3>
          <p>{toPlayerFacingText(qteState.resolution.resultText, 'Итог быстрой сцены записан.')}</p>
          <p className="muted">Исход: {formatQteGradeLabel(normalizeQteGrade(qteState.resolution.grade))}</p>
        </article>
      )}

      {qteState.completion && (
        <article className="summary-card">
          <h3>Сцена завершена</h3>
          <p>{toPlayerFacingText(qteState.completion.summary, 'Быстрая сцена завершилась.')}</p>
        </article>
      )}

      {!hasVisibleState && <p className="muted">Быстрая сцена появится здесь, когда книга предложит короткий выбор или кинематик-эпизод.</p>}
      {notice && <p className="composer-notice">{notice}</p>}
      {result && !isSuccess(result) && <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Быстрая сцена не смогла обновиться.')}</p>}
    </section>
  );
}

function MediaGalleryPanel({ media }: { media: BrowserGameScreenDto['media'] }) {
  return (
    <section className="summary-card media-gallery-panel" aria-labelledby="media-gallery-title">
      <div>
        <p className="panel-eyebrow">галерея</p>
        <h2 id="media-gallery-title">Образы главы</h2>
        <p>{media.sceneImagePrompt ? toPlayerFacingText(media.sceneImagePrompt, 'Образ текущей сцены уточняется.') : 'Книга пока не передала образ текущей сцены.'}</p>
      </div>

      {media.gallery.length > 0 ? (
        <div className="media-gallery-grid">
          {media.gallery.map((item) => (
            <article key={item.mediaId} className="media-gallery-card">
              <a href={item.url} target="_blank" rel="noreferrer">
                <img src={item.url} alt={item.fileName} loading="lazy" />
              </a>
              <div>
                <h3>{toPlayerFacingText(item.fileName, 'Изображение сцены')}</h3>
                <dl className="kv-list">
                  <div><dt>Тип</dt><dd>{toPlayerFacingText(item.contentType, 'изображение')}</dd></div>
                  <div><dt>Размер</dt><dd>{formatMediaSize(item.length)}</dd></div>
                  <div><dt>Обновлено</dt><dd>{formatMediaDate(item.modifiedAtUtc)}</dd></div>
                </dl>
                <a className="media-gallery-card__open" href={item.url} target="_blank" rel="noreferrer">Открыть изображение</a>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <p className="muted">Сохранённые изображения появятся здесь после генерации или добавления сцен в локальную галерею.</p>
      )}
    </section>
  );
}

function MediaAtlasPanel({ map, realmLabel }: { map: BrowserGameScreenDto['media']['map']; realmLabel: string }) {
  const currentNode = map.nodes.find((node) => node.id === map.currentNodeId || node.isCurrent);
  const defaultLayer = map.layers.find((layer) => layer.isDefault)?.id ?? map.layers[0]?.id ?? 'all';
  const defaultZ = currentNode?.z ?? map.zLevels[0]?.z ?? 0;
  const [selectedLayer, setSelectedLayer] = useState(defaultLayer);
  const [selectedZ, setSelectedZ] = useState(defaultZ);
  const [showPolitical, setShowPolitical] = useState(false);

  useEffect(() => {
    setSelectedLayer(defaultLayer);
    setSelectedZ(defaultZ);
  }, [defaultLayer, defaultZ]);

  const layers = map.layers.length > 0 ? map.layers : [{ id: 'all', label: 'Все слои', isDefault: true }];
  const zLevels = map.zLevels.length > 0 ? map.zLevels : [{ z: selectedZ, label: `уровень ${selectedZ}` }];
  const visibleNodes = map.nodes.filter((node) => (selectedLayer === 'all' || node.layer === selectedLayer) && node.z === selectedZ);

  return (
    <section className="media-atlas-panel" aria-labelledby="media-atlas-title">
      <div>
        <p className="panel-eyebrow">атлас</p>
        <h2 id="media-atlas-title">{toPlayerFacingText(map.title, 'Атлас текущего мира')}</h2>
        <p>{toPlayerFacingText(realmLabel || map.realm, 'Текущее царство')} · {currentNode ? `сейчас: ${toPlayerFacingText(currentNode.label, 'текущая точка')}` : 'точка героя уточняется'}</p>
      </div>

      <div className="media-atlas-controls">
        <label>
          <span>Выберите уровень</span>
          <select value={selectedZ} onChange={(event) => setSelectedZ(Number(event.currentTarget.value))}>
            {zLevels.map((level) => (
              <option key={level.z} value={level.z}>{toPlayerFacingText(level.label, `уровень ${level.z}`)}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Выберите слой</span>
          <select value={selectedLayer} onChange={(event) => setSelectedLayer(event.currentTarget.value)}>
            {layers.map((layer) => (
              <option key={layer.id} value={layer.id}>{toPlayerFacingText(layer.label, 'слой карты')}</option>
            ))}
          </select>
        </label>
        <label className="checkbox-control">
          <input type="checkbox" checked={showPolitical} onChange={(event) => setShowPolitical(event.currentTarget.checked)} />
          <span>Политическое влияние</span>
        </label>
      </div>

      {visibleNodes.length > 0 ? (
        <div className="media-atlas-node-grid">
          {visibleNodes.map((node) => {
            const influenceEntries = Object.entries(node.influence).filter(([, value]) => value !== 0);
            return (
              <article key={node.id} className={`media-atlas-node${node.isCurrent ? ' is-current' : ''}`}>
                <header>
                  <h3>{toPlayerFacingText(node.label, 'Точка карты')}</h3>
                  <span className="availability-pill">{node.isCurrent ? 'текущая точка' : toPlayerFacingText(node.type, 'точка')}</span>
                </header>
                <dl className="kv-list">
                  <div><dt>Слой</dt><dd>{toPlayerFacingText(node.layer, 'слой')}</dd></div>
                  <div><dt>Уровень</dt><dd>{node.z}</dd></div>
                  <div><dt>Координаты</dt><dd>{node.x}, {node.y}</dd></div>
                  <div><dt>Владелец</dt><dd>{node.ownerFactionName ? toPlayerFacingText(node.ownerFactionName, 'фракция') : 'не указан'}</dd></div>
                </dl>
                {node.details.length > 0 && (
                  <dl className="kv-list">
                    {node.details.map((detail) => (
                      <div key={`${node.id}-${detail.key}`}><dt>{toPlayerFacingText(detail.key, 'Деталь')}</dt><dd>{toPlayerFacingText(detail.value, '—')}</dd></div>
                    ))}
                  </dl>
                )}
                {showPolitical && (
                  <div className="media-atlas-influence" aria-label="Политическое влияние">
                    <h4>Политическое влияние</h4>
                    {influenceEntries.length > 0 ? (
                      <ul>
                        {influenceEntries.map(([faction, value]) => (
                          <li key={faction}><span>{toPlayerFacingText(faction, 'фракция')}</span><strong>{value}</strong></li>
                        ))}
                      </ul>
                    ) : (
                      <p className="muted">Влияние для этой точки пока не отмечено.</p>
                    )}
                  </div>
                )}
              </article>
            );
          })}
        </div>
      ) : (
        <p className="muted">На выбранном уровне и слое пока нет точек карты. Выберите другой слой или продолжите главу.</p>
      )}
    </section>
  );
}

const qteGradeOrder: QteGrade[] = ['success', 'partial', 'fail'];

function qteGradeOptionsForAction(action: QteAction): QteGrade[] {
  const normalized = action.gradeOptions.map(normalizeQteGrade);
  const unique = qteGradeOrder.filter((grade) => normalized.includes(grade));
  return unique.length > 0 ? unique : qteGradeOrder;
}

function normalizeQteGrade(value: string | null | undefined): QteGrade {
  switch ((value ?? '').trim().toLowerCase()) {
    case 'partial':
    case 'part':
    case 'mixed':
      return 'partial';
    case 'fail':
    case 'failure':
    case 'failed':
      return 'fail';
    case 'success':
    default:
      return 'success';
  }
}

function formatQteGradeLabel(grade: QteGrade): string {
  switch (grade) {
    case 'success':
      return 'Успех';
    case 'partial':
      return 'Частичный успех';
    case 'fail':
      return 'Провал';
  }
}

function formatQteActionCheck(action: QteAction): string {
  const difficulty = action.baseDifficulty > 0 ? `сложность ${action.baseDifficulty}` : 'сложность уточняется';
  const characteristic = toPlayerFacingText(action.primaryCharacteristic, 'проверка');
  return `${characteristic} · ${difficulty}`;
}

function formatMediaSize(length: number): string {
  if (!Number.isFinite(length) || length <= 0) {
    return 'размер уточняется';
  }

  const units = ['Б', 'КБ', 'МБ', 'ГБ'];
  let value = length;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  return `${value.toFixed(value >= 10 || unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`;
}

function formatMediaDate(value: string): string {
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) {
    return 'дата уточняется';
  }

  return date.toLocaleString('ru-RU', { dateStyle: 'medium', timeStyle: 'short' });
}

function SettingsRoute({
  state,
  advancedEnabled,
  onStateRefresh
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  advancedEnabled: boolean;
  onStateRefresh: () => Promise<void>;
}) {
  const [settingsResult, setSettingsResult] = useState(state.settings);
  const [notice, setNotice] = useState('');
  const clientSettingsUpdateQueueRef = useRef<Promise<void>>(Promise.resolve());

  useEffect(() => {
    setSettingsResult(state.settings);
  }, [state.settings]);

  if (!isSuccess(settingsResult)) {
    return <EmptyOrFailure result={settingsResult} advancedEnabled={advancedEnabled} errorTitle="Настройки требуют внимания" empty={{
      title: 'Настройки готовятся',
      message: 'Параметры локального клиента появятся, когда общая конфигурация книги будет доступна.',
      action: 'Если вы только открыли книгу, подождите загрузки или вернитесь на главную страницу.'
    }} />;
  }

  const settings = settingsResult.data;

  function updateClientSettings(request: BrowserClientSettingsUpdateRequest) {
    setNotice('Сохраняем настройки книги…');
    clientSettingsUpdateQueueRef.current = clientSettingsUpdateQueueRef.current
      .catch(() => undefined)
      .then(async () => {
        try {
          const updated = await browserApi.updateClientSettings(request);
          setSettingsResult(updated);
          if (isSuccess(updated)) {
            setNotice('Настройки книги сохранены.');
            await onStateRefresh();
          } else {
            setNotice(toPlayerFacingText(updated.playerMessage, 'Не удалось сохранить настройки книги.'));
          }
        } catch {
          setNotice('Не удалось сохранить настройки книги. Попробуйте ещё раз.');
        }
      });
  }

  return (
    <ShellPanel title="Настройки книги" eyebrow="профиль книги">
      <p className="muted">Настройки читаются и сохраняются в общей конфигурации игры, чтобы браузерный и консольный клиенты не расходились.</p>

      <div className="settings-route-grid">
        <section className="settings-control-card" aria-labelledby="settings-language-title">
          <h3 id="settings-language-title">Язык клиента</h3>
          <p className="muted">Выберите язык интерфейса там, где локальный клиент уже поддерживает перевод.</p>
          <label>
            <span>Текущий язык</span>
            <select
              value={settings.language.value}
              onChange={(event) => void updateClientSettings({ language: event.currentTarget.value })}
            >
              {settings.language.choices.map((choice) => (
                <option key={choice.value} value={choice.value}>{choice.label}</option>
              ))}
            </select>
          </label>
          <p>{toPlayerFacingText(settings.language.label, 'Русский')}</p>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-difficulty-title">
          <h3 id="settings-difficulty-title">Сложность</h3>
          <p className="muted">Сложность остаётся общей для консольного клиента и для подсказок ГМа.</p>
          <label>
            <span>Режим сложности</span>
            <select
              value={settings.difficulty.value}
              onChange={(event) => void updateClientSettings({ difficulty: event.currentTarget.value })}
            >
              {settings.difficulty.choices.map((choice) => (
                <option key={choice.value} value={choice.value}>{choice.label}</option>
              ))}
            </select>
          </label>
          <p>{settings.difficulty.choices.find((choice) => choice.value === settings.difficulty.value)?.description ?? 'Базовый уровень сложности.'}</p>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-gm-thoughts-title">
          <h3 id="settings-gm-thoughts-title">Показывать мысли ГМа</h3>
          <p className="muted">Это явная настройка игрока: скрытые заметки не появляются в обычной игре без вашего выбора.</p>
          <label className="audio-toggle">
            <input
              type="checkbox"
              checked={settings.showGmThoughts}
              onChange={(event) => void updateClientSettings({ showGmThoughts: event.currentTarget.checked })}
            />
            <span>{settings.showGmThoughts ? 'Мысли ГМа будут показаны' : 'Мысли ГМа скрыты'}</span>
          </label>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-audio-title">
          <h3 id="settings-audio-title">Музыка и звуковые подсказки</h3>
          <p className="muted">Эти значения используют ту же общую настройку, что и постоянная аудиопанель.</p>
          <label className="audio-toggle">
            <input
              type="checkbox"
              checked={settings.audio.musicEnabled}
              onChange={(event) => void updateClientSettings({ musicEnabled: event.currentTarget.checked })}
            />
            <span>Музыка включена</span>
          </label>
          <label className="audio-slider">
            <span>Громкость музыки: {settings.audio.musicVolume}%</span>
            <input
              type="range"
              min="0"
              max="100"
              value={settings.audio.musicVolume}
              onChange={(event) => void updateClientSettings({ musicVolume: Number(event.currentTarget.value) })}
            />
          </label>
          <label className="audio-toggle">
            <input
              type="checkbox"
              checked={settings.audio.soundEnabled}
              onChange={(event) => void updateClientSettings({ soundEnabled: event.currentTarget.checked })}
            />
            <span>Звуковые подсказки включены</span>
          </label>
          <label className="audio-slider">
            <span>Громкость подсказок: {settings.audio.soundVolume}%</span>
            <input
              type="range"
              min="0"
              max="100"
              value={settings.audio.soundVolume}
              onChange={(event) => void updateClientSettings({ soundVolume: Number(event.currentTarget.value) })}
            />
          </label>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-accessibility-title">
          <h3 id="settings-accessibility-title">Доступность</h3>
          <p className="muted">Эти параметры меняют только представление браузерного клиента и не добавляют отдельной игровой логики.</p>
          <label className="audio-slider">
            <span>Масштаб текста: {settings.accessibility.fontScalePercent}%</span>
            <input
              type="range"
              min="80"
              max="140"
              step="5"
              value={settings.accessibility.fontScalePercent}
              onChange={(event) => void updateClientSettings({ browserFontScalePercent: Number(event.currentTarget.value) })}
            />
          </label>
          <label className="audio-toggle">
            <input
              type="checkbox"
              checked={settings.accessibility.reducedMotion}
              onChange={(event) => void updateClientSettings({ browserReducedMotion: event.currentTarget.checked })}
            />
            <span>Снизить движение интерфейса</span>
          </label>
          <label className="audio-toggle">
            <input
              type="checkbox"
              checked={settings.accessibility.contrastFriendly}
              onChange={(event) => void updateClientSettings({ browserContrastFriendly: event.currentTarget.checked })}
            />
            <span>Контрастный режим</span>
          </label>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-locality-title">
          <h3 id="settings-locality-title">Локальность</h3>
          <p className="status-pill">{settings.locality.localhostOnly ? 'Только локальное подключение' : 'Нужна проверка локальности'}</p>
          <dl className="kv-list">
            <div><dt>Сессия</dt><dd>{toPlayerFacingText(settings.locality.sessionLabel, 'сохранение книги')}</dd></div>
            <div><dt>Папка книги</dt><dd>{settings.locality.gameSessionExists ? 'найдена' : 'ещё не создана'}</dd></div>
            <div><dt>Мост ГМа</dt><dd>{toPlayerFacingText(settings.locality.gmBridgeLabel, settings.locality.gmBridgeEnabled ? 'локальный мост включён' : 'локальный мост выключен')}</dd></div>
          </dl>
          <p className="muted">{toPlayerFacingText(settings.locality.safetySummary, 'Книга работает только на вашем устройстве.')}</p>
        </section>
      </div>

      {notice && <p className="composer-notice">{notice}</p>}
      <p className="muted">Опасные технические настройки, ключи, команды запуска и внутренние параметры моста ГМа не показываются обычному игроку без расширенного режима.</p>
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
      message: 'Панель звука появится, когда книга подготовит настройки аудио.',
      action: 'Игра продолжит работать без музыки; подробности доступны в расширенном режиме.'
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
      setNotice(toPlayerFacingText(audio.missingAssetsMessage, 'Аудиофайлы для выбранного плейлиста не найдены. Игра продолжится без музыки.'));
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
        {!hasAnyAudioAsset(audio) && !advancedEnabled && <p className="muted">Локальный аудиопакет не установлен. Игра продолжается без музыки.</p>}
        {advancedEnabled && audio.missingAssetsMessage && <p className="warning-text">{toPlayerFacingText(audio.missingAssetsMessage, 'Локальные аудиофайлы не найдены.')}</p>}
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

      {advancedEnabled ? (
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
      ) : !hasAnyAudioAsset(audio) ? (
        <p className="muted">Локальный аудиопакет не найден. Игра работает без музыки и звуковых подсказок. Подробности доступны в расширенном режиме.</p>
      ) : null}
      {notice && <p className="composer-notice">{notice}</p>}
    </section>
  );
}

function hasAnyAudioAsset(audio: BrowserAudioSettingsDto): boolean {
  return audio.playlists.some((playlist) => playlist.available) || audio.cues.some((cue) => cue.available);
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
    <ShellPanel title="Загрузка" eyebrow="книга">
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
