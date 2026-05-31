import type { TabId } from '../context/ShellContext';

export interface TabNavItem {
  id: TabId;
  icon: string;
  label: string;
  shortcut: string;
}

export const tabNav: readonly TabNavItem[] = [
  { id: 'scene', icon: '📖', label: 'Сцена', shortcut: '1' },
  { id: 'status', icon: '📊', label: 'Статус', shortcut: '2' },
  { id: 'help', icon: '❓', label: 'Помощь', shortcut: '3' },
  { id: 'settings', icon: '⚙️', label: 'Настройки', shortcut: '4' }
];

export function resolveTabShortcut(key: string): TabId | null {
  return tabNav.find((tab) => tab.shortcut === key)?.id ?? null;
}
