import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { motion } from 'framer-motion';
import { browserApi } from '../api/client';
import type { BrowserApiResult, BrowserMainMenuDto, ExplorerCommandResult } from '../api/contracts';
import { isSuccess, useShell } from '../context/ShellContext';
import { sanitizePlayerDefaultCommandResult } from '../playerFacingCommandResult';
import { toCommandNotice, toLauncherSaveFailureNotice } from '../utils/formatters';
import { playerLauncherAboutText, toPlayerFacingText } from '../utils/playerCopy';
import { ActionCommandResult } from './CommandResult';
import { buildDefaultPromptAnswers, type PromptAnswers } from './PromptForm';
import { OrnamentBorder } from './decorative';
import { staggerContainer, fadeUp } from '../lib/motion';

type LauncherMode = 'continue' | 'daren-showcase' | 'practice' | 'load' | 'new-game' | 'settings' | 'about';
interface LauncherPrimaryAction { mode: LauncherMode; label: string; description: string; enabled: boolean; disabledReason: string; }

const launcherModes: LauncherMode[] = ['continue', 'daren-showcase', 'practice', 'load', 'new-game', 'settings', 'about'];
const launcherModeDetails: Record<LauncherMode, { label: string; description: string }> = {
  continue: { label: 'Продолжить главу', description: 'Вернуться к текущей сохранённой главе.' },
  'daren-showcase': { label: 'Вылазка Дарена', description: 'Ограбление поместья с постоянным лучшим итогом.' },
  practice: { label: 'Тренировка QTE', description: 'Свободная тренировка быстрых сцен без наград.' },
  load: { label: 'Загрузить сохранение', description: 'Выбрать одну из доступных локальных записей.' },
  'new-game': { label: 'Начать новую главу', description: 'Открыть подготовку новой главы, когда локальная книга разрешает этот шаг.' },
  settings: { label: 'Настроить книгу', description: 'Открыть настройки книги и звука.' },
  about: { label: 'Сведения о книге', description: 'Показать краткое описание книги.' }
};

export function GameLauncher({ menu }: { menu: BrowserMainMenuDto }) {
  const { loadBrowserState: onStateRefresh, setActiveRoute: onActiveRouteChange } = useShell();
  const primaryAction = useMemo(() => selectPrimaryLauncherAction(menu), [menu]);
  const [activeMode, setActiveMode] = useState<LauncherMode>(primaryAction.mode);
  const [launcherNotice, setLauncherNotice] = useState('');
  const [loadingSaveId, setLoadingSaveId] = useState<string | null>(null);
  const isLauncherMountedRef = useRef(true);
  const sessionWarningText = launcherSessionWarningText(menu.session.validationLabel);

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
    if (mode === 'practice') {
      onActiveRouteChange('practice');
      return;
    }
    if (mode === 'daren-showcase') {
      onActiveRouteChange('daren-showcase');
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
      setLauncherNotice('Сохранение не удалось загрузить. Проверьте, что книга запущена, и попробуйте ещё раз.');
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
                  <button type="button" className="launcher-secondary-action" disabled={!loadAvailable || loadingSaveId !== null} onClick={() => void loadSaveSlot(slot)}>
                    {loadingSaveId === slot.saveId ? 'Загружаем…' : 'Загрузить сохранение'}
                  </button>
                </article>
              )) : <p className="muted">Сохранений пока нет. Когда локальная книга найдёт ручные или автоматические записи, они появятся здесь.</p>}
            </div>
            {!loadAvailable && <p className="warning-text">{launcherActionDescription(menu, 'load')}</p>}
          </section>
        );
      }
      case 'practice':
        return (
          <section className="launcher-mode-panel" aria-label="Тренировка QTE">
            <h3>Тренировка QTE</h3>
            <p>{modeDescription}</p>
            <p className="muted">Мини-игры запускаются отдельно от главы: без наград, опыта, предметов и изменения сюжета.</p>
            <button type="button" className="launcher-secondary-action" onClick={() => onActiveRouteChange('practice')}>Открыть тренировку</button>
          </section>
        );
      case 'daren-showcase':
        return (
          <section className="launcher-mode-panel" aria-label="Вылазка Дарена">
            <h3>Вылазка Дарена</h3>
            <p>{modeDescription}</p>
            <p className="muted">Отдельная авторская QTE-сцена: Дарен проникает в поместье, крадёт посох и возвращается в убежище. Обычная глава не меняется.</p>
            <button type="button" className="launcher-secondary-action" onClick={() => onActiveRouteChange('daren-showcase')}>Открыть вылазку</button>
          </section>
        );
      case 'new-game':
        return <NewChapterStartPanel modeAction={modeAction} modeDescription={modeDescription} />;
      case 'settings':
        return (
          <section className="launcher-mode-panel" aria-label="Настройки книги">
            <h3>Настроить книгу</h3>
            <p>{toPlayerFacingText(menu.options.guidance, 'Настройки книги доступны в отдельном разделе.')}</p>
            <button type="button" className="launcher-secondary-action" onClick={() => onActiveRouteChange('settings')}>Открыть настройки</button>
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
      <div className="launcher-art-bg" aria-hidden="true">
        <img src="/main-menu-bg.webp" alt="" onError={(event) => { event.currentTarget.hidden = true; }} />
      </div>
      {/* Atmospheric side flourishes — fill the empty side margins with
          decorative art so the launcher reads as a full-frame composition
          instead of a centered card floating on a bare background. */}
      <div className="launcher-side-flourish launcher-side-flourish--left" aria-hidden="true">
        <svg viewBox="0 0 120 800" focusable="false" preserveAspectRatio="xMinYMid meet">
          <defs>
            <linearGradient id="launcherFlourishLeft" x1="0" y1="0" x2="1" y2="0">
              <stop offset="0" stopColor="#c89b3c" stopOpacity="0.55" />
              <stop offset="1" stopColor="#c89b3c" stopOpacity="0" />
            </linearGradient>
          </defs>
          <path d="M10 0 L10 220 Q60 280 30 360 Q0 420 40 480 Q80 540 30 620 Q-10 700 30 800" fill="none" stroke="url(#launcherFlourishLeft)" strokeWidth="2" />
          <circle cx="12" cy="200" r="4" fill="#d4a84b" opacity="0.7" />
          <circle cx="18" cy="400" r="3" fill="#d4a84b" opacity="0.6" />
          <circle cx="14" cy="600" r="4" fill="#d4a84b" opacity="0.65" />
          <path d="M6 200 l6 -12 l6 12 l-6 12 z" fill="#c89b3c" opacity="0.5" />
          <path d="M12 600 l6 -12 l6 12 l-6 12 z" fill="#c89b3c" opacity="0.5" />
        </svg>
      </div>
      <div className="launcher-side-flourish launcher-side-flourish--right" aria-hidden="true">
        <svg viewBox="0 0 120 800" focusable="false" preserveAspectRatio="xMaxYMid meet">
          <defs>
            <linearGradient id="launcherFlourishRight" x1="1" y1="0" x2="0" y2="0">
              <stop offset="0" stopColor="#c89b3c" stopOpacity="0.55" />
              <stop offset="1" stopColor="#c89b3c" stopOpacity="0" />
            </linearGradient>
          </defs>
          <path d="M110 0 L110 220 Q60 280 90 360 Q120 420 80 480 Q40 540 90 620 Q130 700 90 800" fill="none" stroke="url(#launcherFlourishRight)" strokeWidth="2" />
          <circle cx="108" cy="300" r="3" fill="#d4a84b" opacity="0.6" />
          <circle cx="102" cy="500" r="4" fill="#d4a84b" opacity="0.65" />
          <path d="M102 500 l6 -12 l6 12 l-6 12 z" fill="#c89b3c" opacity="0.5" />
        </svg>
      </div>
      {/* Drifting arcane motes for ambient life (CSS-animated; reduced-motion
          stops them). */}
      <div className="launcher-ambient" aria-hidden="true">
        <span /><span /><span /><span /><span /><span />
      </div>
      <div className="launcher-window">
        <div className="launcher-crest" aria-hidden="true">
          <svg viewBox="0 0 64 64" focusable="false">
            <defs>
              <radialGradient id="launcherCrestGlow" cx="50%" cy="42%" r="55%">
                <stop offset="0" stopColor="#f5d488" stopOpacity="0.9" />
                <stop offset="1" stopColor="#c89b3c" stopOpacity="0" />
              </radialGradient>
            </defs>
            <circle cx="32" cy="30" r="26" fill="url(#launcherCrestGlow)" opacity="0.55" />
            {/* Open book */}
            <path d="M14 40 Q32 33 50 40 L50 50 Q32 43 14 50 Z" fill="#1a140c" stroke="#d4a84b" strokeWidth="1.4" />
            <path d="M32 34 L32 48" stroke="#d4a84b" strokeWidth="1.2" opacity="0.7" />
            {/* Eternal flame */}
            <path d="M32 16 Q26 24 30 30 Q32 27 34 30 Q38 24 32 16 Z" fill="#e07a3a" opacity="0.92" />
            <path d="M32 20 Q29 25 31 29 Q32 27 33 29 Q35 25 32 20 Z" fill="#f5d488" opacity="0.9" />
          </svg>
        </div>
        <div className="launcher-copy">
          <p className="panel-eyebrow">главная книга</p>
          <h2 id="browser-launcher-title">Открыть книгу</h2>
          <p className="muted">{toPlayerFacingText(menu.session.continueReason, 'Выберите продолжение, загрузку или новую главу.')}</p>
          {sessionWarningText && <p className="launcher-session-warning" role="status">{sessionWarningText}</p>}
        </div>

        <motion.nav
          className="launcher-menu"
          aria-label="Действия главного меню"
          variants={staggerContainer}
          initial="hidden"
          animate="visible"
        >
          {launcherModes.map((mode) => {
            const details = launcherModeDetails[mode];
            const action = findLauncherMenuAction(menu, mode);
            const disabled = Boolean(action && !action.enabled && mode !== 'settings' && mode !== 'about');
            const isActive = activeMode === mode;
            const actionDescription = launcherActionDescription(menu, mode);
            const disabledReason = disabled ? launcherActionDisabledReason(menu, mode) : '';
            return (
              <motion.button
                key={mode}
                variants={fadeUp}
                type="button"
                className={`launcher-menu__item${isActive ? ' is-active' : ''}${mode === primaryAction.mode && !disabled ? ' is-primary' : ''}`}
                data-launcher-mode={mode}
                data-action-state={disabled ? 'disabled' : 'enabled'}
                disabled={disabled}
                onClick={() => activateLauncherMode(mode)}
                aria-current={isActive ? 'true' : undefined}
              >
                <strong>{details.label}</strong>
                <span className="launcher-menu__item-copy">{disabled ? disabledReason : actionDescription}</span>
                <span className="launcher-menu__item-affordance" aria-hidden="true">
                  {disabled ? 'Закрыто' : 'Открыть'}
                  <span>→</span>
                </span>
              </motion.button>
            );
          })}
        </motion.nav>

        <OrnamentBorder />

        {renderModeContent()}
        {launcherNotice && <p className="composer-notice">{launcherNotice}</p>}
      </div>
    </article>
  );
}

function NewChapterStartPanel({ modeAction, modeDescription }: { modeAction: BrowserMainMenuDto['actions'][number] | undefined; modeDescription: string; }) {
  const [notice, setNotice] = useState('');
  const [newChapterResult, setNewChapterResult] = useState<BrowserApiResult<ExplorerCommandResult> | null>(null);
  const [newChapterPromptAnswers, setNewChapterPromptAnswers] = useState<PromptAnswers>({});
  const [submissionMode, setSubmissionMode] = useState<'opening' | 'submitting' | null>(null);
  const isSubmitting = submissionMode !== null;
  const isNewChapterMountedRef = useRef(true);
  const startCommand = modeAction?.command.trim() ?? '';
  const canOpenStartFlow = Boolean(modeAction?.enabled && startCommand);
  const unavailableReason = !modeAction
    ? 'Подготовка новой главы пока недоступна из главного меню. Продолжите текущую главу, загрузите сохранение или проверьте состояние книги.'
    : modeAction.enabled && !startCommand
      ? 'Подготовка новой главы пока не открыла безопасные поля. Продолжите текущую главу или загрузите сохранение.'
      : launcherModeUnavailableReason(modeAction, modeDescription);

  useEffect(() => () => {
    isNewChapterMountedRef.current = false;
  }, []);

  async function openNewChapterFlow() {
    if (!canOpenStartFlow) {
      setNotice(unavailableReason);
      return;
    }
    setSubmissionMode('opening');
    setNotice('Открываем форму новой главы…');
    const result = sanitizeNewChapterCommandResult(await browserApi.executeExplorerCommand({ command: startCommand, ownerLabel: 'Главная книга' }));
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
    const result = sanitizeNewChapterCommandResult(await browserApi.submitPromptSession({ sessionId: session.sessionId, ownerId: session.ownerId, answers: newChapterPromptAnswers }));
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
      <p className="muted">Заполните поля подготовки и отправьте их книге, когда она разрешит новую главу.</p>
      {!canOpenStartFlow && <p className="warning-text">{unavailableReason}</p>}
      <button type="button" className="launcher-secondary-action" disabled={!canOpenStartFlow || isSubmitting} onClick={() => void openNewChapterFlow()}>
        <strong>{submissionMode === 'opening' ? 'Открываем…' : submissionMode === 'submitting' ? 'Отправляем…' : 'Открыть форму новой главы'}</strong>
        <span>{canOpenStartFlow ? 'Показать поля подготовки мира.' : 'Сейчас доступно только продолжение или загрузка.'}</span>
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

function launcherActionDisabledReason(menu: BrowserMainMenuDto, mode: LauncherMode): string {
  const action = findLauncherMenuAction(menu, mode);
  return action ? launcherModeUnavailableReason(action, launcherModeDetails[mode].description) : launcherModeDetails[mode].description;
}

function launcherSessionWarningText(validationLabel: string): string {
  const playerText = toPlayerFacingText(validationLabel, '').trim();
  if (!playerText || isLauncherValidationOkLabel(playerText)) {
    return '';
  }
  return playerText;
}

function isLauncherValidationOkLabel(playerText: string): boolean {
  return /валидн/i.test(playerText) && !/(невалидн|ошиб|предупрежд|warning|error)/i.test(playerText);
}

function toNewChapterNotice(result: ExplorerCommandResult): string {
  if (result.state === 'RequiresInput') {
    return 'Форма новой главы открыта. Заполните поля ниже и отправьте её книге.';
  }
  return toCommandNotice(result);
}

function sanitizeNewChapterCommandResult(result: BrowserApiResult<ExplorerCommandResult>): BrowserApiResult<ExplorerCommandResult> {
  return sanitizePlayerDefaultCommandResult(result, {
    blockedTextFallback: 'Подробности подготовки скрыты в обычном режиме.',
    blockTitleFallback: 'Сведения подготовки новой главы',
    notificationTitleFallback: 'Форма новой главы',
    notificationMessageFallback: 'Форма новой главы готова к заполнению.',
    promptTextFallback: 'Заполните поле формы новой главы',
    failureMessageFallback: 'Форма новой главы сейчас недоступна.',
    preserveSafeBlocks: false
  });
}

function selectPrimaryLauncherAction(menu: BrowserMainMenuDto): LauncherPrimaryAction {
  const preferredModes: LauncherMode[] = ['continue', 'daren-showcase', 'practice', 'load', 'new-game'];
  for (const mode of preferredModes) {
    const action = findLauncherMenuAction(menu, mode);
    if (action?.enabled) {
      return { mode, label: launcherModeDetails[mode].label, description: toPlayerFacingText(action.description, launcherModeDetails[mode].description), enabled: true, disabledReason: '' };
    }
  }
  const fallback = preferredModes.map((mode) => ({ mode, action: findLauncherMenuAction(menu, mode) })).find((candidate) => candidate.action);
  const disabledReason = fallback?.action ? toPlayerFacingText(fallback.action.disabledReason || fallback.action.description, 'Главные действия книги сейчас недоступны.') : 'Главные действия книги сейчас недоступны.';
  return { mode: fallback?.mode ?? 'continue', label: 'Открыть книгу', description: 'Выберите продолжение, загрузку или новую главу, когда книга будет готова.', enabled: false, disabledReason };
}

function findLauncherMenuAction(menu: BrowserMainMenuDto, mode: LauncherMode): BrowserMainMenuDto['actions'][number] | undefined {
  switch (mode) {
    case 'continue':
      return menu.actions.find((action) => action.id === 'continue');
    case 'practice':
      return menu.actions.find((action) => action.id === 'qte-practice');
    case 'daren-showcase':
      return menu.actions.find((action) => action.id === 'daren-showcase');
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
  return action.enabled ? toPlayerFacingText(action.description, fallback) : toPlayerFacingText(action.disabledReason || action.description, fallback);
}
