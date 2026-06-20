import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const frontendDir = process.cwd();

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8');
}

describe('realm theming integration', () => {
  it('imports realms.css between layout and components', () => {
    const styles = readSource('src', 'styles.css');

    expect(styles).toContain("@import './styles/realms.css';");
    expect(styles.indexOf("@import './styles/layout.css';")).toBeGreaterThanOrEqual(0);
    expect(styles.indexOf("@import './styles/realms.css';")).toBeGreaterThan(styles.indexOf("@import './styles/layout.css';"));
    expect(styles.indexOf("@import './styles/components.css';")).toBeGreaterThan(styles.indexOf("@import './styles/realms.css';"));
  });

  it('defines realm mood selectors in realms.css instead of layout.css', () => {
    const layout = readSource('src', 'styles', 'layout.css');

    expect(layout).not.toContain(".browser-shell[data-theme-key*='chaos'] {");
    expect(layout).not.toContain(".browser-shell[data-theme-key*='shining'],");
    expect(existsSync(join(frontendDir, 'src', 'styles', 'realms.css'))).toBe(true);

    const realms = readSource('src', 'styles', 'realms.css');
    expect(realms).toContain(".browser-shell[data-theme-key='mortal-world'] {");
    expect(realms).toContain("--realm-accent: var(--realm-mortal);");
    expect(realms).toContain(".browser-shell[data-theme-key*='chaos']");
    expect(realms).toContain("--realm-accent: var(--realm-chaos);");
    expect(realms).toContain(".browser-shell[data-theme-key*='shining']");
    expect(realms).toContain("--realm-accent: var(--realm-shining);");
    expect(realms).toContain('.realm-badge {');
    expect(realms).toMatch(/box-shadow:[\s\S]*color-mix\(in srgb, var\(--realm-accent\) \d+%, transparent\)/);
  });

  it('lets realm css own the accent instead of overriding it inline', () => {
    const appShell = readSource('src', 'App.tsx');

    expect(appShell).not.toContain("'--realm-accent': realmTheme.accent");
    expect(appShell).not.toContain('"--realm-accent": realmTheme.accent');
    expect(appShell).toContain("'--browser-font-scale':");
  });

  it('keeps contrast-friendly accent override stronger than realm mood rules', () => {
    const layout = readSource('src', 'styles', 'layout.css');
    const realms = readSource('src', 'styles', 'realms.css');

    expect(layout).toContain(".browser-shell.is-contrast-friendly[data-theme-key] {");
    expect(layout).toContain("--realm-accent: color-mix(in srgb, var(--color-gold-bright) 78%, white 22%);");
    expect(realms).not.toContain('.browser-shell.is-contrast-friendly');
  });

  it('defines semantic component aliases and removes legacy fallback colors', () => {
    const tokens = readSource('src', 'styles', 'tokens.css');
    const components = readSource('src', 'styles', 'components.css');
    const shellContext = readSource('src', 'context', 'ShellContext.tsx');

    for (const token of [
      '--border-subtle: color-mix(in srgb, var(--realm-accent, var(--color-gold)) 16%, rgba(255, 255, 255, 0.06));',
      '--surface-base: var(--color-ink-2);',
      '--surface-subtle: var(--color-obsidian);',
      '--surface-elevated: var(--color-obsidian-2);',
      '--surface-hover: color-mix(in srgb, var(--realm-accent, var(--color-gold)) 8%, var(--color-obsidian-2));',
      '--surface-active: color-mix(in srgb, var(--realm-accent, var(--color-gold)) 12%, var(--color-obsidian-2));',
      '--text-primary: var(--color-parchment);',
      '--text-muted: var(--color-mist);',
      '--accent-gold: var(--color-gold);'
    ]) {
      expect(tokens).toContain(token);
    }

    for (const legacyFallback of [
      'var(--border-subtle, #2a2a3e)',
      'var(--surface-subtle, #151528)',
      'var(--surface-elevated, #1a1a2e)',
      'var(--surface-hover, #252540)',
      'var(--text-muted, #8a8a9a)',
      'var(--text-primary, #f0e8d8)',
      'var(--surface-active, #2a2a50)',
      'var(--accent-gold, #d8b36a)',
      'var(--realm-accent, var(--accent-gold, #d8b36a))'
    ]) {
      expect(components).not.toContain(legacyFallback);
    }

    expect(shellContext).toMatch(/accent:\s*['\"]#c9a24d['\"]/);
    expect(shellContext).not.toContain("accent: '#d8b36a'");
  });

  it('keeps realm ownership on the shell instead of the tab contract', () => {
    const appShell = readSource('src', 'App.tsx');
    const tabBar = readSource('src', 'components', 'TabBar.tsx');

    expect(appShell).toContain('data-theme-key={realmTheme.key}');
    expect(tabBar).toContain('gameScreen.world.turnNumber');
    expect(tabBar).toContain("gameScreen.world.location || '—'");
    expect(tabBar).not.toContain('realm-badge');
  });
});
