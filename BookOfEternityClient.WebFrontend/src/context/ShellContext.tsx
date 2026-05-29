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
  LocalWebUiSessionStatus
} from '../api/contracts';
import { useShellState } from '../hooks/useShellState';
import { toCommandNotice } from '../utils/formatters';

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
  activeRoute: RouteId;
  setActiveRoute: (route: RouteId) => void;
  connectionStatus: 'connected' | 'partial' | 'disconnected';
  advancedEnabled: boolean;
  setAdvancedEnabled: (updater: (value: boolean) => boolean) => void;
  composerText: string;
  setComposerText: (value: string) => void;
  composerNotice: string | null;
  submitComposer: (event: FormEvent<HTMLFormElement>) => void;
  loadBrowserState: () => Promise<void>;
}

const fallbackTheme: RealmTheme = {
  key: 'mortal-world',
  label: 'Мир смертных',
  icon: '🌘',
  accent: '#c9a24d'
};

export const ShellContext = createContext<ShellContextValue | null>(null);

export function isSuccess<T>(result: BrowserApiResult<T>): result is Extract<BrowserApiResult<T>, { ok: true }> {
  return result.ok;
}

export function resolveRealmTheme(gameScreen: BrowserGameScreenDto | null): RealmTheme {
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

export function useShell() {
  const context = useContext(ShellContext);
  if (!context) {
    throw new Error('useShell must be used within a ShellProvider.');
  }

  return context;
}

export function ShellProvider({ children }: { children: ReactNode }) {
  const [activeRoute, setActiveRoute] = useState<RouteId>('home');
  const [advancedEnabled, setAdvancedEnabledState] = useState(false);
  const [composerText, setComposerTextState] = useState('');
  const [composerNotice, setComposerNotice] = useState<string | null>(null);
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
  const setAdvancedEnabled = useCallback((updater: (value: boolean) => boolean) => {
    setAdvancedEnabledState(updater);
  }, []);
  const setComposerText = useCallback((value: string) => {
    setComposerTextState(value);
  }, []);
  const submitComposer = useCallback((event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const normalized = composerText.trim();

    if (normalized.startsWith('/')) {
      setComposerNotice('Служебные команды не выполняются из основного поля. Откройте «Расширенный режим» отдельной кнопкой, если хотите перенести команду в техническую панель и подтвердить её там.');
      return;
    }

    setComposerNotice('Отправляем действие…');
    void browserApi.submitPlayerAction({ text: normalized }).then((result) => {
      if (result.ok && result.data.success) {
        setComposerNotice(result.data.playerMessage);
        setComposerTextState('');
        void loadBrowserState();
      } else if (result.ok && !result.data.success) {
        setComposerNotice(result.data.playerMessage);
      } else {
        setComposerNotice('Не удалось отправить действие. Попробуйте ещё раз.');
      }
    }).catch(() => {
      setComposerNotice('Ошибка соединения. Убедитесь, что клиент запущен.');
    });
  }, [composerText, loadBrowserState]);

  const value = useMemo<ShellContextValue>(() => ({
    shellState,
    readyState,
    gameScreen,
    menu,
    session,
    clientSettings,
    realmTheme,
    activeRoute,
    setActiveRoute,
    connectionStatus,
    advancedEnabled,
    setAdvancedEnabled,
    composerText,
    setComposerText,
    composerNotice,
    submitComposer,
    loadBrowserState
  }), [
    shellState,
    readyState,
    gameScreen,
    menu,
    session,
    clientSettings,
    realmTheme,
    activeRoute,
    connectionStatus,
    advancedEnabled,
    setAdvancedEnabled,
    composerText,
    setComposerText,
    composerNotice,
    submitComposer,
    loadBrowserState
  ]);

  return <ShellContext.Provider value={value}>{children}</ShellContext.Provider>;
}
