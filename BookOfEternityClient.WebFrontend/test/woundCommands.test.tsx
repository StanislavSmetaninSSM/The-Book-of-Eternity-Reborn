import { renderToStaticMarkup } from 'react-dom/server';
import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';
import type { ExplorerCommandResult } from '../src/api/contracts';
import { CommandResultView } from '../src/components/CommandResultView';
import { ShellContext } from '../src/context/ShellContext';
import { sanitizeExplorerCommandResultForPlayer } from '../src/utils/playerCopy';
import { woundCase, woundResult, woundShell, type WoundCase } from './helpers/woundCommandFixtures';

function render(result: ExplorerCommandResult) {
  return renderToStaticMarkup(<ShellContext.Provider value={woundShell(sanitizeExplorerCommandResultForPlayer(result))}><CommandResultView /></ShellContext.Provider>);
}

it('presents real known wound facts as a dedicated accessible wound view', () => {
  const html = render(woundResult());
  expect(html).toContain('Ожог души');
  expect(html).toContain('Требуется лечение');
  expect(html).not.toContain('<b>не становится разметкой</b>');
  expect(html).toContain('&lt;b&gt;не становится разметкой&lt;/b&gt;');
  expect(html).toMatch(/<h1[^>]*>Активные раны<\/h1>/);
});

for (const command of ['/раны', '/wounds']) it(`recognizes ${command} as the active view`, () => {
  expect(render(woundResult({ command }))).toMatch(/<h1[^>]*>Активные раны<\/h1>/);
});
for (const command of ['/раны история', '/wounds history']) it(`separates ${command} from active facts`, () => {
  const html = render({ ...woundCase('history'), command });
  expect(html).toContain('Заживший ожог');
  expect(html).not.toContain('Требуется лечение');
  expect(html).toContain('Активные раны');
});
for (const command of ['/лечить', '/treat', '/исцелить', '/heal']) it(`recognizes ${command} without adding authority`, () => {
  const html = render({ ...woundCase('target'), command });
  expect(html).toMatch(/<h1[^>]*>Лечение ран<\/h1>/);
  expect(html).toContain('aria-current="step"');
  expect(html).toContain('Отменить выбор');
});
for (const name of ['empty', 'unavailable', 'foreign', 'pending', 'result', 'cancelled', 'stale'] as WoundCase[])
  it(`preserves the server's distinct ${name} explanation`, () => {
    const result = woundCase(name); const html = render(result);
    for (const block of result.blocks) if (block.kind === 'text') expect(html).toContain(block.text);
    expect(html).not.toContain('Лечение успешно');
    expect(html).not.toContain('opaque-owner');
    expect(html).not.toContain('opaque-confirm');
  });
it('shows unavailable option reasons and explicit confirmation without hidden value labels', () => {
  const html = render(woundCase('confirm'));
  expect(html).toContain('disabled=""');
  expect(html).toContain('Пока недоступно: нет согласия на помощь.');
  expect(html).toContain('Подтвердить лечение и один цикл мира?');
  expect(html).not.toMatch(/<option[^>]*>opaque-/);
  expect(html).not.toContain('checked=""');
});
it('retains read-only prompts without inventing a submit or cancellation session', () => {
  const html = render(woundCase('readonly'));
  expect(html).toContain('Поля команды');
  expect(html).not.toContain('Отправить форму');
  expect(html).not.toContain('Отменить выбор');
});
it('retains generic command rendering and does not expose diagnostic payloads by default', () => {
  expect(render(woundCase('generic'))).toContain('Сведения действия');
  const html = render(woundResult({ blocks: [...woundResult().blocks,
    { kind: 'rawJson', title: 'Служебные сведения', json: { hidden_route: 'SECRET_PRIVATE_PATH' } }] }));
  expect(html).not.toContain('SECRET_PRIVATE_PATH');
  expect(html).not.toContain('hidden_route');
});
it('documents client presentation without inventing a GM healing schema', () => {
  const guide = readFileSync(new URL('../../OtherGuides/Wound_Materialization_Contract.md', import.meta.url), 'utf8');
  expect(guide).toContain('wound_browser_cards_v1');
  expect(guide).toContain('не является результатом лечения');
});
