import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { isValidElement, type ReactElement } from 'react';
import { describe, expect, it } from 'vitest';
import { StatusBar } from '../src/components/StatusBar';

const frontendDir = process.cwd();

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8').replace(/\r\n/g, '\n');
}

function stripCssComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, '');
}

function ruleBody(source: string, selector: string): string {
  let openingBrace = source.indexOf('{');
  while (openingBrace >= 0) {
    const previousClose = source.lastIndexOf('}', openingBrace - 1);
    const prelude = stripCssComments(source.slice(previousClose + 1, openingBrace)).trim();
    if (prelude === selector) {
      const closingBrace = source.indexOf('}', openingBrace);
      expect(closingBrace, `Expected CSS selector to close its rule body: ${selector}`).toBeGreaterThan(openingBrace);
      return source.slice(openingBrace + 1, closingBrace);
    }

    openingBrace = source.indexOf('{', openingBrace + 1);
  }

  throw new Error(`Expected CSS selector to exist: ${selector}`);
}

function expectRuleIncludes(source: string, selector: string, declaration: string) {
  expect(ruleBody(source, selector), `Expected ${selector} to include ${declaration}`).toContain(declaration);
}

function renderStatusBarClass(value?: string | null): string {
  const element = StatusBar({ label: 'Здоровье', value });
  if (!isValidElement(element)) {
    throw new Error('StatusBar should return a React element.');
  }

  return String((element as ReactElement<{ className?: string }>).props.className ?? '');
}

describe('browser card visual hierarchy #788', () => {
  it('keeps launcher and action CTA cards visually stronger than passive info panels', () => {
    const components = readSource('src', 'styles', 'components.css');

    expectRuleIncludes(components, '.launcher-menu__item.is-primary', 'border-color: color-mix(in srgb, var(--color-gold)');
    expectRuleIncludes(components, '.launcher-menu__item.is-primary', 'background: linear-gradient');
    expectRuleIncludes(components, '.launcher-menu__item.is-primary', 'box-shadow:');
    expectRuleIncludes(components, `.launcher-menu__item.is-primary:hover:not(:disabled),
.launcher-menu__item.is-primary:focus-visible`, 'transform: translateY(-1px)');
    expectRuleIncludes(components, `.launcher-menu__item.is-primary:hover:not(:disabled),
.launcher-menu__item.is-primary:focus-visible`, 'outline: 2px solid');

    expectRuleIncludes(components, '.action-card:not(.is-disabled)', 'border-color: color-mix(in srgb, var(--color-gold)');
    expectRuleIncludes(components, '.action-card:not(.is-disabled)', 'background: linear-gradient');
    expectRuleIncludes(components, '.action-card:not(.is-disabled)', 'box-shadow:');

    expectRuleIncludes(components, '.launcher-mode-panel', 'background: rgba(255, 255, 255, 0.03);');
    expect(ruleBody(components, '.launcher-mode-panel')).not.toContain('var(--shadow-glow)');
    expectRuleIncludes(components, '.launcher-save-card', 'background: rgba(0, 0, 0, 0.2);');
    expect(ruleBody(components, '.launcher-save-card')).not.toContain('linear-gradient');
  });

  it('keeps disabled action surfaces visibly inactive', () => {
    const components = readSource('src', 'styles', 'components.css');

    expectRuleIncludes(components, '.launcher-menu__item:disabled', 'cursor: not-allowed;');
    expectRuleIncludes(components, '.launcher-menu__item:disabled', 'box-shadow: none;');
    expectRuleIncludes(components, '.launcher-menu__item:disabled', 'background: rgba(255, 255, 255, 0.025);');
    expectRuleIncludes(components, '.action-card.is-disabled', 'box-shadow: none;');
    expectRuleIncludes(components, '.action-card.is-disabled', 'filter: saturate(0.55);');
  });

  it('maps StatusBar values to semantic severity classes and scoped fills', () => {
    expect(renderStatusBarClass('67%')).toContain('status-bar--good');
    expect(renderStatusBarClass('66%')).toContain('status-bar--warning');
    expect(renderStatusBarClass('34')).toContain('status-bar--warning');
    expect(renderStatusBarClass('33%')).toContain('status-bar--danger');
    expect(renderStatusBarClass('неизвестно')).toContain('status-bar--danger');
    expect(renderStatusBarClass(undefined)).toContain('status-bar--danger');

    const components = readSource('src', 'styles', 'components.css');
    expectRuleIncludes(components, '.status-bar__track', 'background: rgba(255, 255, 255, 0.08);');
    expectRuleIncludes(components, '.status-bar__fill', 'transition: width var(--motion-fast);');
    expectRuleIncludes(components, '.status-bar--good .status-bar__fill', 'var(--state-success)');
    expectRuleIncludes(components, '.status-bar--warning .status-bar__fill', 'var(--state-warning)');
    expectRuleIncludes(components, '.status-bar--danger .status-bar__fill', 'var(--state-danger)');
  });

  it('keeps status view meters on CSS semantic fills instead of inline colors', () => {
    const statusView = readSource('src', 'components', 'StatusView.tsx');
    expect(statusView).toContain("className={`status-meter status-meter--${severity}`}");
    expect(statusView).not.toContain('background: color');
    expect(statusView).not.toContain('color: string');

    const commandUi = readSource('src', 'styles', 'command-ui.css');
    expectRuleIncludes(commandUi, '.status-meter--good .status-meter__fill', 'var(--state-success)');
    expectRuleIncludes(commandUi, '.status-meter--warning .status-meter__fill', 'var(--state-warning)');
    expectRuleIncludes(commandUi, '.status-meter--danger .status-meter__fill', 'var(--state-danger)');
  });

  it('uses scannable grid typography for key-value lists', () => {
    const components = readSource('src', 'styles', 'components.css');

    expectRuleIncludes(components, '.kv-list div', 'display: grid;');
    expectRuleIncludes(components, '.kv-list div', 'grid-template-columns: minmax(7rem, 0.55fr) minmax(0, 1fr);');
    expectRuleIncludes(components, '.kv-list div', 'gap: var(--space-3);');
    expectRuleIncludes(components, '.kv-list dt', 'color: var(--color-mist);');
    expectRuleIncludes(components, '.kv-list dt', 'font-size: 0.76rem;');
    expectRuleIncludes(components, '.kv-list dd', 'color: var(--color-parchment);');
    expectRuleIncludes(components, '.kv-list dd', 'font-weight: 700;');
    expectRuleIncludes(components, '.kv-list dd', 'overflow-wrap: anywhere;');
  });
});
