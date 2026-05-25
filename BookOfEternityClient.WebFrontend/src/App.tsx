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
  { id: 'soul', label: 'Душа', description: 'Душа, герой, состояние и текущий realm.', icon: '🕯️' },
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
      setComposerNotice('Slash-команды не выполняются из основного поля. Откройте «Расширенный режим» отдельной кнопкой, если хотите перенести команду в техническую панель и подтвердить её там.');
      return;
    }

    setComposerNotice('Художественный ввод подготовлен. Запись хода будет подключена отдельной задачей безопасной локальной записи.');
  }

  return (
    <main className="browser-shell" style={{ '--realm-accent': realmTheme.accent } as CSSProperties}>
      <section className="shell-hero" aria-labelledby="browser-client-title">
        <p className="eyebrow">Book of Eternity Reborn · Browser Client</p>
        <div className="hero-layout">
          <div>
            <h1 id="browser-client-title">Локальный игровой клиент</h1>
            <p className="lead">
              Локальный клиент остаётся источником истины: браузер показывает игру, маршруты и состояние интерфейса,
              но не переносит правила, сохранения или посмертные контракты в отдельный слой.
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
          <ShellPanel title="Сессия" eyebrow="локальная книга">
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
              <p className="muted">Ждём локальный клиент…</p>
            )}
          </ShellPanel>

          <ShellPanel title="Ход и ремонт" eyebrow="безопасность хода">
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
      return <SettingsRoute state={state} activeRoute={activeRoute} />;
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
        <p>{game.narrative.text || 'Последний нарратив пока не найден в локальной книге.'}</p>
      </article>

      <div className="split-grid">
        <ShellPanel title="Состояние хода" eyebrow={game.turnState.state} nested>
          <p className="status-pill">{game.turnState.title}</p>
          <p>{game.turnState.message}</p>
          <p className="muted">Быстрая сцена: {game.qte.notification ?? game.qte.state}</p>
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

function ActionSection({ section }: { section: BrowserPlayerCommandSectionDto }) {
  return (
    <section className="action-section" aria-labelledby={`action-section-${section.id}`}>
      <div>
        <h3 id={`action-section-${section.id}`}>{section.label}</h3>
        <p className="muted">{section.description}</p>
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
      setNotice(result.playerMessage);
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
      setNotice(result.playerMessage);
    }
    setIsSubmitting(false);
  }

  return (
    <article className={action.enabled ? 'action-card' : 'action-card is-disabled'}>
      <header>
        <h4>{action.label}</h4>
        <span className="availability-pill">{action.realmAvailability}</span>
      </header>
      <p>{action.description}</p>
      <p className={action.mutationMode === 'local-turn' ? 'warning-text' : 'muted'}>{action.mutationWarning}</p>
      {!action.enabled && <p className="warning-text">{action.disabledReason}</p>}
      <form className="guided-form" onSubmit={submitGuidedForm}>
        <label htmlFor={`action-form-${action.id}`}>{isGuidedForm ? action.formLabel : 'Открыть раздел'}</label>
        <p id={`action-form-${action.id}`} className="muted">{action.formPrompt}</p>
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
    return <p className="warning-text">{result.playerMessage}</p>;
  }

  const command = result.data;
  return (
    <section className="command-result" aria-label="Результат игрового действия">
      <p className="status-pill">{commandStateLabel(command.state)}</p>
      {command.notifications.map((notification, index) => (
        <p key={`${notification.title}-${index}`} className="composer-notice">
          <strong>{notification.title}</strong> — {notification.message}
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
      return <p>{block.text}</p>;
    case 'panel':
      return (
        <div className="summary-card">
          <h5>{block.title}</h5>
          {block.blocks.map((child, index) => <div key={`${child.kind}-${index}`}>{renderCommandBlock(child)}</div>)}
        </div>
      );
    case 'table':
      return <p>{block.title}: {block.rows.length} строк.</p>;
    case 'list':
      return <ul>{block.items.map((item) => <li key={item}>{item}</li>)}</ul>;
    case 'keyValueGrid':
      return <dl className="kv-list">{block.items.map((item) => <div key={item.key}><dt>{item.key}</dt><dd>{item.value}</dd></div>)}</dl>;
    case 'message':
      return <p className="composer-notice"><strong>{block.title}</strong> — {block.message}</p>;
    case 'image':
      return <p>{block.title}: изображение готово к просмотру.</p>;
    case 'map':
      return <p>{block.title}: карта содержит {block.map.nodes.length} точек.</p>;
    case 'rawJson':
      return <p className="muted">{block.title}: подробные данные доступны в расширенном режиме.</p>;
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
          <span>{prompt.prompt}</span>
        </label>
      );
    case 'selection':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{prompt.prompt}</span>
          <select
            id={controlId}
            value={typeof value === 'string' ? value : ''}
            required={prompt.required}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
          >
            <option value="">Выберите вариант…</option>
            {prompt.options.map((option) => (
              <option key={option.value} value={option.value} disabled={option.disabled}>{option.label}</option>
            ))}
          </select>
        </label>
      );
    case 'longTextInput':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{prompt.prompt}</span>
          <textarea
            id={controlId}
            rows={prompt.minLines ?? 3}
            value={typeof value === 'string' ? value : prompt.defaultValue}
            placeholder={prompt.placeholder}
            required={prompt.required}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
          />
        </label>
      );
    case 'textInput':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{prompt.prompt}</span>
          <input
            id={controlId}
            type="text"
            value={typeof value === 'string' ? value : prompt.defaultValue}
            placeholder={prompt.placeholder}
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
        <div className="summary-card"><h2>Быстрые сцены</h2><p>{qte.notification ?? qte.error ?? qte.state}</p></div>
        <div className="summary-card"><h2>Галерея</h2><p>Изображения и кинематик-сцены будут подключаться через безопасный локальный просмотрщик.</p></div>
      </div>
    </ShellPanel>
  );
}

function SettingsRoute({
  state,
  activeRoute
}: {
  state: Extract<BrowserShellState, { status: 'ready' }>;
  activeRoute: RouteId;
}) {
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
      <AudioSettingsPanel result={state.audio} activeRoute={activeRoute} />
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

  async function updateAudioSettings(request: BrowserAudioSettingsUpdateRequest) {
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
      setNotice(updated.playerMessage);
    }
  }

  async function unlockBrowserMusic() {
    if (!audio.musicEnabled) {
      setNotice('Музыка выключена в общих настройках клиента. Включите её переключателем ниже.');
      return;
    }

    const track = playlist?.tracks[0];
    if (!track) {
      setNotice(audio.missingAssetsMessage || 'Аудиофайлы для выбранного плейлиста не найдены. Клиент продолжит игру без музыки.');
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
      setNotice(`Музыка включена: ${playlist?.label ?? track.label}. Управление громкостью сохраняется в общих настройках.`);
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'браузер заблокировал воспроизведение';
      setNotice(`Браузер не дал запустить музыку автоматически. Нажмите кнопку ещё раз или проверьте разрешения вкладки. Подробность: ${message}`);
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
      setNotice(`Звуковая подсказка воспроизведена: ${asset.label}.`);
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'браузер заблокировал воспроизведение';
      setNotice(`Браузер не дал запустить звуковую подсказку. Подробность: ${message}`);
    }
  }

  return (
    <section className="audio-control-panel" aria-labelledby="browser-audio-title">
      <div>
        <p className="panel-eyebrow">музыка и звук</p>
        <h2 id="browser-audio-title">Аудио браузерного клиента</h2>
        <p>{audio.autoplayGuidance}</p>
        {audio.missingAssetsMessage && <p className="warning-text">{audio.missingAssetsMessage}</p>}
      </div>

      <div className="split-grid">
        <div className="summary-card">
          <h3>Музыка</h3>
          <p>{playlist ? `${playlist.label}: ${playlist.usage}` : 'Плейлисты пока недоступны.'}</p>
          <button type="button" onClick={unlockBrowserMusic} disabled={!audio.musicEnabled || !hasMusic}>
            Включить музыку в браузере
          </button>
          {!hasMusic && <p className="muted">Когда в локальной папке появятся треки, браузер сможет включить их после вашего нажатия.</p>}
        </div>
        <div className="summary-card">
          <h3>Звуковые подсказки</h3>
          <p>{notificationCue?.usage ?? 'QTE и уведомления будут звучать, если локальные файлы найдены.'}</p>
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
            {item.label}: {item.available ? `${item.tracks.length} трек(ов)` : 'файлы не найдены'}
          </span>
        ))}
        {audio.cues.map((cue) => (
          <span key={cue.id} className={cue.available ? 'status-pill' : 'status-pill is-muted'}>
            {cue.label}: {cue.available ? 'готово' : 'нет файла'}
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
    <ShellPanel title="Загрузка" eyebrow="локальный host">
      <p>Собираем главное меню, сессию, игровой экран и состояние хода из локального клиента…</p>
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
