import type { RouteId } from '../src/context/ShellContext.js';
import { resolveRouteShortcut } from '../src/components/navBarConfig.js';

const expectedShortcuts: Array<[string, RouteId]> = [
  ['1', 'home'],
  ['2', 'game'],
  ['3', 'soul'],
  ['4', 'world'],
  ['5', 'journal'],
  ['6', 'inventory'],
  ['7', 'media'],
  ['8', 'settings']
];

for (const [key, routeId] of expectedShortcuts) {
  const actual = resolveRouteShortcut(key);
  if (actual !== routeId) {
    throw new Error(`Expected shortcut ${key} to resolve to ${routeId}, got ${actual ?? 'null'}.`);
  }
}

for (const key of ['0', '9', 'x']) {
  if (resolveRouteShortcut(key) !== null) {
    throw new Error(`Expected shortcut ${key} to be ignored.`);
  }
}
