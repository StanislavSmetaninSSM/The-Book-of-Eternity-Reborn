import { readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { BrowserApiResult, ExplorerCommandResult, UiBlock } from '../src/api/contracts';
import { BlockList } from '../src/components/BlockRenderer';
import { CommandResultView } from '../src/components/CommandResultView';
import { ShellContext, type ShellContextValue } from '../src/context/ShellContext';
import { sanitizeExplorerCommandResultForPlayer } from '../src/utils/playerCopy';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8').replace(/\r\n/g, '\n');
}

function unavailable<T>(): BrowserApiResult<T> {
  return {
    ok: false,
    status: null,
    kind: 'no-active-session',
    message: 'no session',
    playerMessage: 'Книга ещё не открыта.'
  };
}

function renderCommandResult(result: ExplorerCommandResult, advancedEnabled = false): string {
  const readyState = {
    status: 'ready' as const,
    connectionStatus: 'connected' as const,
    menu: unavailable(),
    session: unavailable(),
    game: unavailable(),
    audio: unavailable(),
    settings: unavailable(),
    lifecycle: null,
    commandCoverage: null
  };
  const context = {
    shellState: readyState,
    readyState,
    gameScreen: null,
    menu: null,
    session: null,
    clientSettings: null,
    realmTheme: null,
    activeTab: 'scene',
    setActiveTab: () => undefined,
    activeRoute: 'game',
    setActiveRoute: () => undefined,
    connectionStatus: 'connected',
    advancedEnabled,
    setAdvancedEnabled: () => undefined,
    composerText: '',
    setComposerText: () => undefined,
    composerNotice: null,
    submitComposer: () => undefined,
    submitComposerText: () => undefined,
    commandResult: result,
    isCommandView: true,
    executeCommand: async () => undefined,
    clearCommandResult: () => undefined,
    loadBrowserState: async () => undefined
  } satisfies ShellContextValue;

  return renderToStaticMarkup(
    <ShellContext.Provider value={context}>
      <CommandResultView />
    </ShellContext.Provider>
  );
}

function baseCommandResult(overrides: Partial<ExplorerCommandResult> = {}): ExplorerCommandResult {
  return {
    command: '/новости_мира',
    state: 'Completed',
    blocks: [
      { kind: 'text', text: 'Сводка мира готова.', tone: 'Default' }
    ],
    actions: [],
    prompts: [],
    notifications: [],
    interactiveSession: null,
    ...overrides
  };
}

describe('BlockRenderer rendered rich command output #1126', () => {
  it('omits rawJson diagnostics from default player output and renders them only in advanced mode', () => {
    const blocks: UiBlock[] = [
      { kind: 'text', text: 'Игрок видит эту сводку.', tone: 'Default' },
      { kind: 'rawJson', title: 'Полная запись мира', json: { secret_marker_1126: 'debug-only' } }
    ];

    const defaultHtml = renderToStaticMarkup(<BlockList blocks={blocks} advancedEnabled={false} />);
    expect(defaultHtml).toContain('Игрок видит эту сводку.');
    expect(defaultHtml).not.toContain('Полная запись мира');
    expect(defaultHtml).not.toContain('secret_marker_1126');
    expect(defaultHtml).not.toContain('расширенном режиме');

    const advancedHtml = renderToStaticMarkup(<BlockList blocks={blocks} advancedEnabled />);
    expect(advancedHtml).toContain('json-tree');
    expect(advancedHtml).toContain('Полная запись мира');
  });

  it('removes rawJson blocks from sanitized default command results before rendering', () => {
    const sanitized = sanitizeExplorerCommandResultForPlayer(baseCommandResult({
      blocks: [
        {
          kind: 'panel',
          title: 'Игровая сводка',
          blocks: [
            { kind: 'text', text: 'Эта строка остаётся видимой.', tone: 'Default' },
            { kind: 'rawJson', title: 'Полная запись', json: { internal_marker_1126: true } }
          ]
        },
        {
          kind: 'panel',
          title: 'Служебная диагностика',
          blocks: [
            { kind: 'rawJson', title: 'Debug JSON', json: { debug_only_1126: true } }
          ]
        }
      ]
    }));

    const html = renderCommandResult(sanitized);

    expect(html).toContain('Игровая сводка');
    expect(html).toContain('Эта строка остаётся видимой.');
    expect(html).not.toContain('Полная запись');
    expect(html).not.toContain('internal_marker_1126');
    expect(html).not.toContain('Служебная диагностика');
    expect(html).not.toContain('debug_only_1126');
  });

  it('marks nested panels with depth so hierarchy survives visual rendering', () => {
    const blocks: UiBlock[] = [
      {
        kind: 'panel',
        title: 'Новости мира',
        blocks: [
          { kind: 'text', text: 'Краткая сводка.', tone: 'Default' },
          {
            kind: 'panel',
            title: 'Событие: Письмо',
            blocks: [
              {
                kind: 'keyValueGrid',
                items: [
                  { key: 'Когда', value: '08:00' },
                  { key: 'Описание', value: 'Письмо нашли на столе.' }
                ]
              }
            ]
          }
        ]
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html).toContain('data-block-depth="0"');
    expect(html).toContain('data-block-depth="1"');
    expect(html).toContain('block-panel--nested');
    expect(html).toContain('Новости мира');
    expect(html).toContain('Событие: Письмо');
  });

  it('renders command actions as a named player-facing action group', () => {
    const html = renderCommandResult(baseCommandResult({
      actions: [
        {
          id: 'event-detail',
          label: 'Открыть событие',
          command: '/новости_мира событие world_event_valmont_letter',
          style: 'Secondary',
          requiresConfirmation: false,
          payload: null
        },
        {
          id: 'back',
          label: 'Назад к списку',
          command: '/новости_мира',
          style: 'Default',
          requiresConfirmation: false,
          payload: null
        }
      ]
    }));

    expect(html).toContain('command-result-view__actions');
    expect(html).toContain('Доступные действия');
    expect(html).toContain('aria-label="Доступные действия"');
    expect(html).toContain('btn-action btn-action--secondary');
    expect(html).toContain('btn-action btn-action--default');
    expect(html).toContain('Открыть событие');
    expect(html).toContain('Назад к списку');
  });

  it('keeps dense tables and key-value grids wrapped inside the command surface', () => {
    const commandUi = readSource('src', 'styles', 'command-ui.css');

    expect(cssRule(commandUi, '.block-table__scroll')).toContain('max-width: 100%;');
    expect(cssRule(commandUi, '.block-table th')).toContain('overflow-wrap: anywhere;');
    expect(cssRule(commandUi, '.block-table td')).toContain('overflow-wrap: anywhere;');
    expect(cssRule(commandUi, '.block-kv')).toContain('grid-template-columns: minmax(8rem, 0.35fr) minmax(0, 1fr);');
    expect(cssRule(commandUi, '.block-kv__row')).toContain('display: grid;');
    expect(cssRule(commandUi, '.block-kv__row dd')).toContain('overflow-wrap: anywhere;');
    expect(commandUi).toContain('@media (max-width: 640px)');
    expect(commandUi).toContain('.block-kv__row { grid-template-columns: 1fr; }');
  });

  it('renders status resource key-value rows as stable visual meters', () => {
    const blocks: UiBlock[] = [
      {
        kind: 'keyValueGrid',
        items: [
          { key: 'Здоровье', value: '85%' },
          { key: 'Энергия', value: '60%' },
          { key: 'Равновесие', value: '95%' },
          { key: 'Царство', value: 'Смертный мир' }
        ]
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html).toContain('block-kv block-kv--with-meters');
    expect(html).toContain('command-resource-meter command-resource-meter--health');
    expect(html).toContain('command-resource-meter command-resource-meter--energy');
    expect(html).toContain('command-resource-meter command-resource-meter--poise');
    expect(html).toContain('aria-label="Здоровье: 85%"');
    expect(html).toContain('style="--meter-value:85%"');
    expect(html).toContain('Царство');
    expect(html).toContain('Смертный мир');
  });

  it('renders structured bonus tables as grouped player-facing cards instead of repeated rows', () => {
    const blocks: UiBlock[] = [
      {
        kind: 'table',
        title: 'Структурные бонусы',
        columns: ['Бонус', 'Поле', 'Значение'],
        rows: [
          { cells: ['Скрытность +1 в городских сценах', 'Тип цели', 'навык'] },
          { cells: ['Скрытность +1 в городских сценах', 'Навык', 'Скрытность'] },
          { cells: ['Скрытность +1 в городских сценах', 'Тип значения', 'плоский бонус'] },
          { cells: ['Скрытность +1 в городских сценах', 'Значение', '1'] },
          { cells: ['Скрытность +1 в городских сценах', 'Условие', 'городские сцены и побег из поместья'] },
          { cells: ['Скрытность +1 в городских сценах', 'Источник', 'Тёмный дорожный плащ'] }
        ]
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html).toContain('structured-bonus-list');
    expect(html).toContain('structured-bonus-card');
    expect(html).toContain('Скрытность +1 в городских сценах');
    expect(html).toContain('<dt>Условие</dt>');
    expect(html).toContain('<dd>городские сцены и побег из поместья</dd>');
    expect(html).not.toContain('<table>');
    expect(html).not.toContain('<th>Бонус</th>');
  });

  it('keeps local map media URLs renderable while still showing player-facing location details', () => {
    const blocks: UiBlock[] = [
      {
        kind: 'map',
        title: 'Карта',
        map: {
          schemaVersion: 1,
          realm: 'Mortal World',
          title: 'Карта смертного мира',
          currentNodeId: 'loc_parlor',
          layers: [{ id: 'world', label: 'Мир', isDefault: true }],
          zLevels: [{ z: 0, label: 'земля' }],
          nodes: [
            {
              id: 'loc_parlor',
              label: 'Гостиная виконта',
              type: 'indoor',
              x: 0,
              y: 0,
              z: 0,
              layer: 'world',
              isCurrent: true,
              ownerFactionId: '',
              ownerFactionName: '',
              influence: {},
              details: [{ key: 'Описание', value: 'Комната с тяжёлыми шторами.' }],
              isPlaceholder: false,
              imageUrl: '/api/media/aW1hZ2VzL2xvY2F0aW9ucy9sb2NfcGFybG9yLnBuZw',
              imageAltText: 'Изображение локации «Гостиная виконта»'
            },
            {
              id: 'loc_locked_gallery',
              label: 'Запертая галерея',
              type: '',
              x: 3,
              y: 0,
              z: 0,
              layer: 'world',
              isCurrent: false,
              ownerFactionId: '',
              ownerFactionName: '',
              influence: {},
              details: [{ key: 'Состояние', value: 'известный выход; подробная локация ещё не открыта' }],
              isPlaceholder: true,
              imageUrl: '',
              imageAltText: ''
            }
          ],
          links: [
            {
              id: 'loc_parlor->loc_locked_gallery',
              sourceNodeId: 'loc_parlor',
              targetNodeId: 'loc_locked_gallery',
              label: 'за ширмой',
              state: 'Hidden',
              layer: 'world',
              z: null
            }
          ],
          regions: []
        }
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html).toContain('map-image-thumb');
    expect(html).toContain('src="/api/media/aW1hZ2VzL2xvY2F0aW9ucy9sb2NfcGFybG9yLnBuZw"');
    expect(html).toContain('Изображение локации «Гостиная виконта»');
    expect(html).toContain('map-node--placeholder');
    expect(html).toContain('Известный выход');
    expect(html).toContain('Комната с тяжёлыми шторами.');
  });
});

function cssRule(source: string, selector: string): string {
  const normalized = stripCssComments(source);
  let openingBrace = normalized.indexOf('{');
  while (openingBrace >= 0) {
    const previousClose = normalized.lastIndexOf('}', openingBrace - 1);
    const prelude = normalized.slice(previousClose + 1, openingBrace).trim();
    if (prelude === selector) {
      const closingBrace = normalized.indexOf('}', openingBrace);
      expect(closingBrace).toBeGreaterThan(openingBrace);
      return normalized.slice(openingBrace + 1, closingBrace);
    }

    openingBrace = normalized.indexOf('{', openingBrace + 1);
  }

  throw new Error(`Expected CSS selector to exist: ${selector}`);
}

function stripCssComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, '');
}
