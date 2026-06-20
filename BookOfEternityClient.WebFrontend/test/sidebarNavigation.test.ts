import { existsSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { resolveTabShortcut, tabNav } from '../src/components/tabBarConfig';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8');
}

describe('tab navigation source', () => {
  it('defines the current player shell tabs without duplicate combat or QTE practice entries', () => {
    const navConfig = readSource('src', 'components', 'tabBarConfig.ts');

    expect(navConfig).toContain('export interface TabNavItem');
    expect(navConfig).toContain('id: TabId;');
    expect(navConfig).toContain('glyph: TabGlyphId;');
    expect(tabNav).toEqual([
      { id: 'scene', glyph: 'scene', label: 'Сцена', shortcut: '1', description: 'Текущий ход, повествование и быстрые действия.' },
      { id: 'status', glyph: 'status', label: 'Статус', shortcut: '2', description: 'Персонаж, душа, мир и посмертный прогресс.' },
      { id: 'help', glyph: 'help', label: 'Помощь', shortcut: '3', description: 'Справка книги и подсказки текущей главы.' },
      { id: 'settings', glyph: 'settings', label: 'Настройки', shortcut: '4', description: 'Язык, звук, доступность и явный расширенный режим.' }
    ]);
    expect(resolveTabShortcut('1')).toBe('scene');
    expect(resolveTabShortcut('4')).toBe('settings');
    expect(resolveTabShortcut('5')).toBeNull();
    expect(resolveTabShortcut('0')).toBeNull();
    expect(resolveTabShortcut('x')).toBeNull();

    expect(navConfig).not.toContain("label: 'Бой'");
    expect(navConfig).not.toContain("label: 'Тренировка'");
    expect(navConfig).not.toContain("'combat'");
    expect(navConfig).not.toContain("'practice', label: 'Тренировка'");

    for (const emojiIcon of ['📖', '⚡', '📊', '❓', '⚙️']) {
      expect(navConfig).not.toContain(emojiIcon);
    }
  });

  it('defines the tab bar component without reviving the old sidebar route grid', () => {
    const tabBarPath = join(frontendDir, 'src', 'components', 'TabBar.tsx');

    expect(existsSync(tabBarPath)).toBe(true);

    const tabBar = readFileSync(tabBarPath, 'utf-8');
    expect(tabBar).toContain("import { resolveTabShortcut, tabNav, type TabGlyphId } from './tabBarConfig';");
    expect(tabBar).toContain('function TabGlyph');
    expect(tabBar).toContain('<svg');
    expect(tabBar).toContain('<nav className="tab-bar" role="tablist" aria-label="Навигация">');
    expect(tabBar).toContain('tabNav.map((tab)');
    expect(tabBar).toContain('aria-selected={activeTab === tab.id}');
    expect(tabBar).toContain('onClick={() => setActiveTab(tab.id)}');
    expect(tabBar).not.toContain('routeNav');
    expect(tabBar).not.toContain('route-grid');
    expect(tabBar).not.toContain('className="sidebar"');
  });

  it('handles keyboard shortcuts while ignoring text entry targets', () => {
    const tabBar = readSource('src', 'components', 'TabBar.tsx');

    expect(tabBar).toContain('target instanceof HTMLInputElement');
    expect(tabBar).toContain('target instanceof HTMLTextAreaElement');
    expect(tabBar).toContain('target instanceof HTMLSelectElement');
    expect(tabBar).toContain('target instanceof HTMLElement && target.isContentEditable');
    expect(tabBar).toContain('isShortcutBlockedTarget(event.target) || event.ctrlKey || event.altKey || event.metaKey');
    expect(tabBar).toContain("document.addEventListener('keydown', handleKeyDown);");
    expect(tabBar).toContain('const tabId = resolveTabShortcut(event.key);');
  });

  it('keeps tab bar styles in the command UI stylesheet', () => {
    const commandUiCssPath = join(frontendDir, 'src', 'styles', 'command-ui.css');

    expect(existsSync(commandUiCssPath)).toBe(true);

    const styles = readFileSync(commandUiCssPath, 'utf-8');
    expect(styles).toContain('.tab-bar {');
    expect(styles).toContain('.tab-bar__tab {');
    expect(styles).toContain('.tab-bar__tab.is-active');

    const componentStyles = readSource('src', 'styles', 'components.css');
    expect(componentStyles).not.toContain('.nav-bar');
    expect(componentStyles).not.toContain('NavBar');

    const motionStyles = readSource('src', 'styles', 'motion.css');
    expect(motionStyles).not.toContain('.nav-bar');
  });
});
