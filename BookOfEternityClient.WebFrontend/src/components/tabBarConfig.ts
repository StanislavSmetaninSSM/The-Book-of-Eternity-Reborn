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
  { id: 'practice', icon: '⚡', label: 'Тренировка', shortcut: '2', description: 'Свободная тренировка быстрых сцен без наград.' },
  { id: 'status', icon: '📊', label: 'Статус', shortcut: '3', description: 'Персонаж, душа, мир и посмертный прогресс.' },
  { id: 'help', icon: '❓', label: 'Помощь', shortcut: '4', description: 'Справка книги и подсказки текущей главы.' },
  { id: 'settings', icon: '⚙️', label: 'Настройки', shortcut: '5', description: 'Язык, звук, доступность и явный расширенный режим.' }
];

export function resolveTabShortcut(key: string): TabId | null {
  return tabNav.find((tab) => tab.shortcut === key)?.id ?? null;
}
