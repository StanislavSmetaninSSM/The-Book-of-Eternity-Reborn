import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type FormEvent,
  type ReactNode
} from 'react';
import { browserApi } from '../api/client';
import type {
  BrowserApiResult,
  BrowserAudioSettingsDto,
  BrowserClientSettingsDto,
  BrowserCommandCoverageDto,
  BrowserGameScreenDto,
  BrowserLifecycleDashboardDto,
  BrowserMainMenuDto,
  ExplorerCommandResult,
  LocalWebUiSessionStatus
} from '../api/contracts';
import { useShellState } from '../hooks/useShellState';

export type TabId = 'scene' | 'status' | 'help' | 'settings';

/** @deprecated Temporary compatibility alias until remaining route consumers migrate. */
export type RouteId = 'home' | 'game' | 'soul' | 'world' | 'journal' | 'inventory' | 'media' | 'settings';

export type BrowserShellState =
  | { status: 'loading' }
  | {
      status: 'ready';
      connectionStatus: 'connected' | 'partial';
      menu: BrowserApiResult<BrowserMainMenuDto>;
      session: BrowserApiResult<LocalWebUiSessionStatus>;
      game: BrowserApiResult<BrowserGameScreenDto>;
      audio: BrowserApiResult<BrowserAudioSettingsDto>;
      settings: BrowserApiResult<BrowserClientSettingsDto>;
      lifecycle: BrowserApiResult<BrowserLifecycleDashboardDto> | null;
      commandCoverage: BrowserApiResult<BrowserCommandCoverageDto> | null;
    }
  | { status: 'error'; playerMessage: string; technicalDetails?: string };

export interface RealmTheme {
  key: string;
  label: string;
  icon: string;
  accent: string;
}

export interface ShellContextValue {
  shellState: BrowserShellState;
  readyState: Extract<BrowserShellState, { status: 'ready' }> | null;
  gameScreen: BrowserGameScreenDto | null;
  menu: BrowserMainMenuDto | null;
  session: LocalWebUiSessionStatus | null;
  clientSettings: BrowserClientSettingsDto | null;
  realmTheme: RealmTheme;
  activeTab: TabId;
  setActiveTab: (tab: TabId) => void;
  /** @deprecated Temporary compatibility alias until remaining route consumers migrate. */
  activeRoute: RouteId;
  /** @deprecated Temporary compatibility alias until remaining route consumers migrate. */
  setActiveRoute: (route: RouteId) => void;
  connectionStatus: 'connected' | 'partial' | 'disconnected';
  advancedEnabled: boolean;
  setAdvancedEnabled: (updater: (value: boolean) => boolean) => void;
  composerText: string;
  setComposerText: (value: string) => void;
  composerNotice: string | null;
  submitComposer: (event: FormEvent<HTMLFormElement>) => void;
  commandResult: ExplorerCommandResult | null;
  isCommandView: boolean;
  executeCommand: (command: string) => Promise<void>;
  clearCommandResult: () => void;
  loadBrowserState: () => Promise<void>;
}

const fallbackTheme: RealmTheme = {
  key: 'mortal-world',
  label: 'Мир смертных',
  icon: '🌘',
  accent: '#c9a24d'
};

const routeToTabMap: Record<RouteId, TabId> = {
  home: 'scene',
  game: 'scene',
  soul: 'status',
  world: 'scene',
  journal: 'help',
  inventory: 'status',
  media: 'scene',
  settings: 'settings'
};

const tabToRouteMap: Record<TabId, RouteId> = {
  scene: 'game',
  status: 'soul',
  help: 'journal',
  settings: 'settings'
};

export const ShellContext = createContext<ShellContextValue | null>(null);

export function isSuccess<T>(result: BrowserApiResult<T>): result is Extract<BrowserApiResult<T>, { ok: true }> {
  return result.ok;
}

export function resolveRealmTheme(gameScreen: BrowserGameScreenDto | null): RealmTheme {
  if (!gameScreen) return fallbackTheme;
  return {
    key: gameScreen.theme.key,
    label: gameScreen.theme.label,
    icon: gameScreen.theme.icon,
    accent: gameScreen.theme.accent || fallbackTheme.accent
  };
}

function routeToTab(route: RouteId): TabId {
  return routeToTabMap[route];
}

function tabToRoute(tab: TabId): RouteId {
  return tabToRouteMap[tab];
}

export function useShell() {
  const context = useContext(ShellContext);
  if (!context) throw new Error('useShell must be used within a ShellProvider.');
  return context;
}

export function ShellProvider({ children }: { children: ReactNode }) {
  const [activeTab, setActiveTabState] = useState<TabId>('scene');
  const [advancedEnabled, setAdvancedEnabledState] = useState(false);
  const [composerText, setComposerTextState] = useState('');
  const [composerNotice, setComposerNotice] = useState<string | null>(null);
  const [commandResult, setCommandResult] = useState<ExplorerCommandResult | null>(null);
  const [isCommandView, setIsCommandView] = useState(false);
  const { shellState, loadBrowserState } = useShellState(advancedEnabled);

  useEffect(() => {
    void loadBrowserState();
  }, [loadBrowserState]);

  const readyState = shellState.status === 'ready' ? shellState : null;
  const gameScreen = readyState && isSuccess(readyState.game) ? readyState.game.data : null;
  const menu = readyState && isSuccess(readyState.menu) ? readyState.menu.data : null;
  const session = readyState && isSuccess(readyState.session) ? readyState.session.data : null;
  const clientSettings = readyState && isSuccess(readyState.settings) ? readyState.settings.data : null;
  const connectionStatus: 'connected' | 'partial' | 'disconnected' =
    shellState.status === 'ready' ? shellState.connectionStatus :
    shellState.status === 'error' ? 'disconnected' : 'connected';
  const realmTheme = useMemo(() => resolveRealmTheme(gameScreen), [gameScreen]);
  const activeRoute = useMemo(() => tabToRoute(activeTab), [activeTab]);

  const setActiveTab = useCallback((tab: TabId) => {
    setActiveTabState(tab);
  }, []);

  const setActiveRoute = useCallback((route: RouteId) => {
    setActiveTabState(routeToTab(route));
  }, []);

  const setAdvancedEnabled = useCallback((updater: (value: boolean) => boolean) => {
    setAdvancedEnabledState(updater);
  }, []);

  const setComposerText = useCallback((value: string) => {
    setComposerTextState(value);
  }, []);

  const clearCommandResult = useCallback(() => {
    setCommandResult(null);
    setIsCommandView(false);
  }, []);

  const executeCommand = useCallback(async (command: string) => {
    setComposerNotice('Выполняю команду…');
    try {
      const result = await browserApi.executeExplorerCommand({ command, advancedEnabled });
      if (result.ok) {
        setCommandResult(result.data);
        setIsCommandView(true);
        setActiveTabState('scene');
        setComposerNotice(null);
      } else {
        setComposerNotice(result.playerMessage);
      }
    } catch {
      setComposerNotice('Ошибка соединения при выполнении команды.');
    }
    void loadBrowserState();
  }, [advancedEnabled, loadBrowserState]);

  const submitComposer = useCallback((event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const normalized = composerText.trim();
    if (!normalized) return;

    if (normalized.startsWith('/')) {
      setComposerTextState('');
      void executeCommand(normalized);
      return;
    }

    setComposerNotice('Отправляем действие…');
    void browserApi.submitPlayerAction({ text: normalized }).then((result) => {
      if (result.ok && result.data.success) {
        setComposerNotice(result.data.playerMessage);
        setComposerTextState('');
        clearCommandResult();
        void loadBrowserState();
      } else if (result.ok && !result.data.success) {
        setComposerNotice(result.data.playerMessage);
      } else {
        setComposerNotice('Не удалось отправить действие. Попробуйте ещё раз.');
      }
    }).catch(() => {
      setComposerNotice('Ошибка соединения. Убедитесь, что клиент запущен.');
    });
  }, [composerText, executeCommand, clearCommandResult, loadBrowserState]);

  const value = useMemo<ShellContextValue>(() => ({
    shellState,
    readyState,
    gameScreen,
    menu,
    session,
    clientSettings,
    realmTheme,
    activeTab,
    setActiveTab,
    activeRoute,
    setActiveRoute,
    connectionStatus,
    advancedEnabled,
    setAdvancedEnabled,
    composerText,
    setComposerText,
    composerNotice,
    submitComposer,
    commandResult,
    isCommandView,
    executeCommand,
    clearCommandResult,
    loadBrowserState
  }), [
    shellState,
    readyState,
    gameScreen,
    menu,
    session,
    clientSettings,
    realmTheme,
    activeTab,
    setActiveTab,
    activeRoute,
    setActiveRoute,
    connectionStatus,
    advancedEnabled,
    setAdvancedEnabled,
    composerText,
    setComposerText,
    composerNotice,
    submitComposer,
    commandResult,
    isCommandView,
    executeCommand,
    clearCommandResult,
    loadBrowserState
  ]);

  return <ShellContext.Provider value={value}>{children}</ShellContext.Provider>;
}
