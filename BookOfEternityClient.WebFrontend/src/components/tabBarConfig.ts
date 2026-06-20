import type { TabId } from '../context/ShellContext';

export type TabGlyphId = 'scene' | 'status' | 'help' | 'settings';

export interface TabNavItem {
  id: TabId;
  glyph: TabGlyphId;
  label: string;
  shortcut: string;
  description: string;
}

export const tabNav: readonly TabNavItem[] = [
  { id: 'scene', glyph: 'scene', label: 'Сцена', shortcut: '1', description: 'Текущий ход, повествование и быстрые действия.' },
  { id: 'status', glyph: 'status', label: 'Статус', shortcut: '2', description: 'Персонаж, душа, мир и посмертный прогресс.' },
  { id: 'help', glyph: 'help', label: 'Помощь', shortcut: '3', description: 'Справка книги и подсказки текущей главы.' },
  { id: 'settings', glyph: 'settings', label: 'Настройки', shortcut: '4', description: 'Язык, звук, доступность и явный расширенный режим.' }
];

export function resolveTabShortcut(key: string): TabId | null {
  return tabNav.find((tab) => tab.shortcut === key)?.id ?? null;
}
