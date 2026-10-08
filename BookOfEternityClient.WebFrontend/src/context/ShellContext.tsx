import type { BrowserLoadStateDto } from '../api/contracts';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
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
import { sanitizeExplorerCommandResultForPlayer } from '../utils/playerCopy';
import { createSaveContinuationLatch, type SavePersistenceNotice } from '../utils/savePersistenceNotice';
import { createBrowserLoadController, type BrowserLoadOwner, type LoadPersistenceNotice } from '../utils/loadPersistenceNotice';

export type TabId = 'scene' | 'practice' | 'status' | 'help' | 'settings';

/** @deprecated Temporary compatibility alias until remaining route consumers migrate. */
export type RouteId = 'home' | 'game' | 'practice' | 'daren-showcase' | 'soul' | 'world' | 'journal' | 'inventory' | 'media' | 'settings';

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
  submitComposerText: (text: string) => void;
  commandResult: ExplorerCommandResult | null;
  isCommandView: boolean;
  executeCommand: (command: string) => Promise<void>;
  clearCommandResult: () => void;
  loadBrowserState: (isCurrent?: () => boolean) => Promise<void>;
  /** The real shell owns synchronous load admission and retained interruption evidence. */
  beginLoad?: () => BrowserLoadOwner | null;
  isLoadCurrent?: (owner: BrowserLoadOwner) => boolean;
  finishLoad?: (owner: BrowserLoadOwner) => void;
  isLoadInProgress?: () => boolean;
  loadInProgress?: boolean;
  loadContinuationNotice?: LoadPersistenceNotice | null;
  loadFollowUpNotice?: LoadPersistenceNotice | null;
  reportLoadNotice?: (notice: LoadPersistenceNotice) => void;
  blockLoadContinuation?: (notice: LoadPersistenceNotice) => void;
  refreshAfterLoad?: (generation: string | null, isCurrent: () => boolean, allowNoActiveSession?: boolean, state?: BrowserLoadStateDto | null) => Promise<boolean>;
  /** Present in the real shell; optional for independent read-only component hosts. */
  saveContinuationNotice?: SavePersistenceNotice | null;
  /** Retains a save-specific stop until this shell is restarted after storage reconciliation. */
  blockSaveContinuation?: (notice: SavePersistenceNotice) => void;
  /** Confirms the exact save in required refreshed surfaces; ordinary refresh retains its void contract. */
  refreshAfterSave?: (createdSaveId: string, isCurrent?: () => boolean) => Promise<boolean>;
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
  practice: 'practice',
  'daren-showcase': 'practice',
  soul: 'status',
  world: 'scene',
  journal: 'help',
  inventory: 'status',
  media: 'scene',
  settings: 'settings'
};

const tabToRouteMap: Record<TabId, RouteId> = {
  scene: 'game',
  practice: 'practice',
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
  const [activeRoute, setActiveRouteState] = useState<RouteId>('home');
  const [advancedEnabled, setAdvancedEnabledState] = useState(false);
  const [composerText, setComposerTextState] = useState('');
  const [composerNotice, setComposerNotice] = useState<string | null>(null);
  const [commandResult, setCommandResult] = useState<ExplorerCommandResult | null>(null);
  const [isCommandView, setIsCommandView] = useState(false);
  const composerSubmissionInFlight = useRef(false);
  const operationEpoch = useRef(0);
  const { shellState, loadBrowserState: loadBrowserStateCore, refreshAfterSave: refreshAfterSaveCore,
    refreshAfterLoad: refreshAfterLoadCore, invalidateRefresh } = useShellState(advancedEnabled);
  const saveContinuationLatch = useRef(createSaveContinuationLatch());
  const [saveContinuationNotice, setSaveContinuationNotice] = useState<SavePersistenceNotice | null>(null);
  const blockSaveContinuation = useCallback((notice: SavePersistenceNotice) => {
    setSaveContinuationNotice(saveContinuationLatch.current.block(notice));
  }, []);
  const loadController = useRef(createBrowserLoadController());
  const [loadInProgress, setLoadInProgress] = useState(false);
  const [loadContinuationNotice, setLoadContinuationNotice] = useState<LoadPersistenceNotice | null>(null);
  const [loadFollowUpNotice, setLoadFollowUpNotice] = useState<LoadPersistenceNotice | null>(null);
  const reportLoadNotice = useCallback((notice: LoadPersistenceNotice) => {
    if (notice.needsFollowUp && !notice.continuationBlocked) setLoadFollowUpNotice(notice);
  }, []);
  const continuationBlocked = useCallback(() => saveContinuationLatch.current.isBlocked() || loadController.current.isBlocked(), []);
  const isLoadInProgress = useCallback(() => loadController.current.isInFlight(), []);
  const beginLoad = useCallback(() => {
    if (continuationBlocked()) return null;
    const owner = loadController.current.begin();
    if (owner) {
      operationEpoch.current++;
      composerSubmissionInFlight.current = false;
      setCommandResult(null); setIsCommandView(false); setComposerNotice(null);
      invalidateRefresh(); setLoadInProgress(true);
    }
    return owner;
  }, [continuationBlocked, invalidateRefresh]);
  const isLoadCurrent = useCallback((owner: BrowserLoadOwner) =>
    !continuationBlocked() && loadController.current.isCurrent(owner), [continuationBlocked]);
  const finishLoad = useCallback((owner: BrowserLoadOwner) => {
    loadController.current.finish(owner); setLoadInProgress(loadController.current.isInFlight());
  }, []);
  const blockLoadContinuation = useCallback((notice: LoadPersistenceNotice) => {
    setLoadContinuationNotice(loadController.current.block(notice)); invalidateRefresh();
  }, [invalidateRefresh]);
  const refreshAfterLoad = useCallback((generation: string | null, isCurrent: () => boolean, allowNoActiveSession = false, state?: BrowserLoadStateDto | null) => {
    if (continuationBlocked()) return Promise.resolve(false);
    return refreshAfterLoadCore(generation, () => !continuationBlocked() && isCurrent(), allowNoActiveSession, state);
  }, [continuationBlocked, refreshAfterLoadCore]);
  const loadBrowserState = useCallback(async (isCurrent?: () => boolean) => {
    if (continuationBlocked() || isLoadInProgress()) return;
    await loadBrowserStateCore(() => !continuationBlocked() && !isLoadInProgress() && (isCurrent?.() ?? true));
  }, [loadBrowserStateCore, continuationBlocked, isLoadInProgress]);
  const refreshAfterSave = useCallback((createdSaveId: string, isCurrent?: () => boolean) => {
    if (continuationBlocked() || isLoadInProgress()) return Promise.resolve(false);
    return refreshAfterSaveCore(createdSaveId,
      () => !continuationBlocked() && !isLoadInProgress() && (isCurrent?.() ?? true));
  }, [refreshAfterSaveCore, continuationBlocked, isLoadInProgress]);

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
  const activeTab = useMemo(() => routeToTab(activeRoute), [activeRoute]);

  const setActiveTab = useCallback((tab: TabId) => {
    if (continuationBlocked()) return;
    loadController.current.navigate();
    setActiveRouteState(tabToRoute(tab));
  }, []);

  const setActiveRoute = useCallback((route: RouteId) => {
    if (continuationBlocked()) return;
    loadController.current.navigate();
    setActiveRouteState(route);
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
    if (continuationBlocked() || isLoadInProgress()) return;
    const epoch = operationEpoch.current;
    const isCurrent = () => epoch === operationEpoch.current && !continuationBlocked() && !isLoadInProgress();
    setComposerNotice('Выполняю команду…');
    try {
      const result = await browserApi.executeExplorerCommand({ command, advancedEnabled });
      if (!isCurrent()) return;
      if (result.ok) {
        setCommandResult(advancedEnabled ? result.data : sanitizeExplorerCommandResultForPlayer(result.data));
        setIsCommandView(true);
        setActiveRouteState('game');
        const pendingGmAction = result.data.pendingGmAction?.trim();
        if (pendingGmAction && !continuationBlocked()) {
          setComposerNotice('Запрос отправляется ГМ. Витрина подготавливается…');
          const actionResult = await browserApi.submitPlayerAction({ text: pendingGmAction });
          if (!isCurrent()) return;
          if (actionResult.ok && actionResult.data.success) {
            setComposerNotice(actionResult.data.playerMessage || 'Запрос отправлен ГМ. Дождитесь обновления витрины.');
          } else if (actionResult.ok) {
            setComposerNotice(actionResult.data.playerMessage || 'Не удалось отправить запрос ГМ.');
          } else {
            setComposerNotice(actionResult.playerMessage || 'Не удалось отправить запрос ГМ.');
          }
        } else {
          setComposerNotice(null);
        }
      } else {
        setComposerNotice(result.playerMessage);
      }
    } catch {
      if (isCurrent()) setComposerNotice('Ошибка соединения при выполнении команды.');
    }
    if (isCurrent()) void loadBrowserState();
  }, [advancedEnabled, loadBrowserState, continuationBlocked, isLoadInProgress]);

  const submitComposerText = useCallback((text: string) => {
    const normalized = text.trim();
    if (!normalized || composerSubmissionInFlight.current || continuationBlocked() || isLoadInProgress()) return;

    const epoch = operationEpoch.current;
    const isCurrent = () => epoch === operationEpoch.current && !continuationBlocked() && !isLoadInProgress();
    composerSubmissionInFlight.current = true;

    if (normalized.startsWith('/')) {
      setComposerTextState('');
      void executeCommand(normalized).finally(() => {
        if (epoch === operationEpoch.current) composerSubmissionInFlight.current = false;
      });
      return;
    }

    setComposerNotice('Отправляем действие…');
    void browserApi.submitPlayerAction({ text: normalized }).then((result) => {
      if (!isCurrent()) return;
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
      if (isCurrent()) setComposerNotice('Ошибка соединения. Убедитесь, что игра запущена.');
    }).finally(() => {
      if (epoch === operationEpoch.current) composerSubmissionInFlight.current = false;
    });
  }, [executeCommand, clearCommandResult, loadBrowserState, continuationBlocked, isLoadInProgress]);

  const submitComposer = useCallback((event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    submitComposerText(composerText);
  }, [composerText, submitComposerText]);

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
    submitComposerText,
    commandResult,
    isCommandView,
    executeCommand,
    clearCommandResult,
    loadBrowserState,
    saveContinuationNotice,
    blockSaveContinuation,
    refreshAfterSave,
    beginLoad, isLoadCurrent, finishLoad, isLoadInProgress, loadInProgress,
    loadContinuationNotice, blockLoadContinuation, refreshAfterLoad, loadFollowUpNotice, reportLoadNotice
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
    submitComposerText,
    commandResult,
    isCommandView,
    executeCommand,
    clearCommandResult,
    loadBrowserState,
    saveContinuationNotice,
    blockSaveContinuation,
    refreshAfterSave,
    beginLoad, isLoadCurrent, finishLoad, isLoadInProgress, loadInProgress,
    loadContinuationNotice, blockLoadContinuation, refreshAfterLoad, loadFollowUpNotice, reportLoadNotice
  ]);

  return <ShellContext.Provider value={value}>{children}</ShellContext.Provider>;
}
