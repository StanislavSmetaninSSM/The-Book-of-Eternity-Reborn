import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import { browserApiContractFixtures } from '../src/api/contract-fixture-checks';
import { HelpView } from '../src/components/HelpView';
import { ShellContext, type BrowserShellState } from '../src/context/ShellContext';
import { woundShell } from './helpers/woundCommandFixtures';
import { createWoundHelpCoverage } from './helpers/woundHelpCoverage';

it('labels the actual default wounds handler category in Russian after rendering its command', () => {
  const coverage = createWoundHelpCoverage();
  const success = <T,>(data: T) => ({ ok: true as const, status: 200, data: structuredClone(data) });
  const ready: Extract<BrowserShellState, { status: 'ready' }> = {
    status: 'ready', connectionStatus: 'connected',
    menu: success(browserApiContractFixtures.mainMenuContract),
    session: success(browserApiContractFixtures.sessionStatusContract),
    game: success(browserApiContractFixtures.gameScreenContract),
    audio: success(browserApiContractFixtures.audioSettingsContract),
    settings: success(browserApiContractFixtures.clientSettingsContract),
    lifecycle: null, commandCoverage: { ok: true, status: 200, data: coverage }
  };
  const shell = woundShell(null, {
    shellState: ready, readyState: ready, activeTab: 'help', advancedEnabled: false
  });
  const renderedCoverage = ready.commandCoverage;
  if (!renderedCoverage?.ok) throw new Error('The actual HelpView coverage must be successful.');
  const frozen = JSON.stringify(renderedCoverage.data);
  expect(shell.advancedEnabled).toBe(false);
  expect(coverage.commands).toHaveLength(1);
  expect(coverage.commands[0]).toMatchObject({
    id: 'wounds', handlerKind: 'Wounds', group: 'UniversalMeta', mutationMode: 'ReadOnly',
    surface: 'player-default', primaryCommand: '/раны'
  });

  const html = renderToStaticMarkup(<ShellContext.Provider value={shell}><HelpView /></ShellContext.Provider>);
  expect(html.match(/class="help-command"/g)).toHaveLength(1);
  expect(html).toContain('Раны и заживление');
  expect(html).toContain('/раны');
  expect(html).toContain('/wounds');
  expect(JSON.stringify(renderedCoverage.data)).toBe(frozen);

  const headings = Array.from(html.matchAll(/<span class="help-category__label">([^<]*)<\/span>/g), match => match[1]);
  expect(headings).toEqual(['Раны']);
  expect(html).not.toContain('Wounds');
});
