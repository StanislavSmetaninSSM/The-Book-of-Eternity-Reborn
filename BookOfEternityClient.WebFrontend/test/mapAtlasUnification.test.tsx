import { readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { MapNodeDto, MapViewDto, UiBlock } from '../src/api/contracts';
import { BlockList } from '../src/components/BlockRenderer';
import {
  buildViewBox,
  describeMapNode,
  describeNodeStatus,
  groupNodesForDrawer,
  isContested,
  MAP_NODE_HIT_RADIUS,
  nodeMatchesQuery,
  orderMapNodesForDisplay,
  resolveNodeDisplayGeometry
} from '../src/components/map/mapAtlasLogic';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8').replace(/\r\n/g, '\n');
}

describe('unified MapAtlas — bring-to-front ordering (overlap fix)', () => {
  const a = node('a', { isCurrent: false });
  const b = node('b', { isCurrent: false });
  const current = node('cur', { isCurrent: true });
  const placeholder = node('ph', { isPlaceholder: true });
  const contested = node('ct', { influence: { f1: 60, f2: 55 } });

  it('places the hovered node last so it renders on top of its neighbours', () => {
    const ordered = orderMapNodesForDisplay([a, b, current], {
      hoveredNodeId: 'a',
      selectedNodeId: 'cur',
      currentNodeId: 'cur'
    });
    expect(ordered[ordered.length - 1].id).toBe('a');
  });

  it('falls back to selected > current > contested > normal > placeholder when nothing is hovered', () => {
    const ordered = orderMapNodesForDisplay([placeholder, a, contested, current], {
      selectedNodeId: 'cur-not-present',
      currentNodeId: 'cur'
    });
    const ids = ordered.map((n) => n.id);
    // ascending priority: placeholder(0) < normal(1) < contested(2) < current(3)
    expect(ids).toEqual(['ph', 'a', 'ct', 'cur']);
  });

  it('treats a hovered placeholder as higher priority than a normal selected node', () => {
    const ordered = orderMapNodesForDisplay([a, placeholder], {
      hoveredNodeId: 'ph',
      selectedNodeId: 'a'
    });
    expect(ordered[ordered.length - 1].id).toBe('ph');
  });

  it('isContested matches the C# rule (>=2 factions at >=25 within 10)', () => {
    expect(isContested(node('x', { influence: { f1: 60, f2: 55 } }))).toBe(true);
    expect(isContested(node('x', { influence: { f1: 60, f2: 30 } }))).toBe(false);
    expect(isContested(node('x', { influence: { f1: 20, f2: 19 } }))).toBe(false); // both < 25
  });
});

describe('unified MapAtlas — drawer navigation + search', () => {
  const map: MapViewDto = {
    schemaVersion: 1,
    realm: 'Mortal World',
    title: 'Карта смертного мира',
    currentNodeId: 'loc_a',
    layers: [
      { id: 'world', label: 'Мир', isDefault: true },
      { id: 'underground', label: 'Подземелье', isDefault: false }
    ],
    zLevels: [
      { z: 0, label: 'земля' },
      { z: -1, label: 'подземелье' }
    ],
    nodes: [
      node('loc_a', { layer: 'world', z: 0 }),
      node('loc_b', { layer: 'world', z: 0 }),
      node('cave_1', { layer: 'underground', z: -1, type: 'dungeon' })
    ],
    links: [],
    regions: []
  };

  it('groups every node by layer then by Z-level for the drawer', () => {
    const groups = groupNodesForDrawer(map);
    expect(groups.map((g) => g.layer.id)).toEqual(['world', 'underground']);
    const world = groups[0];
    expect(world.levels.map((l) => l.zLevel.z)).toEqual([0, -1]);
    const surfaceLevel = world.levels[0];
    expect(surfaceLevel.nodes.map((n) => n.id)).toEqual(['loc_a', 'loc_b']);
    const caves = groups[1].levels[1];
    expect(caves.nodes.map((n) => n.id)).toEqual(['cave_1']);
  });

  it('matches by label, type, and faction; folds accents and case (Russian + English)', () => {
    expect(nodeMatchesQuery(node('x', { label: 'Гостиная' }), 'гостин')).toBe(true);
    expect(nodeMatchesQuery(node('x', { label: 'Гостиная', type: 'indoor' }), 'INDOOR')).toBe(true);
    expect(nodeMatchesQuery(node('x', { ownerFactionName: 'Орден Серебра' }), 'орден')).toBe(true);
    expect(nodeMatchesQuery(node('x', { label: 'Ёлкин лес' }), 'елкин')).toBe(true); // ё→е fold
    expect(nodeMatchesQuery(node('x', { label: 'Гостиная' }), 'катакомбы')).toBe(false);
    expect(nodeMatchesQuery(node('x', { label: 'Что-то' }), '')).toBe(true); // empty query matches all
  });

  it('describeNodeStatus covers current/placeholder/faction/contested/normal', () => {
    expect(describeNodeStatus(node('x', { isCurrent: true })).kind).toBe('current');
    expect(describeNodeStatus(node('x', { isPlaceholder: true })).kind).toBe('placeholder');
    expect(describeNodeStatus(node('x', { ownerFactionId: 'f1' })).kind).toBe('faction');
    expect(describeNodeStatus(node('x', { influence: { f1: 60, f2: 55 } })).kind).toBe('contested');
    expect(describeNodeStatus(node('x', {})).kind).toBe('normal');
  });

  it('describes faction influence by name and score instead of a generic status', () => {
    const status = describeNodeStatus(
      node('gate', {
        ownerFactionId: 'merchant_guild',
        ownerFactionName: 'Купеческая гильдия Этернии',
        influence: { merchant_guild: 55 }
      })
    );

    expect(status.kind).toBe('faction');
    expect(status.label).toContain('Купеческая гильдия Этернии');
    expect(status.label).toContain('55');
    expect(status.label).not.toBe('Под контролем фракции');
  });

  it('localizes selected-node detail keys and common technical values', () => {
    const details = describeMapNode(
      node('gate', {
        type: 'outdoor',
        ownerFactionId: 'merchant_guild',
        ownerFactionName: 'Купеческая гильдия Этернии',
        influence: { merchant_guild: 55 },
        details: [
          { key: 'Тип', value: 'outdoor' },
          { key: 'biome', value: 'Urban' },
          { key: 'Выходы', value: 'Коридор поместья (Safe)' },
          { key: 'Контроль фракций', value: 'Купеческая гильдия Этернии: Economic 55' }
        ]
      })
    );
    const byKey = new Map(details.map((item) => [item.key, item.value]));

    expect(byKey.get('Тип')).toBe('на открытом воздухе');
    expect(byKey.get('Биом')).toBe('городская среда');
    expect(byKey.get('Выходы')).toContain('безопасный');
    expect(byKey.get('Влияние фракций')).toContain('экономическое влияние');
    expect(details.map((item) => item.value).join(' ')).not.toMatch(/\b(outdoor|Urban|Safe|Economic)\b/);
  });

  it('buildViewBox frames nodes with the asymmetric padding used to keep labels unclipped', () => {
    const vb = buildViewBox([node('a', { x: 0, y: 0 })]);
    // +9 extra on the right so labels above nodes aren't clipped (preserved invariant).
    expect(vb.width).toBeGreaterThanOrEqual(12);
    expect(vb.x).toBe(-3);
    expect(vb.y).toBe(-4);
  });

  it('spreads nodes that share exact coordinates so exits and created locations stay clickable', () => {
    const display = resolveNodeDisplayGeometry([
      node('created_location', { x: 0, y: 1 }),
      node('known_exit', { x: 0, y: 1, isPlaceholder: true }),
      node('current_room', { x: 0, y: 0, isCurrent: true })
    ]);

    const created = display.find((item) => item.node.id === 'created_location');
    const exit = display.find((item) => item.node.id === 'known_exit');
    const current = display.find((item) => item.node.id === 'current_room');

    expect(created).toBeDefined();
    expect(exit).toBeDefined();
    expect(current).toBeDefined();
    expect(created!.x).not.toBe(exit!.x);
    expect(created!.y).toBe(exit!.y);
    expect(distance(created!, exit!)).toBeGreaterThan(MAP_NODE_HIT_RADIUS);
    expect(distance(exit!, current!)).toBeGreaterThan(MAP_NODE_HIT_RADIUS);
  });
});

describe('unified MapAtlas — component surfaces the overlap fix + drawer', () => {
  const blocks: UiBlock[] = [
    {
      kind: 'map',
      title: 'Карта',
      map: mapWith([
        node('loc_a', { label: 'Покои виконта', isCurrent: true, x: 0, y: 0 }),
        node('loc_b', { label: 'Семейная библиотека', x: 3, y: 0 }),
        node('loc_c', { label: 'Запертая галерея', isPlaceholder: true, x: 0.2, y: 0.1 })
      ])
    }
  ];

  it('renders cartouche labels, the bring-to-front classes, and the data hook for ordering', () => {
    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);
    // New, larger label class with the parchment cartouche halo (overlap fix).
    expect(html).toContain('map-node-label');
    // Selected/current nodes get the bring-to-front classes consumed by CSS.
    expect(html).toContain('map-node--selected');
    // Every node carries a status hook the CSS and ordering logic key on.
    expect(html).toContain('data-node-status=');
    // Placeholder nodes still render distinctly.
    expect(html).toContain('map-node--placeholder');
    // Bring-to-front invariant: the selected (current) node is the LAST <g>
    // in the SVG, so it paints on top of its neighbours. We confirm by index.
    const lastNodeBlockIdx = html.lastIndexOf('class="map-node ');
    // The last node block in paint order must be the current/selected one,
    // proving it is rendered last (on top) — the overlap fix.
    expect(html.slice(lastNodeBlockIdx)).toContain('map-node--current');
  });

  it('renders the collapsible location drawer with all locations and a search input', () => {
    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);
    expect(html).toContain('map-location-selector');
    expect(html).toContain('Список локаций');
    expect(html).toContain('Покои виконта');
    expect(html).toContain('Семейная библиотека');
    expect(html).toContain('Запертая галерея');
    expect(html).toContain('type="search"');
  });

  it('renders a current-node glow plus the political-overlay toggle', () => {
    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);
    expect(html).toContain('map-node-current-glow');
    expect(html).toContain('Влияние фракций');
  });

  it('does not render the old compass ornament over the atlas corner', () => {
    const html = renderToStaticMarkup(<BlockList blocks={blocks} />);
    expect(html).not.toContain('map-compass-rose');
    expect(html).not.toContain('Роза ветров');
  });
});

describe('unified MapAtlas — single source of truth (parity source-guard)', () => {
  // These assertions prevent a second renderer from reappearing. If either the
  // embedded React client path or the standalone IIFE entry drifts away from
  // the shared MapAtlas component, the build breaks here.
  it('embedded client renders MapAtlas via the BlockRenderer→MapBlock wrapper', () => {
    const blockRenderer = readSource('src', 'components', 'BlockRenderer.tsx');
    const wrapper = readSource('src', 'components', 'MapBlock.tsx');
    expect(wrapper).toContain("from './map/MapAtlas'");
    expect(wrapper).toContain('export function MapBlock');
    expect(blockRenderer).toContain("import { MapBlock } from './MapBlock'");
  });

  it('standalone IIFE entry mounts the SAME MapAtlas component', () => {
    const standalone = readSource('src', 'mapViewerStandalone.tsx');
    expect(standalone).toContain("import './styles/map-atlas.css'");
    expect(standalone).toContain("from './components/map/MapAtlas'");
    expect(standalone).toContain('MapAtlas');
    expect(standalone).toContain('variant: \'standalone\'');
    expect(standalone).toContain('window.BookOfEternityMap');
  });

  it('pure logic module is framework-agnostic and shared by both surfaces', () => {
    const logic = readSource('src', 'components', 'map', 'mapAtlasLogic.ts');
    // Hovered priority is the heart of the overlap fix.
    expect(logic).toContain('focus.hoveredNodeId && node.id === focus.hoveredNodeId');
    expect(logic).toContain('orderMapNodesForDisplay');
    expect(logic).toContain('groupNodesForDrawer');
    expect(logic).toContain('nodeMatchesQuery');
    // No React import in the shared logic — it is callable from any surface.
    expect(logic).not.toMatch(/from ['"]react['"]/);
  });

  it('map styling lives in one stylesheet consumed by both surfaces', () => {
    const atlas = readSource('src', 'styles', 'map-atlas.css');
    const styles = readSource('src', 'styles.css');
    expect(atlas).toContain('.map-node--hovered');
    expect(atlas).toContain('.map-node--selected');
    expect(atlas).toContain('.map-location-selector');
    expect(atlas).toContain('.map-toolbar select option');
    expect(atlas).toContain('color-scheme: dark');
    expect(styles).toContain("styles/map-atlas.css");
  });

  it('fullscreen atlas uses a themed fixed overlay instead of the native dialog backdrop', () => {
    const atlas = readSource('src', 'components', 'map', 'MapAtlas.tsx');
    const css = readSource('src', 'styles', 'map-atlas.css');
    expect(atlas).not.toContain('<dialog\n            className="map-fullscreen-dialog"');
    expect(atlas).toContain('role="dialog"');
    expect(atlas).toContain('aria-modal="true"');
    expect(css).not.toContain('.map-fullscreen-dialog::backdrop');
    expect(css).toContain('isolation: isolate');
    expect(css).toContain('--text-primary: #f4ead0');
    expect(css).toContain('--text-muted: #b8aa8a');
  });

  it('local web UI shell mounts the unified bundle, not a vanilla viewer', () => {
    const shell = readSource('public', 'local-web-ui-shell.html');
    expect(shell).toContain('BookOfEternityMap');
    expect(shell).toContain('BookOfEternityMap.mount');
    // The deleted vanilla viewer must not be referenced.
    expect(shell).not.toContain('BookOfEternityMapViewer.renderMapBlock');
    expect(shell).not.toContain('map-viewer.css');
  });
});

function node(id: string, overrides: Partial<MapNodeDto> = {}): MapNodeDto {
  return {
    id,
    label: overrides.label ?? id,
    type: overrides.type ?? '',
    x: overrides.x ?? 0,
    y: overrides.y ?? 0,
    z: overrides.z ?? 0,
    layer: overrides.layer ?? 'world',
    isCurrent: overrides.isCurrent ?? false,
    ownerFactionId: overrides.ownerFactionId ?? '',
    ownerFactionName: overrides.ownerFactionName ?? '',
    influence: overrides.influence ?? {},
    details: overrides.details ?? [],
    isPlaceholder: overrides.isPlaceholder ?? false,
    imageUrl: overrides.imageUrl ?? '',
    imageAltText: overrides.imageAltText ?? ''
  };
}

function mapWith(nodes: MapNodeDto[]): MapViewDto {
  return {
    schemaVersion: 1,
    realm: 'Mortal World',
    title: 'Карта смертного мира',
    currentNodeId: nodes[0]?.id ?? '',
    layers: [{ id: 'world', label: 'Мир', isDefault: true }],
    zLevels: [{ z: 0, label: 'земля' }],
    nodes,
    links: [],
    regions: []
  };
}

function distance(left: { x: number; y: number }, right: { x: number; y: number }): number {
  return Math.hypot(left.x - right.x, left.y - right.y);
}
