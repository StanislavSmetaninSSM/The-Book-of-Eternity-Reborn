import { useEffect, useMemo, useRef, useState } from 'react';
import type { PointerEvent as ReactPointerEvent, WheelEvent as ReactWheelEvent } from 'react';
import { createPortal } from 'react-dom';
import type { MapNodeDto, UiMapBlock } from '../api/contracts';
import { sanitizePlayerMessage, toPlayerFacingText } from '../utils/playerCopy';

interface MapBlockProps {
  block: UiMapBlock;
  variant?: 'full' | 'compact';
}

interface ViewBoxParts {
  x: number;
  y: number;
  width: number;
  height: number;
}

interface PanPoint {
  x: number;
  y: number;
}

const DEFAULT_PAN: PanPoint = { x: 0, y: 0 };
const MIN_VIEWBOX_SCALE = 0.35;
const MAX_VIEWBOX_SCALE = 3.5;

export function MapBlock({ block, variant = 'full' }: MapBlockProps) {
  const mapTitle = toPlayerFacingText(block.title || block.map.title, 'Карта');
  const mapSubtitle = toPlayerFacingText(block.map.title || block.map.realm, 'Атлас местности');
  const allNodes = block.map.nodes;
  const currentNodeSeed = allNodes.find((node) => node.id === block.map.currentNodeId) ?? allNodes.find((node) => node.isCurrent) ?? allNodes[0];
  const zLevels = block.map.zLevels.length > 0
    ? block.map.zLevels
    : [{ z: currentNodeSeed?.z ?? 0, label: 'земля' }];
  const layers = block.map.layers.length > 0
    ? block.map.layers
    : [{ id: currentNodeSeed?.layer || 'world', label: 'Мир', isDefault: true }];
  const defaultZ = currentNodeSeed?.z ?? zLevels[0]?.z ?? 0;
  const defaultLayer = currentNodeSeed?.layer || layers.find((layer) => layer.isDefault)?.id || layers[0]?.id || 'world';
  const defaultNodeId = currentNodeSeed?.id ?? '';
  const [selectedZ, setSelectedZ] = useState(defaultZ);
  const [selectedLayer, setSelectedLayer] = useState(defaultLayer);
  const [selectedNodeId, setSelectedNodeId] = useState(defaultNodeId);
  const [expandedImageNode, setExpandedImageNode] = useState<MapNodeDto | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState<PanPoint>(DEFAULT_PAN);
  const [isPanning, setIsPanning] = useState(false);
  const inlineAtlasFrameRef = useRef<HTMLDivElement | null>(null);
  const fullscreenAtlasFrameRef = useRef<HTMLDivElement | null>(null);
  const panStartRef = useRef<{
    pointerId: number;
    clientX: number;
    clientY: number;
    pan: PanPoint;
    viewBox: ViewBoxParts;
  } | null>(null);
  const mapResetKey = block.map;

  useEffect(() => {
    setSelectedZ(defaultZ);
    setSelectedLayer(defaultLayer);
    setSelectedNodeId(defaultNodeId);
    setExpandedImageNode(null);
    setIsFullscreen(false);
    setZoom(1);
    setPan(DEFAULT_PAN);
    setIsPanning(false);
    panStartRef.current = null;
  }, [defaultLayer, defaultNodeId, defaultZ, mapResetKey]);

  useEffect(() => {
    const handleNativeWheel = (event: WheelEvent) => {
      event.preventDefault();
      setZoom((value) => clamp(value * (event.deltaY < 0 ? 0.88 : 1.12), MIN_VIEWBOX_SCALE, MAX_VIEWBOX_SCALE));
    };
    const options = { passive: false };
    const frames = [inlineAtlasFrameRef.current, fullscreenAtlasFrameRef.current].filter(isHtmlDivElement);

    for (const frame of frames) {
      frame.addEventListener('wheel', handleNativeWheel, options);
    }

    return () => {
      for (const frame of frames) {
        frame.removeEventListener('wheel', handleNativeWheel);
      }
    };
  }, [isFullscreen]);

  useEffect(() => {
    if (!isFullscreen) return undefined;

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsFullscreen(false);
    };

    window.addEventListener('keydown', handleEscape);
    return () => window.removeEventListener('keydown', handleEscape);
  }, [isFullscreen]);

  const nodes = useMemo(
    () => allNodes.filter((node) => node.z === selectedZ && (node.layer || 'world') === selectedLayer),
    [allNodes, selectedLayer, selectedZ]
  );
  const nodeById = new Map(nodes.map((node) => [node.id, node] as const));
  const currentNode = nodes.find((node) => node.id === selectedNodeId) ??
    nodes.find((node) => node.id === block.map.currentNodeId) ??
    nodes.find((node) => node.isCurrent) ??
    nodes[0];
  const renderedNodes = useMemo(() => orderMapNodesForDisplay(nodes, currentNode?.id), [currentNode?.id, nodes]);
  const baseViewBox = useMemo(() => buildViewBox(nodes), [nodes]);
  const transformedViewBox = useMemo(() => scaleViewBox(baseViewBox, zoom, pan), [baseViewBox, pan, zoom]);
  const viewBox = formatViewBox(transformedViewBox);

  function resetViewport() {
    setZoom(1);
    setPan(DEFAULT_PAN);
  }

  function changeZoom(multiplier: number) {
    setZoom((value) => clamp(value * multiplier, MIN_VIEWBOX_SCALE, MAX_VIEWBOX_SCALE));
  }

  function handleWheel(event: ReactWheelEvent<HTMLDivElement>) {
    const wasPrevented = event.defaultPrevented;
    event.preventDefault();
    if (!wasPrevented) {
      changeZoom(event.deltaY < 0 ? 0.88 : 1.12);
    }
  }

  function handlePointerDown(event: ReactPointerEvent<HTMLDivElement>) {
    if (event.button !== 0) return;

    const target = event.target as Element;
    if (target.closest('.map-node, button, select, input, textarea')) return;

    event.preventDefault();
    try {
      event.currentTarget.setPointerCapture(event.pointerId);
    } catch {
      // Browser automation can dispatch synthetic pointer events without an active pointer.
    }
    panStartRef.current = {
      pointerId: event.pointerId,
      clientX: event.clientX,
      clientY: event.clientY,
      pan,
      viewBox: transformedViewBox
    };
    setIsPanning(true);
  }

  function handlePointerMove(event: ReactPointerEvent<HTMLDivElement>) {
    const start = panStartRef.current;
    if (!start || start.pointerId !== event.pointerId) return;

    if (event.buttons !== 1) {
      endPan(event);
      return;
    }

    const rect = event.currentTarget.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return;

    const deltaX = event.clientX - start.clientX;
    const deltaY = event.clientY - start.clientY;
    setPan({
      x: start.pan.x - (deltaX * start.viewBox.width) / rect.width,
      y: start.pan.y - (deltaY * start.viewBox.height) / rect.height
    });
  }

  function endPan(event?: ReactPointerEvent<HTMLDivElement>) {
    const start = panStartRef.current;
    if (!start) return;

    if (event?.currentTarget.hasPointerCapture(start.pointerId)) {
      try {
        event.currentTarget.releasePointerCapture(start.pointerId);
      } catch {
        // Pointer may already be released by the browser.
      }
    }

    panStartRef.current = null;
    setIsPanning(false);
  }

  function handleZChange(nextZ: number) {
    setSelectedZ(nextZ);
    resetViewport();
  }

  function handleLayerChange(nextLayer: string) {
    setSelectedLayer(nextLayer);
    resetViewport();
  }

  function renderToolbar(fullscreenMode: boolean) {
    return (
      <div className="map-toolbar" aria-label="Управление картой">
        <label>
          Уровень
          <select value={selectedZ} onChange={(event) => handleZChange(Number(event.target.value))}>
            {zLevels.map((level) => (
              <option key={level.z} value={level.z}>{toSafeMapText(level.label || String(level.z), 'уровень')}</option>
            ))}
          </select>
        </label>
        <label>
          Слой
          <select value={selectedLayer} onChange={(event) => handleLayerChange(event.target.value)}>
            {layers.map((layer) => (
              <option key={layer.id} value={layer.id}>{toSafeMapText(layer.label || layer.id, 'слой')}</option>
            ))}
          </select>
        </label>
        <button type="button" className="secondary" onClick={() => changeZoom(0.82)}>Приблизить</button>
        <button type="button" className="secondary" onClick={() => changeZoom(1.22)}>Отдалить</button>
        <button type="button" className="secondary" onClick={resetViewport}>Сброс</button>
        {fullscreenMode ? (
          <button type="button" className="map-fullscreen-button secondary" onClick={() => setIsFullscreen(false)}>
            Закрыть карту
          </button>
        ) : (
          <button
            type="button"
            className="map-fullscreen-button secondary"
            title="Открыть карту на весь экран"
            aria-label="Открыть карту на весь экран"
            onClick={() => setIsFullscreen(true)}
          >
            На весь экран
          </button>
        )}
        <span className="map-interaction-hint">Колесо мыши — масштаб, левая кнопка — перемещение.</span>
      </div>
    );
  }

  function renderAtlasFrame(fullscreenMode: boolean) {
    return (
      <div
        ref={fullscreenMode ? fullscreenAtlasFrameRef : inlineAtlasFrameRef}
        className={[
          'map-atlas-frame',
          'map-atlas-frame--interactive',
          fullscreenMode ? 'map-atlas-frame--fullscreen' : '',
          isPanning ? 'map-atlas-frame--panning' : ''
        ].filter(Boolean).join(' ')}
        onWheel={handleWheel}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={endPan}
        onPointerCancel={endPan}
        onPointerLeave={endPan}
        aria-label="Полотно карты. Колесо мыши меняет масштаб, зажатая левая кнопка перемещает карту."
      >
        <div className="map-border-runes" aria-hidden="true">✦ ᚱ ᚨ ᚾ ᛃ ᛟ ✦</div>
        <div className="map-compass-rose" aria-label="Роза ветров">
          <span>С</span>
          <span>В</span>
          <span>Ю</span>
          <span>З</span>
        </div>
        <svg
          className="map-canvas"
          role="img"
          aria-label={mapTitle}
          viewBox={viewBox}
          preserveAspectRatio="xMidYMid meet"
        >
          {block.map.regions.map((region) => {
            const regionNodes = region.nodeIds.map((nodeId) => nodeById.get(nodeId)).filter(isMapNode);
            if (regionNodes.length === 0) return null;
            const { cx, cy, radius } = regionCircle(regionNodes);
            return (
              <g key={region.id || region.label}>
                <circle className="map-region" cx={cx} cy={cy} r={radius} />
                <text className="map-region-label" x={cx - radius * 0.45} y={cy - radius * 0.72}>
                  {toSafeMapText(region.ownerFactionName || region.label || region.ownerFactionId, '')}
                </text>
              </g>
            );
          })}

          {block.map.links.map((link) => {
            const source = nodeById.get(link.sourceNodeId);
            const target = nodeById.get(link.targetNodeId);
            if (!source || !target) return null;
            return (
              <line
                key={link.id || `${link.sourceNodeId}-${link.targetNodeId}`}
                className={`map-link ${link.state === 'dangerous' ? 'map-link--dangerous' : ''}`}
                x1={source.x}
                y1={-source.y}
                x2={target.x}
                y2={-target.y}
              />
            );
          })}

          {renderedNodes.map((node) => (
            <g
              key={node.id}
              role="button"
              tabIndex={0}
              className={[
                'map-node',
                node.isCurrent ? 'map-node--current' : '',
                node.id === currentNode?.id ? 'map-node--selected' : '',
                isContested(node) ? 'map-node--contested' : '',
                node.isPlaceholder ? 'map-node--placeholder' : ''
              ].filter(Boolean).join(' ')}
              aria-label={`${node.isPlaceholder ? 'Известный выход' : 'Локация'}: ${toSafeMapText(node.label || node.id, 'точка карты')}`}
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => setSelectedNodeId(node.id)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') setSelectedNodeId(node.id);
              }}
            >
              <circle className="map-node-hit-area" cx={node.x} cy={-node.y} r={1.35} />
              <circle className="map-node-focus-ring" cx={node.x} cy={-node.y} r={node.isCurrent ? 1.06 : 0.86} />
              <circle cx={node.x} cy={-node.y} r={node.isCurrent ? 0.72 : 0.52} />
              <text x={node.x} y={-node.y - 1.05}>{toSafeMapText(node.label || node.id, 'точка карты')}</text>
            </g>
          ))}
        </svg>
        {nodes.length === 0 && <p className="map-empty">На карте пока нет видимых точек.</p>}
      </div>
    );
  }

  function renderLocationSelector() {
    return (
      <aside className="map-location-selector" aria-label="Выбрать локацию на карте">
        <div className="map-location-selector__header">
          <h5>Список локаций</h5>
          <p>Выберите место, чтобы подсветить его на атласе и открыть сведения ниже.</p>
        </div>
        {nodes.length > 0 ? (
          <div className="map-location-selector__list" role="list">
            {nodes.map((node) => {
              const isSelected = node.id === currentNode?.id;
              return (
                <button
                  key={node.id}
                  type="button"
                  className={[
                    'map-location-option',
                    isSelected ? 'is-selected' : '',
                    node.isPlaceholder ? 'is-placeholder' : ''
                  ].filter(Boolean).join(' ')}
                  aria-current={isSelected ? 'true' : undefined}
                  onClick={() => setSelectedNodeId(node.id)}
                >
                  <span className="map-location-option__title">
                    {toSafeMapText(node.label || node.id, 'Точка карты')}
                  </span>
                  <span className="map-location-option__meta">{describeNodeStatus(node)}</span>
                </button>
              );
            })}
          </div>
        ) : (
          <p className="map-location-selector__empty">На выбранном уровне пока нет видимых мест.</p>
        )}
      </aside>
    );
  }

  function renderMapViewport(fullscreenMode: boolean) {
    return (
      <div className={`map-viewport ${fullscreenMode ? 'map-viewport--fullscreen' : ''}`}>
        {renderLocationSelector()}
        {renderAtlasFrame(fullscreenMode)}
      </div>
    );
  }

  function renderMapFooter() {
    return (
      <footer className="map-block__footer">
        <div className="map-legend" aria-label="Легенда карты">
          <strong>Легенда</strong>
          <span><i className="legend-swatch current" aria-hidden="true" />Текущая точка</span>
          <span><i className="legend-swatch" aria-hidden="true" />Обычная точка</span>
          <span><i className="legend-swatch placeholder" aria-hidden="true" />Известный выход</span>
          <span><i className="legend-swatch faction" aria-hidden="true" />Влияние фракций</span>
        </div>
        {currentNode && (
          <dl className="map-card">
            <div>
              <dt>Выбрано</dt>
              <dd>{toSafeMapText(currentNode.label || currentNode.id, 'Локация')}</dd>
            </div>
            {describeMapNode(currentNode).map((item) => (
              <div key={`${item.key}-${item.value}`}>
                <dt>{item.key}</dt>
                <dd>{item.value}</dd>
              </div>
            ))}
            {resolveNodeImageUrl(currentNode) && (
              <div className="map-detail-media">
                <dt>Облик</dt>
                <dd>
                  <button type="button" className="map-image-thumb" onClick={() => setExpandedImageNode(currentNode)}>
                    <img
                      src={resolveNodeImageUrl(currentNode)}
                      alt={toSafeMapText(currentNode.imageAltText, `Изображение: ${currentNode.label || currentNode.id}`)}
                    />
                  </button>
                </dd>
              </div>
            )}
          </dl>
        )}
      </footer>
    );
  }

  return (
    <section className={`map-block map-block--${variant}`} data-map-realm={block.map.realm}>
      <header className="map-block__header">
        <div>
          <h4>{mapTitle}</h4>
          <p className="map-subtitle">{mapSubtitle}</p>
        </div>
        <div className="map-block__stats" aria-label="Сводка карты">
          <span>{allNodes.length} точек</span>
          <span>{block.map.links.length} связей</span>
          <span>{block.map.zLevels.length || 1} уровней</span>
        </div>
      </header>

      {renderToolbar(false)}
      {renderMapViewport(false)}
      {renderMapFooter()}

      {isFullscreen && typeof document !== 'undefined' && createPortal(
        <dialog className="map-fullscreen-dialog" open aria-label={`Полноэкранная карта: ${mapTitle}`}>
          <div className="map-fullscreen-panel">
            <header className="map-fullscreen-header">
              <div>
                <h3>{mapTitle}</h3>
                <p>{mapSubtitle}</p>
              </div>
              <button type="button" className="secondary" onClick={() => setIsFullscreen(false)}>Закрыть</button>
            </header>
            {renderToolbar(true)}
            <div className="map-fullscreen-content">
              {renderMapViewport(true)}
              {renderMapFooter()}
            </div>
          </div>
        </dialog>,
        document.body
      )}

      {expandedImageNode && (
        <dialog className="map-image-dialog" open aria-label={`Изображение локации ${toSafeMapText(expandedImageNode.label, 'карты')}`}>
          <form method="dialog">
            <button type="button" className="secondary" onClick={() => setExpandedImageNode(null)}>Закрыть</button>
          </form>
          <img
            src={resolveNodeImageUrl(expandedImageNode)}
            alt={toSafeMapText(expandedImageNode.imageAltText, `Изображение: ${expandedImageNode.label || expandedImageNode.id}`)}
          />
        </dialog>
      )}
    </section>
  );
}

function buildViewBox(nodes: MapNodeDto[]): ViewBoxParts {
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

function scaleViewBox(viewBox: ViewBoxParts, zoom: number, pan: PanPoint): ViewBoxParts {
  const nextWidth = viewBox.width * zoom;
  const nextHeight = viewBox.height * zoom;
  return {
    x: viewBox.x + (viewBox.width - nextWidth) / 2 + pan.x,
    y: viewBox.y + (viewBox.height - nextHeight) / 2 + pan.y,
    width: nextWidth,
    height: nextHeight
  };
}

function formatViewBox(viewBox: ViewBoxParts): string {
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

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function regionCircle(nodes: MapNodeDto[]) {
  const xs = nodes.map((node) => node.x);
  const ys = nodes.map((node) => -node.y);
  const cx = xs.reduce((sum, value) => sum + value, 0) / xs.length;
  const cy = ys.reduce((sum, value) => sum + value, 0) / ys.length;
  const radius = Math.max(2.2, ...nodes.map((node) => Math.hypot(node.x - cx, -node.y - cy) + 1.6));
  return { cx, cy, radius };
}

function describeMapNode(node: MapNodeDto): Array<{ key: string; value: string }> {
  const details = node.details
    .map((item) => ({
      key: toSafeMapText(item.key, 'Сведения'),
      value: toSafeMapText(item.value, 'не указано')
    }))
    .filter((item) => item.value);

  if (node.ownerFactionName || node.ownerFactionId) {
    details.push({
      key: 'Фракция',
      value: toSafeMapText(node.ownerFactionName || node.ownerFactionId, 'не указано')
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

function isMapNode(node: MapNodeDto | undefined): node is MapNodeDto {
  return node !== undefined;
}

function isHtmlDivElement(element: HTMLDivElement | null): element is HTMLDivElement {
  return element !== null;
}

function orderMapNodesForDisplay(nodes: MapNodeDto[], selectedNodeId: string | undefined): MapNodeDto[] {
  return [...nodes].sort((left, right) => mapNodeRenderPriority(left, selectedNodeId) - mapNodeRenderPriority(right, selectedNodeId));
}

function mapNodeRenderPriority(node: MapNodeDto, selectedNodeId: string | undefined): number {
  if (node.id === selectedNodeId) return 4;
  if (node.isCurrent) return 3;
  if (isContested(node)) return 2;
  if (node.isPlaceholder) return 0;
  return 1;
}

function isContested(node: MapNodeDto): boolean {
  const values = Object.values(node.influence ?? {})
    .map(Number)
    .filter((value) => value >= 25)
    .sort((a, b) => b - a);
  return values.length >= 2 && values[0] - values[1] <= 10;
}

function describeNodeStatus(node: MapNodeDto): string {
  if (node.isCurrent) return 'Вы здесь';
  if (node.isPlaceholder) return 'Известный выход';
  if (isContested(node)) return 'Спорное влияние';
  return 'Открытая локация';
}

function toSafeMapText(value: string | null | undefined, fallback: string): string {
  return sanitizePlayerMessage(value, fallback).safe;
}

function resolveNodeImageUrl(node: MapNodeDto): string {
  const value = node.imageUrl?.trim();
  if (!value) return '';

  if (
    value.startsWith('/api/media/') ||
    value.startsWith('/assets/') ||
    /^https?:\/\//i.test(value) ||
    /^data:image\/(?:png|jpeg|jpg|webp|gif);base64,/i.test(value)
  ) {
    return value;
  }

  return '';
}
