import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { describe, expect, it } from 'vitest';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8');
}

/** Returns the contents of every tracked stylesheet under src/styles. */
function readAllStyleSheets(): Array<{ name: string; content: string }> {
  const stylesDir = join(frontendDir, 'src', 'styles');
  return readdirSync(stylesDir)
    .filter((file) => file.endsWith('.css'))
    .map((file) => ({ name: file, content: readFileSync(join(stylesDir, file), 'utf-8') }));
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
    // Border / background may be overridden by BG3 cinematic refresh layers —
    // we only require the var-based fallback to be present somewhere.
    expect(commandUi).toContain('var(--border-subtle)');
    expect(commandUi).toContain('var(--surface-command-bar)');
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
    // BG3 cinematic refresh may extend the transition shorthand with extra
    // properties (box-shadow, transform) — require only that the original
    // motion-fast transition props are present in some form.
    expect(commandUi).toMatch(/transition:[^;]*var\(--motion-fast\)/);
    expect(commandUi).not.toContain('transition: all');
  });

  it('never uses transition: all anywhere in the design system stylesheets', () => {
    // The BG3 cinematic refresh layers spread transitions across the whole
    // src/styles directory. `transition: all` forces layout/paint on every
    // animatable property (including unrelated ones) and can hide reduced-motion
    // intent, so require explicit transition properties in every stylesheet.
    const offenders = readAllStyleSheets()
      .filter(({ content }) => content.includes('transition: all'))
      .map(({ name }) => name);
    expect(offenders).toEqual([]);
  });
});
