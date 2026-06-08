import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const frontendDir = process.cwd();

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8').replace(/\r\n/g, '\n');
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

function stripCssComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, '');
}

function atRuleBody(source: string, atRule: string): string {
  const atRuleIndex = source.indexOf(atRule);
  expect(atRuleIndex, `Expected CSS at-rule to exist: ${atRule}`).toBeGreaterThanOrEqual(0);

  const openingBrace = source.indexOf('{', atRuleIndex);
  expect(openingBrace, `Expected CSS at-rule to have a body: ${atRule}`).toBeGreaterThanOrEqual(0);

  let depth = 0;
  for (let index = openingBrace; index < source.length; index += 1) {
    if (source[index] === '{') {
      depth += 1;
    } else if (source[index] === '}') {
      depth -= 1;
      if (depth === 0) {
        return source.slice(openingBrace + 1, index);
      }
    }
  }

  throw new Error(`Expected CSS at-rule to close its body: ${atRule}`);
}

function expectRuleIncludes(source: string, selector: string, declaration: string) {
  expect(ruleBody(source, selector), `Expected ${selector} to include ${declaration}`).toContain(declaration);
}

describe('browser card spacing polish', () => {
  it('keeps shared shell, summary, narrative, detail, and sidebar cards on the tokenized #787 spacing rhythm', () => {
    const components = readSource('src', 'styles', 'components.css');

    const shellPanelRule = `.shell-panel,
.advanced-diagnostics,
.empty-state,
.error-notice`;
    expectRuleIncludes(components, shellPanelRule, 'display: grid;');
    expectRuleIncludes(components, shellPanelRule, 'gap: var(--space-4);');
    expectRuleIncludes(components, shellPanelRule, 'padding: var(--space-4);');

    for (const selector of ['.summary-card', '.narrative-card']) {
      expectRuleIncludes(components, selector, 'display: grid;');
    }
    expectRuleIncludes(components, '.summary-card', 'gap: var(--space-3);');
    expectRuleIncludes(components, '.summary-card', 'padding: var(--space-4);');
    expectRuleIncludes(components, '.narrative-card', 'gap: var(--space-4);');
    expectRuleIncludes(components, '.narrative-card', 'padding: var(--space-5);');

    expectRuleIncludes(components, '.status-summary-card', 'gap: var(--space-3);');
    expectRuleIncludes(components, '.detail-surface-card', 'gap: var(--space-3);');

    for (const selector of ['.compact-sidebar__realm', '.compact-sidebar__vitals', '.compact-sidebar__turn']) {
      expectRuleIncludes(components, selector, 'gap: var(--space-3);');
      expectRuleIncludes(components, selector, 'padding: var(--space-4);');
    }
  });

  it('keeps active minimalist browser cards from falling back to cramped ad hoc rem spacing', () => {
    const commandUi = readSource('src', 'styles', 'command-ui.css');

    for (const selector of ['.scene-combat-log', '.block-panel', '.prompt-readonly-card', '.status-card', '.settings-card']) {
      expectRuleIncludes(commandUi, selector, 'display: grid;');
      expectRuleIncludes(commandUi, selector, 'gap: var(--space-3);');
    }

    for (const selector of ['.scene-combat-log', '.block-panel', '.prompt-readonly-card', '.status-card']) {
      expectRuleIncludes(commandUi, selector, 'padding: var(--space-4);');
    }
    expectRuleIncludes(commandUi, '.settings-card', 'padding: calc(var(--space-4) * var(--browser-ui-scale));');
  });

  it('preserves at least space-3 mobile interiors for #787 card surfaces', () => {
    const componentsMobile = atRuleBody(readSource('src', 'styles', 'components.css'), '@media (max-width: 640px)');
    const commandUiMobile = atRuleBody(readSource('src', 'styles', 'command-ui.css'), '@media (max-width: 640px)');

    for (const selector of [
      '.shell-panel',
      '.advanced-diagnostics',
      '.summary-card',
      '.narrative-card',
      '.status-summary-card',
      '.detail-surface-card',
      '.compact-sidebar__realm',
      '.compact-sidebar__vitals',
      '.compact-sidebar__turn'
    ]) {
      expect(componentsMobile, `Expected mobile components.css spacing rule for ${selector}`).toContain(selector);
    }
    expect(componentsMobile).toContain('padding: var(--space-3);');

    for (const selector of ['.scene-combat-log', '.block-panel', '.prompt-readonly-card', '.status-card', '.settings-card']) {
      expect(commandUiMobile, `Expected mobile command-ui.css spacing rule for ${selector}`).toContain(selector);
    }
    expect(commandUiMobile).toContain('padding: var(--space-3);');
    expect(commandUiMobile).toContain('padding: calc(var(--space-3) * var(--browser-ui-scale));');
  });
});
