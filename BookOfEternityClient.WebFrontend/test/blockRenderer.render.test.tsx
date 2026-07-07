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

  it('keeps prototype entity dossier fields when sanitizing default player command results', () => {
    const sanitized = sanitizeExplorerCommandResultForPlayer(baseCommandResult({
      blocks: [
        {
          kind: 'entityDossier',
          entityType: 'inventory',
          title: 'Инвентарь',
          subtitle: 'Снаряжение',
          summary: 'Предметы персонажа.',
          badges: [{ label: '2 предмета', tone: 'Accent', icon: 'inventory' }],
          media: null,
          facts: [{ label: '💰 Деньги', value: '500' }],
          metrics: [{ label: '⚖ Нагрузка', value: 2.74, max: 28, tone: 'Accent', note: 'в пределах нормы' }],
          hints: [{ title: 'Подсказка', text: 'Документы можно читать через книги.', tone: 'Warning' }],
          list: ['Переносимые вещи'],
          cards: [
            {
              title: 'Запечатанное письмо',
              subtitle: 'Документ',
              summary: 'Письмо с незнакомой печатью.',
              icon: 'inventory',
              badges: [{ label: 'документ', tone: 'Accent', icon: 'book' }],
              media: null,
              facts: [{ label: 'Состояние', value: 'в порядке' }],
              metrics: [],
              hints: [],
              list: [],
              nested: [],
              cards: []
            }
          ],
          sections: [
            {
              id: 'items',
              title: 'Предметы',
              summary: '2 предмета в инвентаре.',
              icon: 'inventory',
              collectionLabel: '2 объекта в разделе',
              collapsible: true,
              initiallyExpanded: true,
              facts: [{ label: 'Количество', value: '2' }],
              metrics: [],
              hints: [],
              list: [],
              cards: [
                {
                  title: 'Руническая перчатка',
                  subtitle: 'Артефакт',
                  summary: 'Откликается на владельца.',
                  icon: 'inventory',
                  badges: [],
                  media: null,
                  facts: [{ label: 'Прочность', value: '95%' }],
                  metrics: [],
                  hints: [],
                  list: [],
                  nested: [],
                  cards: []
                }
              ],
              blocks: []
            }
          ]
        }
      ]
    }));

    const dossier = sanitized.blocks[0];
    expect(dossier?.kind).toBe('entityDossier');
    if (dossier?.kind !== 'entityDossier') throw new Error('Expected entity dossier');

    expect(dossier.facts).toHaveLength(1);
    expect(dossier.metrics).toHaveLength(1);
    expect(dossier.hints).toHaveLength(1);
    expect(dossier.list).toHaveLength(1);
    expect(dossier.cards).toHaveLength(1);
    expect(dossier.sections).toHaveLength(1);
    expect(dossier.sections[0].facts).toHaveLength(1);
    expect(dossier.sections[0].cards).toHaveLength(1);

    const html = renderCommandResult(sanitized);
    expect(html).toContain('dossier-section');
    expect(html).toContain('entity-card');
    expect(html).toContain('Руническая перчатка');
    expect(html).toContain('Запечатанное письмо');
    expect(html).toContain('Деньги');
    expect(html).toContain('Нагрузка');
    expect(html).not.toContain('💰');
    expect(html).not.toContain('⚖');
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

  it('renders semantic entity dossiers with header, badges, media, and sections', () => {
    const blocks: UiBlock[] = [
      {
        kind: 'entityDossier',
        entityType: 'npc',
        title: 'Мирра Ключница',
        subtitle: 'Смотрительница архива',
        summary: 'Знает, кто входил в покои после полуночи.',
        badges: [
          { label: 'Союзник', tone: 'Success', icon: 'relation' },
          { label: 'Архив', tone: 'Accent', icon: 'archive' }
        ],
        media: {
          kind: 'image',
          title: 'Портрет Мирры',
          url: '/api/media/npc-mirra',
          mediaId: 'npc-mirra',
          relativePath: '',
          altText: 'Портрет Мирры Ключницы',
          contentType: 'image/png',
          length: 42,
          modifiedAtUtc: '2026-06-22T00:00:00Z'
        },
        sections: [
          {
            id: 'skills',
            title: 'Навыки',
            summary: 'Полезны при расследовании письма.',
            icon: 'skills',
            collapsible: true,
            initiallyExpanded: true,
            blocks: [
              { kind: 'list', ordered: false, items: ['Архивная память', 'Тихий шаг'] }
            ]
          }
        ]
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html).toContain('entity-dossier');
    expect(html).toContain('dossier-layout');
    expect(html).toContain('dossier-header');
    expect(html).toContain('badge" data-tone="success"');
    expect(html).toContain('Мирра Ключница');
    expect(html).toContain('Смотрительница архива');
    expect(html).toContain('Знает, кто входил в покои после полуночи.');
    expect(html).toContain('src="/api/media/npc-mirra"');
    expect(html).toContain('Навыки');
    expect(html).toContain('Архивная память');
    expect(html).not.toContain('<table>');
  });

  it('renders entity media previews as accessible lightbox triggers with safe image metadata', () => {
    const blocks: UiBlock[] = [
      {
        kind: 'entityDossier',
        entityType: 'npc',
        title: 'Мирра Ключница',
        subtitle: 'Смотрительница архива',
        summary: 'Знает, кто входил в покои после полуночи.',
        badges: [],
        media: {
          kind: 'image',
          title: 'Портрет Мирры',
          url: '/api/media/npc-mirra',
          mediaId: 'npc-mirra',
          relativePath: 'images/npcs/mirra.png',
          altText: 'Портрет Мирры Ключницы',
          contentType: 'image/png',
          length: 2048,
          modifiedAtUtc: '2026-06-22T00:00:00Z'
        },
        facts: [],
        metrics: [],
        hints: [],
        list: [],
        cards: [],
        sections: []
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);
    const source = readSource('src', 'components', 'BlockRenderer.tsx');

    expect(html).toContain('class="media-preview"');
    expect(html).toContain('aria-haspopup="dialog"');
    expect(html).toContain('aria-label="Открыть изображение: Портрет Мирры"');
    expect(html).toContain('alt="Портрет Мирры Ключницы"');
    expect(html).toContain('media-preview__meta');
    expect(html).toContain('PNG');
    expect(html).toContain('2 КБ');
    expect(html).not.toContain('images/npcs/mirra.png');
    expect(source).toContain('dialog.showModal()');
    expect(source).toContain('onClose={() => setOpen(false)}');
    expect(source).toContain('aria-label="Закрыть изображение"');
  });

  it('does not render an empty media box when an entity has only incomplete media metadata', () => {
    const blocks: UiBlock[] = [
      {
        kind: 'entityDossier',
        entityType: 'item',
        title: 'Пустой медиа-слот',
        subtitle: 'Предмет',
        summary: 'У предмета пока нет изображения.',
        badges: [],
        media: {
          kind: 'image',
          title: 'Недоступное изображение',
          url: '',
          mediaId: 'missing-media',
          relativePath: 'images/items/missing.png',
          altText: 'Недоступное изображение предмета',
          contentType: 'image/png',
          length: 0,
          modifiedAtUtc: '2026-06-22T00:00:00Z'
        },
        facts: [],
        metrics: [],
        hints: [],
        list: [],
        cards: [],
        sections: []
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html).toContain('Пустой медиа-слот');
    expect(html).not.toContain('media-preview');
    expect(html).not.toContain('Недоступное изображение предмета');
    expect(html).not.toContain('images/items/missing.png');
  });

  it('renders entity dossiers through the accepted prototype layout instead of the legacy generic wrapper', () => {
    const itemCards = Array.from({ length: 10 }, (_, index) => ({
      kind: 'entityDossier' as const,
      entityType: 'inventory-item-summary',
      title: `Предмет ${index + 1}`,
      subtitle: index % 2 === 0 ? 'Документ' : 'Артефакт',
      summary: `Краткое описание предмета ${index + 1}.`,
      badges: [{ label: index % 2 === 0 ? 'документ' : 'артефакт', tone: 'Accent' as const, icon: 'inventory' }],
      media: null,
      sections: [
        {
          id: 'facts',
          title: 'Кратко',
          summary: '',
          icon: 'inventory',
          collapsible: true,
          initiallyExpanded: false,
          blocks: [
            {
              kind: 'keyValueGrid' as const,
              items: [
                { key: 'Прочность', value: '100%' },
                { key: 'Состояние', value: 'в порядке' }
              ]
            }
          ]
        }
      ]
    }));

    const blocks: UiBlock[] = [
      {
        kind: 'entityDossier',
        entityType: 'inventory',
        title: 'Инвентарь',
        subtitle: 'Снаряжение и переносимые вещи',
        summary: 'Здесь собраны ресурсы, экипировка и предметы, доступные персонажу.',
        badges: [{ label: '10 предметов', tone: 'Accent', icon: 'inventory' }],
        media: {
          kind: 'image',
          title: 'Архивное изображение',
          url: '/api/media/inventory',
          mediaId: 'inventory',
          relativePath: '',
          altText: 'Содержимое дорожной сумки',
          contentType: 'image/png',
          length: 42,
          modifiedAtUtc: '2026-06-22T00:00:00Z'
        },
        sections: [
          {
            id: 'overview',
            title: 'Сводка',
            summary: 'Общая нагрузка и переносимые ценности.',
            icon: 'inventory',
            collapsible: true,
            initiallyExpanded: true,
            blocks: [
              {
                kind: 'keyValueGrid',
                items: [
                  { key: 'Нагрузка', value: '2.74 / 28' },
                  { key: 'Деньги', value: '500' }
                ]
              }
            ]
          },
          {
            id: 'items',
            title: 'Предметы',
            summary: '10 предметов в инвентаре.',
            icon: 'inventory',
            collapsible: true,
            initiallyExpanded: true,
            blocks: itemCards
          }
        ]
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html).toContain('dossier-layout');
    expect(html).toContain('dossier-main');
    expect(html).toContain('dossier-toc');
    expect(html).toContain('dossier-header');
    expect(html).toContain('dossier-title-row');
    expect(html).toContain('dossier-icon');
    expect(html).toContain('badge-row');
    expect(html).toContain('badge" data-tone="accent"');
    expect(html).toContain('dossier-media');
    expect(html).toContain('media-preview');
    expect(html).toContain('dossier-body');
    expect(html).toContain('dossier-section');
    expect(html).toContain('dossier-section__summary');
    expect(html).toContain('collapse-pill');
    expect(html).toContain('fact-grid');
    expect(html).toContain('fact-card');
    expect(html).toContain('collection-browser');
    expect(html).toContain('collection-featured-card');
    expect(html).toContain('collection-search');
    expect(html).toContain('collection-filter');
    expect(html).toContain('collection-workbench');
    expect(html).toContain('collection-list-item');
    expect(html).toContain('collection-detail-panel');
    expect(html).not.toContain('entity-dossier__header');
    expect(html).not.toContain('entity-dossier__sections');
  });

  it('renders requested entity collections as a selector even when the list is short', () => {
    const cards = ['Мариус де Гран', 'Ворон Рилль', 'Ирен Соль'].map((title) => ({
      title,
      subtitle: 'Персонаж мира',
      summary: 'Связан с ночным письмом в покоях виконта.',
      icon: 'npc',
      badges: [{ label: 'в кадре', tone: 'Accent' as const, icon: 'npc' }],
      media: null,
      facts: [
        { label: 'Роль', value: 'участник сцены' },
        { label: 'Отношение', value: 'настороженное' }
      ],
      metrics: [],
      hints: [],
      list: [],
      nested: [],
      cards: []
    }));

    const html = renderToStaticMarkup(<BlockList blocks={[{
      kind: 'entityDossier',
      entityType: 'npc-collection',
      title: 'Персонажи',
      subtitle: 'Люди в сцене',
      summary: 'Выберите персонажа, чтобы рассмотреть его досье.',
      badges: [{ label: '3 персонажа', tone: 'Accent', icon: 'npc' }],
      media: null,
      facts: [],
      metrics: [],
      hints: [],
      list: [],
      cards: [],
      sections: [
        {
          id: 'npcs',
          title: 'Персонажи',
          summary: 'Кто сейчас связан со сценой.',
          icon: 'npc',
          collectionLabel: '3 персонажа',
          presentation: 'collection',
          collapsible: true,
          initiallyExpanded: true,
          facts: [],
          metrics: [],
          hints: [],
          list: [],
          cards,
          blocks: []
        }
      ]
    }]} />);

    expect(html).toContain('collection-browser');
    expect(html).toContain('collection-list-item');
    expect(html).toContain('collection-detail-panel');
    expect(html).toContain('Мариус де Гран');
    expect(html).toContain('Персонажи');
    expect(html).not.toContain('Большая коллекция');
    expect(html).not.toContain('card-grid');
  });

  it('shows a direct open action inside entity cards when a matching command action exists', () => {
    const html = renderCommandResult(baseCommandResult({
      blocks: [
        {
          kind: 'entityDossier',
          entityType: 'npc-collection',
          title: 'Персонажи',
          subtitle: 'Люди в сцене',
          summary: 'Выберите персонажа, чтобы рассмотреть его досье.',
          badges: [],
          media: null,
          facts: [],
          metrics: [],
          hints: [],
          list: [],
          cards: [],
          sections: [
            {
              id: 'npcs',
              title: 'Персонажи',
              summary: 'Кто сейчас связан со сценой.',
              icon: 'npc',
              collectionLabel: '1 персонаж',
              presentation: 'collection',
              collapsible: true,
              initiallyExpanded: true,
              facts: [],
              metrics: [],
              hints: [],
              list: [],
              cards: [
                {
                  title: 'Мариус де Гран',
                  subtitle: 'Персонаж мира',
                  summary: 'Старший дворецкий и первый свидетель ночных странностей.',
                  icon: 'npc',
                  badges: [],
                  media: null,
                  facts: [],
                  metrics: [],
                  hints: [],
                  list: [],
                  nested: [],
                  cards: []
                }
              ],
              blocks: []
            }
          ]
        }
      ],
      actions: [
        {
          id: 'npc-open-marius',
          label: 'Открыть отдельно: Мариус де Гран',
          command: '/нпс персонаж "Мариус де Гран"',
          style: 'Secondary',
          requiresConfirmation: false,
          payload: null
        }
      ]
    }));

    expect(html).toContain('entity-card__action-row');
    expect(html).toContain('Открыть отдельно: Мариус де Гран');
    expect(html).toContain('collection-detail-panel');
  });

  it('keeps shallow nested dossier sections open so selected entities show their real data immediately', () => {
    const html = renderToStaticMarkup(<BlockList blocks={[{
      kind: 'entityDossier',
      entityType: 'npc',
      title: 'Мариус де Гран',
      subtitle: 'Персонаж мира',
      summary: 'Досье персонажа.',
      badges: [],
      media: null,
      facts: [],
      metrics: [],
      hints: [],
      list: [],
      cards: [],
      sections: [
        {
          id: 'journal',
          title: 'Дневник / мысли',
          summary: 'Что персонаж думает о ночных событиях.',
          icon: 'npc',
          collectionLabel: '',
          presentation: '',
          collapsible: true,
          initiallyExpanded: true,
          facts: [
            { label: 'Мысль', value: 'Печать на письме кажется знакомой.' },
            { label: 'Опасение', value: 'Кто-то вошёл в покои после полуночи.' }
          ],
          metrics: [],
          hints: [{ title: 'Зацепка', text: 'Стоит спросить о семейном архиве.', tone: 'Accent' }],
          list: ['Первым проверить записи караула.'],
          cards: [
            {
              title: 'Запись наблюдения',
              subtitle: 'Журнал',
              summary: 'Мариус помнит шаги у двери.',
              icon: 'archive',
              badges: [],
              media: null,
              facts: [{ label: 'Источник', value: 'ночной обход' }],
              metrics: [],
              hints: [],
              list: [],
              nested: [],
              cards: []
            }
          ],
          blocks: []
        }
      ]
    }]} />);

    expect(html).toContain('Печать на письме кажется знакомой.');
    expect(html).not.toContain('class="nested-card collapsible-card"');
  });

  it('does not repeat card facts again as anonymous list items inside dossier cards', () => {
    const html = renderToStaticMarkup(<BlockList blocks={[{
      kind: 'entityDossier',
      entityType: 'npc',
      title: 'Магистра Селена',
      subtitle: 'Наставница',
      summary: 'Досье персонажа.',
      badges: [],
      media: null,
      facts: [],
      metrics: [],
      hints: [],
      list: [],
      cards: [],
      sections: [
        {
          id: 'memory',
          title: 'Память / состояния',
          summary: 'Что персонаж помнит и какие состояния на него влияют.',
          icon: 'memory',
          collectionLabel: '',
          presentation: '',
          collapsible: true,
          initiallyExpanded: true,
          facts: [],
          metrics: [],
          hints: [],
          list: [],
          cards: [
            {
              title: 'Карта судьбы',
              subtitle: 'Память / состояния',
              summary: '',
              icon: 'memory',
              badges: [],
              media: null,
              facts: [
                { label: 'Название карты', value: 'Холодная милость наставника' },
                { label: 'Описание', value: 'Селена способна закрыть опасную ошибку ученика, но попросит за это трудную правду.' }
              ],
              metrics: [],
              hints: [],
              list: [
                'Холодная милость наставника',
                'Селена способна закрыть опасную ошибку ученика, но попросит за это трудную правду.',
                'Уникальная заметка остаётся видимой.'
              ],
              nested: [],
              cards: []
            }
          ],
          blocks: []
        }
      ]
    }]} />);

    expect(countOccurrences(html, 'Холодная милость наставника')).toBe(1);
    expect(countOccurrences(html, 'Селена способна закрыть опасную ошибку ученика')).toBe(1);
    expect(html).toContain('Уникальная заметка остаётся видимой.');
  });

  it('breaks structured fact values into readable localized rows instead of one semicolon line', () => {
    const html = renderToStaticMarkup(<BlockList blocks={[{
      kind: 'entityDossier',
      entityType: 'characteristics',
      title: 'Характеристики',
      subtitle: 'Расчётные показатели',
      summary: '',
      badges: [],
      media: null,
      facts: [
        {
          label: 'final',
          value: 'strength: 5; dexterity: 7; constitution: 6; intelligence: 13; wisdom: 10; perception: 11; luck: 7'
        }
      ],
      metrics: [],
      hints: [],
      list: [],
      cards: [],
      sections: []
    }]} />);

    expect(html).toContain('structured-fact-list');
    expect(html).toContain('<dt>Сила</dt>');
    expect(html).toContain('<dd>5</dd>');
    expect(html).toContain('<dt>Ловкость</dt>');
    expect(html).toContain('<dt>Восприятие</dt>');
    expect(html).not.toContain('strength: 5; dexterity: 7');
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
    expect(html).toContain('map-node-hit-area');
    expect(html).toContain('map-node-focus-ring');
    expect(html).toContain('Известный выход');
    expect(html).toContain('Комната с тяжёлыми шторами.');
    expect(html).not.toContain('browser-atlas-texture');
  });

  it('renders a player-facing full-screen control for browser maps', () => {
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
              imageUrl: '',
              imageAltText: ''
            }
          ],
          links: [],
          regions: []
        }
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html).toContain('map-fullscreen-button');
    expect(html).toContain('На весь экран');
    expect(html).toContain('Открыть карту на весь экран');
  });

  it('wires browser maps for wheel zoom and left-button drag panning', () => {
    const mapBlock = readSource('src', 'components', 'map', 'MapAtlas.tsx');
    const mapAtlasCss = readSource('src', 'styles', 'map-atlas.css');

    expect(mapBlock).toContain('onWheel={handleWheel}');
    expect(mapBlock).toContain("addEventListener('wheel', handleNativeWheel");
    expect(mapBlock).toContain('passive: false');
    expect(mapBlock).toContain('onPointerDown={handlePointerDown}');
    expect(mapBlock).toContain('onPointerMove={handlePointerMove}');
    expect(mapBlock).toContain('onPointerUp={endPan}');
    expect(mapBlock).toContain('event.currentTarget.setPointerCapture');
    expect(mapBlock).toContain('buttons !== 1');
    expect(mapBlock).toContain('map-fullscreen-dialog');
    expect(mapBlock).toContain('map-fullscreen-close-button');
    expect(mapAtlasCss).toContain('.map-atlas-frame--fullscreen');
    expect(mapAtlasCss).toContain('.map-atlas-frame--panning');
    expect(mapAtlasCss).toContain('.map-fullscreen-dialog');
    expect(mapAtlasCss).toContain('.map-fullscreen-close-button');
  });

  it('renders the selected map node after other nodes so it stays on top visually', () => {
    const blocks: UiBlock[] = [
      {
        kind: 'map',
        title: 'Карта',
        map: {
          schemaVersion: 1,
          realm: 'Mortal World',
          title: 'Карта смертного мира',
          currentNodeId: 'loc_selected',
          layers: [{ id: 'world', label: 'Мир', isDefault: true }],
          zLevels: [{ z: 0, label: 'земля' }],
          nodes: [
            {
              id: 'loc_selected',
              label: 'Выбранная башня',
              type: 'indoor',
              x: 0,
              y: 0,
              z: 0,
              layer: 'world',
              isCurrent: true,
              ownerFactionId: '',
              ownerFactionName: '',
              influence: {},
              details: [],
              isPlaceholder: false,
              imageUrl: '',
              imageAltText: ''
            },
            {
              id: 'loc_other',
              label: 'Соседний двор',
              type: 'outdoor',
              x: 0.2,
              y: 0.1,
              z: 0,
              layer: 'world',
              isCurrent: false,
              ownerFactionId: '',
              ownerFactionName: '',
              influence: {},
              details: [],
              isPlaceholder: false,
              imageUrl: '',
              imageAltText: ''
            }
          ],
          links: [],
          regions: []
        }
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);

    expect(html.indexOf('Локация: Соседний двор')).toBeLessThan(html.indexOf('Локация: Выбранная башня'));
  });

  it('renders a player-facing location selector for browser maps', () => {
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
              label: 'Покои виконта',
              type: 'indoor',
              x: 0,
              y: 0,
              z: 0,
              layer: 'world',
              isCurrent: true,
              ownerFactionId: '',
              ownerFactionName: '',
              influence: {},
              details: [],
              isPlaceholder: false,
              imageUrl: '',
              imageAltText: ''
            },
            {
              id: 'loc_library',
              label: 'Семейная библиотека',
              type: 'indoor',
              x: 3,
              y: 0,
              z: 0,
              layer: 'world',
              isCurrent: false,
              ownerFactionId: '',
              ownerFactionName: '',
              influence: {},
              details: [],
              isPlaceholder: false,
              imageUrl: '',
              imageAltText: ''
            }
          ],
          links: [],
          regions: []
        }
      }
    ];

    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);
    const mapBlock = readSource('src', 'components', 'map', 'MapAtlas.tsx');

    expect(html).toContain('map-location-selector');
    expect(html).toContain('Список локаций');
    expect(html).toContain('aria-label="Выбрать локацию на карте"');
    expect(html).toContain('Покои виконта');
    expect(html).toContain('Семейная библиотека');
    expect(html).toContain('aria-current="true"');
    expect(mapBlock).toContain('onClick={() => setSelectedNodeId(node.id)}');
  });

  it('uses an SVG-local focus halo for map nodes instead of a browser outline rectangle', () => {
    const mapBlock = readSource('src', 'components', 'map', 'MapAtlas.tsx');
    const mapAtlasCss = readSource('src', 'styles', 'map-atlas.css');

    expect(mapBlock).toContain('className="map-node-hit-area"');
    expect(mapBlock).toContain('className="map-node-focus-ring"');
    expect(mapAtlasCss).toContain('.map-node:focus-visible .map-node-focus-ring');
    expect(mapAtlasCss).not.toContain('.map-node:focus-visible {\n  outline: 2px');
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

function countOccurrences(source: string, fragment: string): number {
  return source.split(fragment).length - 1;
}
