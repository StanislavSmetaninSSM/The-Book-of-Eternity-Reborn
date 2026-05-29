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
    expect(realms).toContain(".browser-shell[data-theme-key*='chaos']::before {");
    expect(realms).toContain("--realm-accent: var(--realm-chaos);");
    expect(realms).toContain(".browser-shell[data-theme-key*='shining']::before,");
    expect(realms).toContain("--realm-accent: var(--realm-shining);");
    expect(realms).toContain('.realm-badge {');
    expect(realms).toContain("box-shadow: 0 0 0.6rem color-mix(in srgb, var(--realm-accent) 30%, transparent);");
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

  it('renders the current realm label inside the realm badge', () => {
    const navBar = readSource('src', 'components', 'NavBar.tsx');

    expect(navBar).toContain('<span className="realm-badge">');
    expect(navBar).toContain('{realmTheme.icon} {realmTheme.label}');
  });
});
