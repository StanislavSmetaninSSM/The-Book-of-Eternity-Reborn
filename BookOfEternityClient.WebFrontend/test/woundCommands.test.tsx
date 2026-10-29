import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import { CommandResultView } from '../src/components/CommandResultView';
import { ShellContext } from '../src/context/ShellContext';
import { woundResult, woundShell } from './helpers/woundCommandFixtures';

it('presents real known wound facts as a dedicated accessible wound view', () => {
  const html = renderToStaticMarkup(<ShellContext.Provider value={woundShell(woundResult())}>
    <CommandResultView />
  </ShellContext.Provider>);
  expect(html).toContain('Ожог души');
  expect(html).toContain('Требуется лечение');
  expect(html).not.toContain('<b>не становится разметкой</b>');
  expect(html).toMatch(/<h1[^>]*>Активные раны<\/h1>/);
});
