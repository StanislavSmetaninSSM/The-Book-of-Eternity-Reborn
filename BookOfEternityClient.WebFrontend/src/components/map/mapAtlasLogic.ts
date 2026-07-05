/**
 * Pure logic for the unified map atlas.
 *
 * Shared by the embedded React component (BlockRenderer → MapAtlas) and the
 * standalone IIFE bundle (console `/map` → map_viewer.html). Keeping this
 * logic in pure, framework-agnostic functions means both surfaces share one
 * implementation of: hover/selected bring-to-front ordering, contested-node
 * detection, node-status labelling, location-list filtering/search, and
 * viewport math. Drift between the two renderers is eliminated by design.
 */

import type {
  MapLayerDto,
  MapLinkDto,
  MapNodeDto,
  MapRegionDto,
  MapViewDto,
  MapZLevelDto
} from '../../api/contracts';

export interface ViewBoxParts {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface PanPoint {
  x: number;
  y: number;
}

export const DEFAULT_PAN: PanPoint = { x: 0, y: 0 };
export const MIN_VIEWBOX_SCALE = 0.35;
export const MAX_VIEWBOX_SCALE = 3.5;

export type NodeStatusKind =
  | 'current'
  | 'placeholder'
  | 'contested'
  | 'faction'
  | 'normal';

export interface NodeStatus {
  kind: NodeStatusKind;
  label: string;
}

export interface MapInteractionFocus {
  hoveredNodeId?: string;
  selectedNodeId?: string;
  currentNodeId?: string;
}

/**
 * Resolve the SVG `data-node-status` value for a node. Used both for CSS
 * styling hooks and for the side-list status caption.
 */
export function describeNodeStatus(node: MapNodeDto): NodeStatus {
  if (node.isCurrent) return { kind: 'current', label: 'Вы здесь' };
  if (node.isPlaceholder) return { kind: 'placeholder', label: 'Известный выход' };
  if (isContested(node)) return { kind: 'contested', label: 'Спорное влияние' };
  if (node.ownerFactionId || node.ownerFactionName) return { kind: 'faction', label: 'Под контролем фракции' };
  return { kind: 'normal', label: 'Открытая локация' };
}

/**
 * Contested influence = two or more factions at ≥25 influence whose top two
 * values are within 10 of each other. Mirrors the C# rule in
 * LocalMapViewService and the prior standalone viewer.
 */
export function isContested(node: MapNodeDto): boolean {
  const values = Object.values(node.influence ?? {})
    .map(Number)
    .filter((value) => value >= 25)
    .sort((a, b) => b - a);
  return values.length >= 2 && values[0] - values[1] <= 10;
}

/**
 * Determine whether the political halo should render for a node — any node
 * carrying faction ownership or influence. Kept in lockstep with the prior
 * standalone viewer's drawPoliticalOverlay gating.
 */
export function hasPoliticalSignal(node: MapNodeDto): boolean {
  return Boolean(
    node.ownerFactionId ||
      node.ownerFactionName ||
      Object.keys(node.influence ?? {}).length > 0
  );
}

/**
 * Render order priority. **Hovered > selected > current > contested >
 * normal > placeholder.** Drawing in ascending priority means the
 * hovered/selected node is drawn LAST and therefore appears on top of its
 * neighbours — the direct fix for the overlap complaint.
 */
export function mapNodeRenderPriority(
  node: MapNodeDto,
  focus: MapInteractionFocus
): number {
  if (focus.hoveredNodeId && node.id === focus.hoveredNodeId) return 5;
  if (focus.selectedNodeId && node.id === focus.selectedNodeId) return 4;
  if (node.isCurrent) return 3;
  if (isContested(node)) return 2;
  if (node.isPlaceholder) return 0;
  return 1;
}

export function orderMapNodesForDisplay(
  nodes: MapNodeDto[],
  focus: MapInteractionFocus
): MapNodeDto[] {
  return [...nodes].sort(
    (left, right) =>
      mapNodeRenderPriority(left, focus) - mapNodeRenderPriority(right, focus)
  );
}

/**
 * Group every node (across all Z/layer) by its layer, then by Z-level, for
 * the collapsible navigation drawer. Layers and Z-levels keep the order
 * declared on the MapViewDto so the drawer matches the toolbar selectors.
 */
export interface DrawerLayerGroup {
  layer: MapLayerDto;
  levels: Array<{ zLevel: MapZLevelDto; nodes: MapNodeDto[] }>;
}

export function groupNodesForDrawer(map: MapViewDto): DrawerLayerGroup[] {
  const layers = map.layers.length
    ? map.layers
    : [{ id: 'world', label: 'Мир', isDefault: true }];
  const zLevels = map.zLevels.length
    ? map.zLevels
    : [{ z: 0, label: 'земля' }];

  return layers.map((layer) => {
    const levelsForLayer = zLevels.map((zLevel) => {
      const nodes = (map.nodes ?? []).filter(
        (node) =>
          (node.layer || 'world') === layer.id && node.z === zLevel.z
      );
      return { zLevel, nodes };
    });
    return { layer, levels: levelsForLayer };
  });
}

export interface LocationListFilter {
  query: string;
}

/**
 * Match a node against a free-text query. Case-insensitive, accent-folded for
 * Russian/English. Matches against label, type, id, owner faction name, and
 * the detail values. Returns true for an empty query (no filtering).
 */
export function nodeMatchesQuery(
  node: MapNodeDto,
  query: string
): boolean {
  const normalized = normalizeQuery(query);
  if (!normalized) return true;

  const haystack = normalizeQuery(
    [
      node.label,
      node.type,
      node.id,
      node.ownerFactionName,
      node.ownerFactionId,
      ...(node.details ?? []).map((detail) => `${detail.key} ${detail.value}`)
    ].join(' ')
  );
  return haystack.includes(normalized);
}

function normalizeQuery(value: string): string {
  return String(value ?? '')
    .toLocaleLowerCase('ru-RU')
    .replace(/ё/g, 'е')
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * Compute the SVG viewBox that frames the given nodes with a comfortable
 * margin. Mirrors the prior React implementation's asymmetric padding
 * (extra room on the +x side) so labels above nodes are not clipped.
 */
export function buildViewBox(nodes: MapNodeDto[]): ViewBoxParts {
  if (nodes.length === 0) return { x: -20, y: -20, width: 40, height: 40 };

  const xs = nodes.map((node) => node.x);
  const ys = nodes.map((node) => -node.y);
  const minX = Math.min(...xs) - 3;
  const maxX = Math.max(...xs) + 9;
  const minY = Math.min(...ys) - 4;
  const maxY = Math.max(...ys) + 4;
  return {
    x: minX,
    y: minY,
    width: Math.max(12, maxX - minX),
    height: Math.max(12, maxY - minY)
  };
}

export function scaleViewBox(
  viewBox: ViewBoxParts,
  zoom: number,
  pan: PanPoint
): ViewBoxParts {
  const nextWidth = viewBox.width * zoom;
  const nextHeight = viewBox.height * zoom;
  return {
    x: viewBox.x + (viewBox.width - nextWidth) / 2 + pan.x,
    y: viewBox.y + (viewBox.height - nextHeight) / 2 + pan.y,
    width: nextWidth,
    height: nextHeight
  };
}

export function formatViewBox(viewBox: ViewBoxParts): string {
  return [
    formatNumber(viewBox.x),
    formatNumber(viewBox.y),
    formatNumber(viewBox.width),
    formatNumber(viewBox.height)
  ].join(' ');
}

function formatNumber(value: number): string {
  return Number(value.toFixed(3)).toString();
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

export interface RegionCircle {
  cx: number;
  cy: number;
  radius: number;
}

/**
 * Bounding circle for a political region. Used to draw the dashed halo and
 * the faction label.
 */
export function regionCircle(nodes: MapNodeDto[]): RegionCircle {
  const xs = nodes.map((node) => node.x);
  const ys = nodes.map((node) => -node.y);
  const cx = xs.reduce((sum, value) => sum + value, 0) / xs.length;
  const cy = ys.reduce((sum, value) => sum + value, 0) / ys.length;
  const radius = Math.max(
    2.2,
    ...nodes.map((node) => Math.hypot(node.x - cx, -node.y - cy) + 1.6)
  );
  return { cx, cy, radius };
}

/**
 * Resolve which Z and layer a node should be shown on. Falls back to the
 * canonical defaults when the node lacks explicit values.
 */
export function resolveNodeLayerZ(node: MapNodeDto): { layer: string; z: number } {
  return { layer: node.layer || 'world', z: node.z };
}

/**
 * Map a location `type` (city/dungeon/cave/gate/indoor/outdoor/...) to a
 * short decorative glyph. Pure data → glyph mapping, no DTO change.
 */
export function nodeGlyph(type: string | null | undefined): string {
  const normalized = String(type ?? '').trim().toLocaleLowerCase('ru-RU');
  if (normalized.includes('city') || normalized.includes('город')) return '⌂';
  if (normalized.includes('gate') || normalized.includes('врат')) return '⌖';
  if (normalized.includes('dungeon') || normalized.includes('подземел')) return '⊻';
  if (normalized.includes('cave') || normalized.includes('пещер')) return '◓';
  if (normalized.includes('forest') || normalized.includes('лес')) return '♣';
  if (normalized.includes('water') || normalized.includes('вод') || normalized.includes('речк') || normalized.includes('озёр') || normalized.includes('озер')) return '≈';
  if (normalized.includes('mountain') || normalized.includes('гор')) return '△';
  if (normalized.includes('indoor') || normalized.includes('дом') || normalized.includes('внутр')) return '▦';
  return '◈';
}

export interface ResolvedMapLink {
  link: MapLinkDto;
  source: MapNodeDto;
  target: MapNodeDto;
}

/**
 * Filter the map's links to only those whose endpoints are both visible in
 * the current node set. Used by both renderers.
 */
export function resolveVisibleLinks(
  links: MapLinkDto[],
  nodeById: Map<string, MapNodeDto>
): ResolvedMapLink[] {
  const result: ResolvedMapLink[] = [];
  for (const link of links) {
    const source = nodeById.get(link.sourceNodeId);
    const target = nodeById.get(link.targetNodeId);
    if (!source || !target) continue;
    result.push({ link, source, target });
  }
  return result;
}

/**
 * Detail rows for the selected-node card. Prepends a "Состояние" row when the
 * node doesn't already carry one, appends faction and Z-level. Mirrors the
 * prior React describeMapNode so card content stays stable across surfaces.
 */
export function describeMapNode(
  node: MapNodeDto
): Array<{ key: string; value: string }> {
  const details = (node.details ?? [])
    .map((item) => ({
      key: String(item.key ?? 'Сведения'),
      value: String(item.value ?? '')
    }))
    .filter((item) => item.value);

  if (node.ownerFactionName || node.ownerFactionId) {
    details.push({
      key: 'Фракция',
      value: node.ownerFactionName || node.ownerFactionId
    });
  }

  if (!details.some((item) => item.key.toLocaleLowerCase('ru-RU') === 'состояние')) {
    details.unshift({
      key: 'Состояние',
      value: node.isPlaceholder
        ? 'Известный выход: подробная локация ещё не открыта.'
        : node.isCurrent
          ? 'Текущая локация'
          : 'Открытая локация'
    });
  }

  details.push({ key: 'Уровень', value: String(node.z) });
  return details;
}
