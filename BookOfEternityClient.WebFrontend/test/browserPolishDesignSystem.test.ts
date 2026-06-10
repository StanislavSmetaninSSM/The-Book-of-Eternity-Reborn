import { existsSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { describe, expect, it } from 'vitest';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8');
}

describe('browser polish design system', () => {
  it('defines dark-fantasy aliases for legacy command shell selectors', () => {
    const tokens = readSource('src', 'styles', 'tokens.css');

    for (const token of [
      '--surface-raised',
      '--text-secondary',
      '--color-accent',
      '--color-success',
      '--color-warning',
      '--color-error',
      '--space-xs',
      '--space-sm',
      '--space-md'
    ]) {
      expect(tokens).toContain(token);
    }
  });

  it('keeps command UI from overriding the browser shell back to a generic web shell', () => {
    const commandUiPath = join(frontendDir, 'src', 'styles', 'command-ui.css');
    expect(existsSync(commandUiPath)).toBe(true);

    const commandUi = readFileSync(commandUiPath, 'utf-8');
    expect(commandUi).not.toContain('display: flex; flex-direction: column; height: 100vh');
    expect(commandUi).not.toContain('#1f6feb');
    expect(commandUi).not.toContain('#0d1117');
    expect(commandUi).not.toContain('#161b22');
    expect(commandUi).not.toContain('var(--surface-raised, #');
    expect(commandUi).toContain('border-bottom: 1px solid var(--border-subtle);');
    expect(commandUi).toContain('background: var(--surface-command-bar);');
    expect(commandUi).toContain('.tab-bar__glyph');
  });

  it('places player surfaces in the shell main column and keeps launcher copy wrapped', () => {
    const layout = readSource('src', 'styles', 'layout.css');
    const commandUi = readSource('src', 'styles', 'command-ui.css');
    const components = readSource('src', 'styles', 'components.css');

    expect(layout).toContain('.browser-shell.is-launcher-route');
    expect(layout).toContain('grid-template-rows: auto auto minmax(0, 1fr) auto;');
    expect(layout).toContain('.browser-shell > .content-area');
    expect(layout).toContain('.browser-shell > .unified-input');
    expect(layout).toContain('grid-column: 2;');
    expect(layout).toContain('grid-row: 3;');
    expect(layout).toContain('min-height: 0;');
    expect(layout).toContain('grid-row: 4;');
    expect(layout).toContain('.browser-shell.is-launcher-route > .content-area');
    expect(layout).toContain('grid-column: 1;');
    expect(commandUi).toContain('min-width: 0;');
    expect(components).toContain('.game-launcher');
    expect(components).toContain('.launcher-menu__item');
    expect(components).toContain('overflow-wrap: anywhere;');
  });

  it('uses explicit focus and reduced-motion safeguards on touched shell controls', () => {
    const commandUi = readSource('src', 'styles', 'command-ui.css');
    const base = readSource('src', 'styles', 'base.css');

    expect(base).toContain(':focus-visible');
    expect(commandUi).toContain('.tab-bar__tab:focus-visible');
    expect(commandUi).toContain('.unified-input__textarea:focus-visible');
    expect(commandUi).toContain('transition: background var(--motion-fast), border-color var(--motion-fast), color var(--motion-fast)');
    expect(commandUi).not.toContain('transition: all');
  });
});
