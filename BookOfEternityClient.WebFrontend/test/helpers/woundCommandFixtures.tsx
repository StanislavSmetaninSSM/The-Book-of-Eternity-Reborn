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
