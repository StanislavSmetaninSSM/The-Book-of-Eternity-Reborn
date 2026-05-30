import { existsSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { resolveRouteShortcut, routeNav } from '../src/components/navBarConfig';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8');
}

describe('sidebar navigation source', () => {
  it('extends nav config with grouped items', () => {
    const navConfig = readSource('src', 'components', 'navBarConfig.ts');

    expect(navConfig).toContain('export interface NavItem');
    expect(navConfig).toContain("group: 'primary' | 'secondary';");
    expect(routeNav).toEqual([
      { id: 'home', glyph: '📖', label: 'Главная', shortcut: '1', group: 'primary' },
      { id: 'game', glyph: '🔥', label: 'Игра', shortcut: '2', group: 'primary' },
      { id: 'soul', glyph: '🕯️', label: 'Душа', shortcut: '3', group: 'primary' },
      { id: 'world', glyph: '🗺️', label: 'Мир', shortcut: '4', group: 'primary' },
      { id: 'journal', glyph: '📜', label: 'Журнал', shortcut: '5', group: 'primary' },
      { id: 'inventory', glyph: '🎒', label: 'Инвентарь', shortcut: '6', group: 'primary' },
      { id: 'media', glyph: '🖼️', label: 'Медиа', shortcut: '7', group: 'secondary' },
      { id: 'settings', glyph: '⚙️', label: 'Настройки', shortcut: '8', group: 'secondary' }
    ]);
    expect(resolveRouteShortcut('1')).toBe('home');
    expect(resolveRouteShortcut('8')).toBe('settings');
    expect(resolveRouteShortcut('0')).toBeNull();
    expect(resolveRouteShortcut('x')).toBeNull();
  });

  it('defines the sidebar component with grouped sections', () => {
    const sidebarPath = join(frontendDir, 'src', 'components', 'Sidebar.tsx');

    expect(existsSync(sidebarPath)).toBe(true);

    const sidebar = readFileSync(sidebarPath, 'utf-8');
    expect(sidebar).toContain("const primary = routeNav.filter(r => r.group === 'primary');");
    expect(sidebar).toContain("const secondary = routeNav.filter(r => r.group === 'secondary');");
    expect(sidebar).toContain('<nav className="sidebar" aria-label="Разделы игры">');
    expect(sidebar).toContain('<span className="sidebar__logo-icon">{realmTheme.icon}</span>');
    expect(sidebar).toContain('<div className="sidebar__primary">');
    expect(sidebar).toContain('<div className="sidebar__secondary">');
    expect(sidebar).toContain('aria-current={activeRoute === item.id ? \'page\' : undefined}');
  });

  it('handles keyboard shortcuts while ignoring text entry targets', () => {
    const sidebar = readSource('src', 'components', 'Sidebar.tsx');

    expect(sidebar).toContain('target instanceof HTMLInputElement');
    expect(sidebar).toContain('target instanceof HTMLTextAreaElement');
    expect(sidebar).toContain('target instanceof HTMLSelectElement');
    expect(sidebar).toContain('target instanceof HTMLElement && target.isContentEditable');
    expect(sidebar).toContain('isShortcutBlockedTarget(event.target) || event.ctrlKey || event.altKey || event.metaKey');
    expect(sidebar).toContain("document.addEventListener('keydown', handleKeyDown);");
  });

  it('creates cinematic sidebar styles', () => {
    const sidebarCssPath = join(frontendDir, 'src', 'styles', 'sidebar.css');

    expect(existsSync(sidebarCssPath)).toBe(true);

    const styles = readFileSync(sidebarCssPath, 'utf-8');
    expect(styles).toContain('.sidebar {');
    expect(styles).toContain('width: var(--sidebar-width);');
    expect(styles).toContain('.sidebar__item.is-active::before {');
    expect(styles).toContain('@media (max-width: 640px) {');
  });
});
