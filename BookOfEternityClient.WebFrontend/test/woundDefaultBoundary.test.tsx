import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import type { ExplorerCommandResult } from '../src/api/contracts';
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

for (const [command, view] of [['/раны', 'active'], ['/wounds history', 'history'], ['/heal', 'treatment']] as const)
  it(`retains only the closed ${view} hint through repeated sanitization`, () => {
    const raw = woundResult({ command });
    const safe = sanitizeExplorerCommandResultForPlayer(raw);
    const again = sanitizeExplorerCommandResultForPlayer(safe);
    expect(safe.command).toBe('');
    expect(safe.woundView).toBe(view);
    expect(again).toEqual(safe);
    expect(raw.command).toBe(command);
    expect(raw.woundView).toBeUndefined();
  });
it('rejects arbitrary blank-command hints without exposing their values', () => {
  for (const hint of ['SECRET_PRIVATE_PATH', 'Active', '', 2, null, { mode: 'active' }]) {
    const safe = sanitizeExplorerCommandResultForPlayer({ ...woundResult({ command: '' }), woundView: hint } as unknown as ExplorerCommandResult);
    expect(safe.woundView).toBeUndefined();
    const html = renderToStaticMarkup(<ShellContext.Provider value={woundShell(safe)}><CommandResultView /></ShellContext.Provider>);
    expect(html).toContain('Сведения действия');
    expect(html).not.toContain('SECRET_PRIVATE_PATH');
  }
});
it('lets a new raw command override conflicting hints and clears non-wound hints', () => {
  for (const command of ['/статус', '/unknown']) {
    const safe = sanitizeExplorerCommandResultForPlayer(woundResult({ command, woundView: 'treatment' }));
    expect(safe.woundView).toBeUndefined();
  }
  expect(sanitizeExplorerCommandResultForPlayer(woundResult({ command: '/раны история', woundView: 'treatment' })).woundView).toBe('history');
});
it('keeps raw-command presentation fallback advanced-only', () => {
  const raw = woundResult();
  const html = (advancedEnabled: boolean) => renderToStaticMarkup(<ShellContext.Provider value={woundShell(raw, { advancedEnabled })}><CommandResultView /></ShellContext.Provider>);
  expect(html(false)).toContain('Сведения действия');
  expect(html(true)).toMatch(/<h1[^>]*>Активные раны<\/h1>/);
});
it('does not inherit a previous response hint into a fresh generic response', () => {
  const previous = sanitizeExplorerCommandResultForPlayer(woundResult({ command: '/лечить' }));
  expect(previous.woundView).toBe('treatment');
  const next = sanitizeExplorerCommandResultForPlayer(woundResult({ command: '/статус' }));
  expect(next.woundView).toBeUndefined();
  expect(previous.woundView).toBe('treatment');
});
