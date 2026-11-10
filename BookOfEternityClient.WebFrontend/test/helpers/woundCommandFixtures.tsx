import type { ExplorerCommandResult } from '../../src/api/contracts';
import type { ShellContextValue } from '../../src/context/ShellContext';

export function woundResult(overrides: Partial<ExplorerCommandResult> = {}): ExplorerCommandResult {
  return {
    command: '/раны', state: 'Completed', actions: [], prompts: [], notifications: [], interactiveSession: null,
    blocks: [{ kind: 'entityDossier', entityType: 'panel', title: 'Ожог души — II степень',
      subtitle: '', summary: 'Боль <b>не становится разметкой</b>.', badges: [], media: null,
      facts: [{ label: 'Состояние', value: 'Активна' }, { label: 'Восстановление', value: 'Требуется лечение' }],
      metrics: [], hints: [], list: ['Известный способ (рекомендация): покой.'], cards: [], sections: [] }],
    ...overrides
  };
}

export function woundShell(commandResult: ExplorerCommandResult | null,
  overrides: Partial<ShellContextValue> = {}): ShellContextValue {
  return {
    shellState: { status: 'loading' }, readyState: null, gameScreen: null, menu: null, session: null,
    clientSettings: null, realmTheme: { key: 'chaos-sea', label: 'Море Хаоса', icon: '', accent: '#7a3eb8' },
    activeTab: 'scene', setActiveTab() {}, activeRoute: 'game', setActiveRoute() {},
    connectionStatus: 'connected', advancedEnabled: false, setAdvancedEnabled() {}, composerText: '',
    setComposerText() {}, composerNotice: null, submitComposer() {}, submitComposerText() {},
    commandResult, isCommandView: commandResult !== null, async executeCommand() {}, clearCommandResult() {},
    async loadBrowserState() {}, ...overrides
  };
}

export type WoundCase = 'active' | 'history' | 'empty' | 'unavailable' | 'foreign' | 'target' |
  'confirm' | 'pending' | 'result' | 'cancelled' | 'stale' | 'generic' | 'readonly';

const text = (value: string) => ({ kind: 'text' as const, text: value, tone: 'Default' as const });
const action = (label: string, command: string) => ({ id: label, label, command,
  style: 'Default' as const, requiresConfirmation: false, payload: null });

export function woundCase(name: WoundCase): ExplorerCommandResult {
  if (name === 'active') return woundResult({ actions: [action('История заживших ран', '/раны история')] });
  if (name === 'history') return woundResult({ command: '/раны история',
    blocks: [{ kind: 'panel', title: 'История заживших ран', blocks: [text('Заживший ожог — II степень. Рана зажила.')] }],
    actions: [action('Активные раны', '/раны')] });
  if (name === 'empty') return woundResult({ blocks: [text('Активных ран сейчас нет.')], actions: [action('История заживших ран', '/раны история')] });
  if (name === 'unavailable') return woundResult({ blocks: [text('Сведения о ранах временно недоступны.')] });
  if (name === 'generic') return woundResult({ command: '/статус', blocks: [text('Состояние персонажа')] });
  if (name === 'foreign') return woundResult({ command: '/лечить', blocks: [text('Цель: Ива — сознательный обитатель рядом'),
    ...woundResult().blocks, text('Лечение пока недоступно: нужно действующее согласие обитателя.') ] });
  if (name === 'pending') return woundResult({ command: '/лечить', state: 'Pending', blocks: [text('Попытка передана ГМу. Результат ещё не принят.')] });
  if (name === 'result') return woundResult({ command: '/лечить', blocks: [text('Рана ослаблена до I степени. Мир совершил один цикл.')] });
  if (name === 'cancelled') return woundResult({ command: '/лечить', blocks: [text('Лечение отменено. Бросок и ход не начинались.')] });
  if (name === 'stale') return woundResult({ command: '/лечить', state: 'Failed', blocks: [text('Согласие изменилось. Откройте лечение заново.')] });
  const target = name === 'target';
  const result = woundResult({ command: '/лечить', state: 'RequiresInput', blocks: [text(target
    ? 'Кому требуется помощь? Выбор цели не начинает лечение.'
    : 'Выбрана собственная рана. Лечение займёт один цикл мира и при неудаче.')],
    interactiveSession: { sessionId: target ? 'opaque-target' : 'opaque-confirm', ownerId: 'opaque-owner',
      submitEndpoint: '/api/explorer/prompt-sessions/submit', cancelEndpoint: '/api/explorer/prompt-sessions/cancel',
      requiresLocalUiLock: true, expiresAtUtc: '2099-01-01T00:00:00Z' },
    prompts: [{ kind: 'selection', id: target ? 'wound_target' : 'wound_selection',
      prompt: target ? 'Кому требуется помощь?' : 'Какую рану лечить?', required: true, allowCustom: false,
      options: [ { value: 'opaque-1', label: target ? 'Вы — нынешнее воплощение' : 'Ожог души — II степень', description: 'Сведения доступны.', disabled: false },
        { value: 'opaque-2', label: target ? 'Ива — обитатель рядом' : 'Тяжёлая рана — IV степень',
          description: 'Пока недоступно: нет согласия на помощь.', disabled: true } ] },
      ...(!target ? [{ kind: 'confirmation' as const, id: 'confirm_wound_treatment', prompt: 'Подтвердить лечение и один цикл мира?', required: true, defaultValue: false }] : []) ] });
  if (name === 'readonly') result.interactiveSession = null;
  return result;
}
