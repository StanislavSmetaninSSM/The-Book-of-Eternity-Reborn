import type { RouteId } from '../context/ShellContext';

export interface NavItem {
  id: RouteId;
  glyph: string;
  label: string;
  shortcut: string;
  group: 'primary' | 'secondary';
}

export const routeNav: NavItem[] = [
  { id: 'home', glyph: '📖', label: 'Главная', shortcut: '1', group: 'primary' },
  { id: 'game', glyph: '🔥', label: 'Игра', shortcut: '2', group: 'primary' },
  { id: 'soul', glyph: '🕯️', label: 'Душа', shortcut: '3', group: 'primary' },
  { id: 'world', glyph: '🗺️', label: 'Мир', shortcut: '4', group: 'primary' },
  { id: 'journal', glyph: '📜', label: 'Журнал', shortcut: '5', group: 'primary' },
  { id: 'inventory', glyph: '🎒', label: 'Инвентарь', shortcut: '6', group: 'primary' },
  { id: 'media', glyph: '🖼️', label: 'Медиа', shortcut: '7', group: 'secondary' },
  { id: 'settings', glyph: '⚙️', label: 'Настройки', shortcut: '8', group: 'secondary' }
];

export function resolveRouteShortcut(key: string): RouteId | null {
  const index = Number(key) - 1;
  return Number.isInteger(index) && index >= 0 && index < routeNav.length ? routeNav[index].id : null;
}
