import type { RouteId } from '../context/ShellContext';

export const routeNav: Array<{ id: RouteId; glyph: string; label: string; shortcut: string }> = [
  { id: 'home', glyph: '📖', label: 'Главная', shortcut: '1' },
  { id: 'game', glyph: '🔥', label: 'Игра', shortcut: '2' },
  { id: 'soul', glyph: '🕯️', label: 'Душа', shortcut: '3' },
  { id: 'world', glyph: '🗺️', label: 'Мир', shortcut: '4' },
  { id: 'journal', glyph: '📜', label: 'Журнал', shortcut: '5' },
  { id: 'inventory', glyph: '🎒', label: 'Инвентарь', shortcut: '6' },
  { id: 'media', glyph: '🖼️', label: 'Медиа', shortcut: '7' },
  { id: 'settings', glyph: '⚙️', label: 'Настройки', shortcut: '8' }
];

export function resolveRouteShortcut(key: string): RouteId | null {
  const index = Number(key) - 1;
  return Number.isInteger(index) && index >= 0 && index < routeNav.length ? routeNav[index].id : null;
}
