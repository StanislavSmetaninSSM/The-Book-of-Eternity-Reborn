import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
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

export type RouteId = 'home' | 'game' | 'soul' | 'world' | 'journal' | 'inventory' | 'media' | 'settings';

export type BrowserShellState =
  | { status: 'loading' }
  | {
      status: 'ready';
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
  advancedEnabled: boolean;
  setAdvancedEnabled: (updater: (value: boolean) => boolean) => void;
  loadBrowserState: () => Promise<void>;
}

const fallbackTheme: RealmTheme = {
  key: 'mortal-world',
  label: 'Мир смертных',
  icon: '🌘',
  accent: '#d8b36a'
};

export const ShellContext = createContext<ShellContextValue | null>(null);

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
  const { shellState, loadBrowserState } = useShellState(advancedEnabled);

  useEffect(() => {
    void loadBrowserState();
  }, [loadBrowserState]);

  const readyState = shellState.status === 'ready' ? shellState : null;
  const gameScreen = readyState && readyState.game.ok ? readyState.game.data : null;
  const menu = readyState && readyState.menu.ok ? readyState.menu.data : null;
  const session = readyState && readyState.session.ok ? readyState.session.data : null;
  const clientSettings = readyState && readyState.settings.ok ? readyState.settings.data : null;
  const realmTheme = useMemo(() => resolveRealmTheme(gameScreen), [gameScreen]);
  const setAdvancedEnabled = useCallback((updater: (value: boolean) => boolean) => {
    setAdvancedEnabledState(updater);
  }, []);

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
    advancedEnabled,
    setAdvancedEnabled,
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
    advancedEnabled,
    setAdvancedEnabled,
    loadBrowserState
  ]);

  return <ShellContext.Provider value={value}>{children}</ShellContext.Provider>;
}
