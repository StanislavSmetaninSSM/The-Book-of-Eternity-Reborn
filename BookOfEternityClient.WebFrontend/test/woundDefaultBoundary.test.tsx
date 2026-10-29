import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import { CommandResultView } from '../src/components/CommandResultView';
import { ShellContext } from '../src/context/ShellContext';
import { sanitizeExplorerCommandResultForPlayer } from '../src/utils/playerCopy';
import { woundResult, woundShell } from './helpers/woundCommandFixtures';

it('keeps the wound screen after the real default boundary strips the command', () => {
  const safe = sanitizeExplorerCommandResultForPlayer(woundResult());
  expect(safe.command).toBe('');
  const html = renderToStaticMarkup(<ShellContext.Provider value={woundShell(safe)}>
    <CommandResultView />
  </ShellContext.Provider>);
  expect(html).toContain('Ожог души');
  expect(html).toContain('&lt;b&gt;не становится разметкой&lt;/b&gt;');
  expect(html).toMatch(/<h1[^>]*>Активные раны<\/h1>/);
});
