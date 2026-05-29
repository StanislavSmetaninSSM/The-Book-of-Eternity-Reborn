import type { ReactNode } from 'react';
import type {
  BrowserClientSettingsDto,
  BrowserGameScreenDto,
  BrowserMainMenuDto,
  LocalWebUiSessionStatus
} from '../src/api/contracts.js';
import type { EmptyStateCopy } from '../src/components/ErrorNotice.js';
import type { BrowserShellState, RealmTheme, RouteId, ShellContextValue } from '../src/context/ShellContext.js';

type ShellPanelProps = Parameters<typeof import('../src/components/ShellPanel.js').ShellPanel>[0];
type StatusBarProps = Parameters<typeof import('../src/components/StatusBar.js').StatusBar>[0];
type ErrorNoticeProps = Parameters<typeof import('../src/components/ErrorNotice.js').ErrorNotice>[0];
type ShellProviderProps = Parameters<typeof import('../src/context/ShellContext.js').ShellProvider>[0];
type ResolveRealmTheme = typeof import('../src/context/ShellContext.js').resolveRealmTheme;
type UseShell = typeof import('../src/context/ShellContext.js').useShell;

const shellPanelProps: ShellPanelProps = {
  title: 'Диагностика',
  eyebrow: 'книга',
  children: null as ReactNode,
  nested: true,
  variant: 'turn'
};

const statusBarProps: StatusBarProps = {
  label: 'Здоровье',
  value: '72%'
};

const errorNoticeProps: ErrorNoticeProps = {
  title: 'Ошибка',
  failure: {
    playerMessage: 'Локальный игровой клиент вернул техническую ошибку.',
    technicalDetails: 'stack trace'
  },
  advancedEnabled: true
};

const emptyStateCopy: EmptyStateCopy = {
  title: 'Книга ждёт открытия',
  message: 'Главная страница появится после подготовки сессии.',
  action: 'Откройте книгу, чтобы продолжить игру.'
};

const realmTheme: RealmTheme = {
  key: 'mortal-world',
  label: 'Мир смертных',
  icon: '🌘',
  accent: '#d8b36a'
};

const shellContextValue: ShellContextValue = {
  shellState: { status: 'loading' },
  readyState: null,
  gameScreen: null as BrowserGameScreenDto | null,
  menu: null as BrowserMainMenuDto | null,
  session: null as LocalWebUiSessionStatus | null,
  clientSettings: null as BrowserClientSettingsDto | null,
  realmTheme,
  activeRoute: 'home' as RouteId,
  setActiveRoute: () => undefined,
  connectionStatus: 'connected',
  advancedEnabled: false,
  setAdvancedEnabled: (updater) => {
    updater(false);
  },
  composerText: '',
  setComposerText: () => undefined,
  composerNotice: null,
  submitComposer: () => undefined,
  loadBrowserState: async () => undefined
};

const shellProviderProps: ShellProviderProps = {
  children: null as ReactNode
};

const resolveRealmTheme: ResolveRealmTheme = ((gameScreen) => {
  const theme = gameScreen?.theme;
  return {
    key: theme?.key ?? realmTheme.key,
    label: theme?.label ?? realmTheme.label,
    icon: theme?.icon ?? realmTheme.icon,
    accent: theme?.accent || realmTheme.accent
  };
}) as ResolveRealmTheme;

const useShell: UseShell = (() => shellContextValue) as UseShell;

void shellPanelProps;
void statusBarProps;
void errorNoticeProps;
void emptyStateCopy;
void realmTheme;
void shellContextValue;
void shellProviderProps;
void resolveRealmTheme;
void useShell;
void (null as BrowserShellState | null);
