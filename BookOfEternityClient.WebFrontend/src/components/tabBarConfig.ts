import type { TabId } from '../context/ShellContext';

export interface TabNavItem {
  id: TabId;
  icon: string;
  label: string;
  shortcut: string;
  description: string;
}

export const tabNav: readonly TabNavItem[] = [
  { id: 'scene', icon: '📖', label: 'Сцена', shortcut: '1', description: 'Текущий ход, повествование и быстрые действия.' },
  { id: 'status', icon: '📊', label: 'Статус', shortcut: '2', description: 'Персонаж, душа, мир и посмертный прогресс.' },
  { id: 'help', icon: '❓', label: 'Помощь', shortcut: '3', description: 'Команды /help и подсказки текущего браузерного режима.' },
  { id: 'settings', icon: '⚙️', label: 'Настройки', shortcut: '4', description: 'Язык, звук, доступность и явный расширенный режим.' }
];

export function resolveTabShortcut(key: string): TabId | null {
  return tabNav.find((tab) => tab.shortcut === key)?.id ?? null;
}
