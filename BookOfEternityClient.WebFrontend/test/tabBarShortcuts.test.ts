import type { TabId } from '../src/context/ShellContext.js';
import { resolveTabShortcut, tabNav } from '../src/components/tabBarConfig.js';

const expectedShortcuts: Array<[string, TabId]> = [
  ['1', 'scene'],
  ['2', 'status'],
  ['3', 'help'],
  ['4', 'settings']
];

if (tabNav.length !== expectedShortcuts.length) {
  throw new Error(`Expected ${expectedShortcuts.length} tab navigation items, got ${tabNav.length}.`);
}

for (const [key, tabId] of expectedShortcuts) {
  const actual = resolveTabShortcut(key);
  if (actual !== tabId) {
    throw new Error(`Expected shortcut ${key} to resolve to ${tabId}, got ${actual ?? 'null'}.`);
  }
}

for (const key of ['0', '5', '9', 'x', '']) {
  if (resolveTabShortcut(key) !== null) {
    throw new Error(`Expected shortcut ${key || '<empty>'} to be ignored.`);
  }
}

const ids = new Set(tabNav.map((item) => item.id));
if (ids.size !== tabNav.length) {
  throw new Error('Tab navigation ids must be unique.');
}

const shortcuts = new Set(tabNav.map((item) => item.shortcut));
if (shortcuts.size !== tabNav.length) {
  throw new Error('Tab navigation shortcuts must be unique.');
}
