import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
  type WheelEvent as ReactWheelEvent
} from 'react';
import { createPortal } from 'react-dom';
import type {
  MapLayerDto,
  MapNodeDto,
  UiMapBlock
} from '../../api/contracts';
import { sanitizePlayerMessage, toPlayerFacingText } from '../../utils/playerCopy';
import {
  buildViewBox,
  clamp,
  DEFAULT_PAN,
  describeMapNode,
  describeNodeStatus,
  formatViewBox,
  groupNodesForDrawer,
  hasPoliticalSignal,
  isContested,
  MAP_NODE_HIT_RADIUS,
  MAX_VIEWBOX_SCALE,
  MIN_VIEWBOX_SCALE,
  nodeGlyph,
  nodeMatchesQuery,
  orderMapNodesForDisplay,
  regionCircle,
  resolveNodeDisplayGeometry,
  resolveVisibleLinks,
  scaleViewBox,
  type PanPoint
} from './mapAtlasLogic';

export interface MapAtlasProps {
  block: UiMapBlock;
  /**
   * `embedded` — rendered inside the browser client command surface.
   * `standalone` — rendered as the top-level page (console /map viewer).
   * The only difference is the surrounding chrome, not the map itself.
   */
  variant?: 'embedded' | 'standalone';
}

interface PanStart {
  pointerId: number;
  clientX: number;
  clientY: number;
  pan: PanPoint;
  viewBoxX: number;
  viewBoxY: number;
  viewBoxWidth: number;
  viewBoxHeight: number;
}

export function MapAtlas({ block, variant = 'embedded' }: MapAtlasProps) {
  const mapTitle = toPlayerFacingText(block.title || block.map.title, 'Карта');
  const mapSubtitle = toPlayerFacingText(block.map.title || block.map.realm, 'Атлас местности');
  const allNodes = block.map.nodes;
  const currentNodeSeed =
    allNodes.find((node) => node.id === block.map.currentNodeId) ??
    allNodes.find((node) => node.isCurrent) ??
    allNodes[0];
  const zLevels = block.map.zLevels.length > 0
    ? block.map.zLevels
    : [{ z: currentNodeSeed?.z ?? 0, label: 'земля' }];
  const layers = block.map.layers.length > 0
    ? block.map.layers
    : [{ id: currentNodeSeed?.layer || 'world', label: 'Мир', isDefault: true }];
  const defaultZ = currentNodeSeed?.z ?? zLevels[0]?.z ?? 0;
  const defaultLayer =
    currentNodeSeed?.layer ||
    layers.find((layer) => layer.isDefault)?.id ||
    layers[0]?.id ||
    'world';
  const defaultNodeId = currentNodeSeed?.id ?? '';

  const [selectedZ, setSelectedZ] = useState(defaultZ);
  const [selectedLayer, setSelectedLayer] = useState(defaultLayer);
  const [selectedNodeId, setSelectedNodeId] = useState(defaultNodeId);
  const [hoveredNodeId, setHoveredNodeId] = useState<string | null>(null);
  const [expandedImageNode, setExpandedImageNode] = useState<MapNodeDto | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState<PanPoint>(DEFAULT_PAN);
  const [isPanning, setIsPanning] = useState(false);
  const [isDrawerOpen, setIsDrawerOpen] = useState(true);
  const [drawerQuery, setDrawerQuery] = useState('');
  const [politicalOverlay, setPoliticalOverlay] = useState(true);

  const inlineAtlasFrameRef = useRef<HTMLDivElement | null>(null);
  const fullscreenAtlasFrameRef = useRef<HTMLDivElement | null>(null);
  const panStartRef = useRef<PanStart | null>(null);
  const mapResetKey = block.map;

  useEffect(() => {
    setSelectedZ(defaultZ);
    setSelectedLayer(defaultLayer);
    setSelectedNodeId(defaultNodeId);
    setHoveredNodeId(null);
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
      setZoom((value) =>
        clamp(value * (event.deltaY < 0 ? 0.88 : 1.12), MIN_VIEWBOX_SCALE, MAX_VIEWBOX_SCALE)
      );
    };
    const options = { passive: false };
    const frames = [inlineAtlasFrameRef.current, fullscreenAtlasFrameRef.current].filter(
      isHtmlDivElement
    );

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
    () =>
      allNodes.filter(
        (node) => node.z === selectedZ && (node.layer || 'world') === selectedLayer
      ),
    [allNodes, selectedLayer, selectedZ]
  );
  const displayNodes = useMemo(
    () =>
      resolveNodeDisplayGeometry(nodes).map(({ node, x, y }) =>
        x === node.x && y === node.y ? node : { ...node, x, y }
      ),
    [nodes]
  );
  const nodeById = useMemo(
    () => new Map(displayNodes.map((node) => [node.id, node] as const)),
    [displayNodes]
  );
  const currentNode =
    displayNodes.find((node) => node.id === selectedNodeId) ??
    displayNodes.find((node) => node.id === block.map.currentNodeId) ??
    displayNodes.find((node) => node.isCurrent) ??
    displayNodes[0];
  const focus = useMemo(
    () => ({
      hoveredNodeId: hoveredNodeId ?? undefined,
      selectedNodeId: currentNode?.id,
      currentNodeId: block.map.currentNodeId
    }),
    [block.map.currentNodeId, currentNode?.id, hoveredNodeId]
  );
  const renderedNodes = useMemo(
    () => orderMapNodesForDisplay(displayNodes, focus),
    [displayNodes, focus]
  );
  const visibleLinks = useMemo(() => resolveVisibleLinks(block.map.links, nodeById), [
    block.map.links,
    nodeById
  ]);
  const drawerGroups = useMemo(() => groupNodesForDrawer(block.map), [block.map]);
  const baseViewBox = useMemo(() => buildViewBox(displayNodes), [displayNodes]);
  const transformedViewBox = useMemo(
    () => scaleViewBox(baseViewBox, zoom, pan),
    [baseViewBox, pan, zoom]
  );
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
      viewBoxX: transformedViewBox.x,
      viewBoxY: transformedViewBox.y,
      viewBoxWidth: transformedViewBox.width,
      viewBoxHeight: transformedViewBox.height
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
      x: start.pan.x - (deltaX * start.viewBoxWidth) / rect.width,
      y: start.pan.y - (deltaY * start.viewBoxHeight) / rect.height
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

  function focusNodeFromDrawer(node: MapNodeDto) {
    const nodeLayer = node.layer || 'world';
    if (nodeLayer !== selectedLayer) setSelectedLayer(nodeLayer);
    if (node.z !== selectedZ) setSelectedZ(node.z);
    setSelectedNodeId(node.id);
    resetViewport();
  }

  function renderToolbar(fullscreenMode: boolean) {
    return (
      <div className="map-toolbar" aria-label="Управление картой">
        <label>
          Уровень
          <select value={selectedZ} onChange={(event) => handleZChange(Number(event.target.value))}>
            {zLevels.map((level) => (
              <option key={level.z} value={level.z}>
                {toSafeMapText(level.label || String(level.z), 'уровень')}
              </option>
            ))}
          </select>
        </label>
        <label>
          Слой
          <select
            value={selectedLayer}
            onChange={(event) => handleLayerChange(event.target.value)}
          >
            {layers.map((layer) => (
              <option key={layer.id} value={layer.id}>
                {toSafeMapText(layer.label || layer.id, 'слой')}
              </option>
            ))}
          </select>
        </label>
        <label className="map-toolbar__toggle">
          <input
            type="checkbox"
            checked={politicalOverlay}
            onChange={(event) => setPoliticalOverlay(event.target.checked)}
          />
          <span>Влияние фракций</span>
        </label>
        <button type="button" className="secondary" onClick={() => changeZoom(0.82)}>
          Приблизить
        </button>
        <button type="button" className="secondary" onClick={() => changeZoom(1.22)}>
          Отдалить
        </button>
        <button type="button" className="secondary" onClick={resetViewport}>
          Сброс
        </button>
        <button
          type="button"
          className={`map-drawer-button secondary${isDrawerOpen ? ' is-active' : ''}`}
          aria-pressed={isDrawerOpen}
          onClick={() => setIsDrawerOpen((value) => !value)}
        >
          {isDrawerOpen ? 'Скрыть список' : 'Список локаций'}
        </button>
        {fullscreenMode ? (
          <button
            type="button"
            className="map-fullscreen-button secondary"
            onClick={() => setIsFullscreen(false)}
          >
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
        <span className="map-interaction-hint">
          Колесо мыши — масштаб, левая кнопка — перемещение, наведение и клик выводят точку на передний план.
        </span>
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
        ]
          .filter(Boolean)
          .join(' ')}
        onWheel={handleWheel}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={endPan}
        onPointerCancel={endPan}
        onPointerLeave={endPan}
        aria-label="Полотно карты. Колесо мыши меняет масштаб, зажатая левая кнопка перемещает карту."
      >
        <div className="map-border-runes" aria-hidden="true">
          ✦ ᚱ ᚨ ᚾ ᛃ ᛟ ✦
        </div>
        <svg
          className="map-canvas"
          role="img"
          aria-label={mapTitle}
          viewBox={viewBox}
          preserveAspectRatio="xMidYMid meet"
        >
          <defs>
            <filter id="map-atlas-parchment-grain" x="-5%" y="-5%" width="110%" height="110%">
              <feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="3" seed="12" />
              <feColorMatrix type="saturate" values="0" />
            </filter>
            <radialGradient id="map-atlas-vignette" cx="50%" cy="50%" r="62%">
              <stop offset="60%" stopColor="rgba(42,24,8,0)" />
              <stop offset="100%" stopColor="rgba(42,24,8,0.42)" />
            </radialGradient>
            <filter id="map-node-glow" x="-80%" y="-80%" width="260%" height="260%">
              <feGaussianBlur stdDeviation="0.32" />
            </filter>
          </defs>
          <rect
            className="atlas-texture"
            x="-2000"
            y="-2000"
            width="4000"
            height="4000"
            filter="url(#map-atlas-parchment-grain)"
          />

          {politicalOverlay &&
            block.map.regions.map((region) => {
              const regionNodes = region.nodeIds
                .map((nodeId) => nodeById.get(nodeId))
                .filter(isMapNode);
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

          {politicalOverlay &&
            displayNodes
              .filter(hasPoliticalSignal)
              .map((node) => (
                <circle
                  key={`halo-${node.id}`}
                  className={`map-political-halo ${isContested(node) ? 'map-political-halo--contested' : ''}`}
                  cx={node.x}
                  cy={-node.y}
                  r={isContested(node) ? 1.32 : 1.12}
                />
              ))}

          {visibleLinks.map(({ link, source, target }) => (
            <line
              key={link.id || `${link.sourceNodeId}-${link.targetNodeId}`}
              className={`map-link ${link.state === 'dangerous' ? 'map-link--dangerous' : ''}`}
              x1={source.x}
              y1={-source.y}
              x2={target.x}
              y2={-target.y}
            />
          ))}

          {renderedNodes.map((node) => {
            const isHovered = node.id === hoveredNodeId;
            const isSelected = node.id === currentNode?.id;
            return (
              <g
                key={node.id}
                role="button"
                tabIndex={0}
                className={[
                  'map-node',
                  node.isCurrent ? 'map-node--current' : '',
                  isSelected ? 'map-node--selected' : '',
                  isHovered ? 'map-node--hovered' : '',
                  isContested(node) ? 'map-node--contested' : '',
                  node.ownerFactionId || node.ownerFactionName ? 'map-node--faction' : '',
                  node.isPlaceholder ? 'map-node--placeholder' : ''
                ]
                  .filter(Boolean)
                  .join(' ')}
                data-node-status={describeNodeStatus(node).kind}
                data-node-type={node.type || ''}
                aria-label={`Локация: ${toSafeMapText(node.label || node.id, 'точка карты')}`}
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => setSelectedNodeId(node.id)}
                onMouseEnter={() => setHoveredNodeId(node.id)}
                onMouseLeave={() => setHoveredNodeId((current) => (current === node.id ? null : current))}
                onFocus={() => setHoveredNodeId(node.id)}
                onBlur={() => setHoveredNodeId((current) => (current === node.id ? null : current))}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    setSelectedNodeId(node.id);
                  }
                }}
              >
                {node.isCurrent && (
                  <circle
                    className="map-node-current-glow"
                    cx={node.x}
                    cy={-node.y}
                    r={1.4}
                    filter="url(#map-node-glow)"
                  />
                )}
                <circle className="map-node-hit-area" cx={node.x} cy={-node.y} r={MAP_NODE_HIT_RADIUS} />
                <circle
                  className="map-node-focus-ring"
                  cx={node.x}
                  cy={-node.y}
                  r={node.isCurrent ? 1.06 : 0.86}
                />
                <circle
                  className="map-node-core"
                  cx={node.x}
                  cy={-node.y}
                  r={node.isCurrent ? 0.72 : 0.52}
                />
                <text
                  className="map-node-glyph"
                  x={node.x}
                  y={-node.y + 0.16}
                >
                  {node.isPlaceholder ? '' : nodeGlyph(node.type)}
                </text>
                <text className="map-node-label" x={node.x} y={-node.y - 1.05}>
                  {toSafeMapText(node.label || node.id, 'точка карты')}
                </text>
              </g>
            );
          })}

          <rect className="map-vignette" x="-2000" y="-2000" width="4000" height="4000" fill="url(#map-atlas-vignette)" />
        </svg>
        {displayNodes.length === 0 && <p className="map-empty">На карте пока нет видимых точек.</p>}
      </div>
    );
  }

  function renderLocationDrawer() {
    if (!isDrawerOpen) return null;

    const normalizedQuery = drawerQuery.trim();
    const totalVisible = drawerGroups.reduce(
      (sum, group) =>
        sum +
        group.levels.reduce(
          (levelSum, level) =>
            levelSum + level.nodes.filter((node) => nodeMatchesQuery(node, normalizedQuery)).length,
          0
        ),
      0
    );

    return (
      <aside className="map-location-selector" aria-label="Выбрать локацию на карте">
        <div className="map-location-selector__header">
          <h5>Список локаций</h5>
          <p>
            Все локации по слоям и уровням. Выбор подсветит точку и центрирует атлас. Показано{' '}
            <strong>{totalVisible}</strong>.
          </p>
          <label className="map-location-selector__search">
            <span>Поиск</span>
            <input
              type="search"
              placeholder="Название, тип, фракция..."
              value={drawerQuery}
              onChange={(event) => setDrawerQuery(event.currentTarget.value)}
            />
          </label>
        </div>
        <div className="map-location-selector__list" role="list">
          {drawerGroups.map((group) => {
            const levelsWithMatches = group.levels
              .map((level) => ({
                level,
                nodes: level.nodes.filter((node) => nodeMatchesQuery(node, normalizedQuery))
              }))
              .filter((entry) => entry.nodes.length > 0);
            if (levelsWithMatches.length === 0) return null;
            return (
              <section className="map-location-group" key={group.layer.id}>
                <h6>{toSafeMapText(group.layer.label || group.layer.id, 'Слой')}</h6>
                {levelsWithMatches.map(({ level, nodes }) => (
                  <div className="map-location-group__level" key={`${group.layer.id}-${level.zLevel.z}`}>
                    <span className="map-location-group__level-label">
                      {toSafeMapText(level.zLevel.label || String(level.zLevel.z), 'уровень')}
                    </span>
                    {nodes.map((node) => {
                      const isSelected = node.id === currentNode?.id;
                      const status = describeNodeStatus(node);
                      return (
                        <button
                          key={node.id}
                          type="button"
                          className={[
                            'map-location-option',
                            isSelected ? 'is-selected' : '',
                            node.isPlaceholder ? 'is-placeholder' : '',
                            status.kind === 'contested' ? 'is-contested' : '',
                            status.kind === 'faction' ? 'is-faction' : ''
                          ]
                            .filter(Boolean)
                            .join(' ')}
                          aria-current={isSelected ? 'true' : undefined}
                          onMouseEnter={() => setHoveredNodeId(node.id)}
                          onMouseLeave={() => setHoveredNodeId((current) => (current === node.id ? null : current))}
                          onClick={() => focusNodeFromDrawer(node)}
                        >
                          <span className="map-location-option__glyph" aria-hidden="true">
                            {node.isPlaceholder ? '◇' : nodeGlyph(node.type)}
                          </span>
                          <span className="map-location-option__body">
                            <span className="map-location-option__title">
                              {toSafeMapText(node.label || node.id, 'Точка карты')}
                            </span>
                            <span className="map-location-option__meta">{status.label}</span>
                          </span>
                        </button>
                      );
                    })}
                  </div>
                ))}
              </section>
            );
          })}
          {totalVisible === 0 && (
            <p className="map-location-selector__empty">Ничего не найдено по запросу.</p>
          )}
        </div>
      </aside>
    );
  }

  function renderMapViewport(fullscreenMode: boolean) {
    const viewportClass = [
      'map-viewport',
      fullscreenMode ? 'map-viewport--fullscreen' : '',
      isDrawerOpen ? 'map-viewport--with-drawer' : 'map-viewport--drawer-closed'
    ]
      .filter(Boolean)
      .join(' ');
    return (
      <div className={viewportClass}>
        {renderLocationDrawer()}
        {renderAtlasFrame(fullscreenMode)}
      </div>
    );
  }

  function renderMapFooter() {
    return (
      <footer className="map-block__footer">
        <div className="map-legend" aria-label="Легенда карты">
          <strong>Легенда</strong>
          <span>
            <i className="legend-swatch current" aria-hidden="true" />
            Текущая точка
          </span>
          <span>
            <i className="legend-swatch" aria-hidden="true" />
            Обычная точка
          </span>
          <span>
            <i className="legend-swatch faction" aria-hidden="true" />
            Под контролем фракции
          </span>
          <span>
            <i className="legend-swatch contested" aria-hidden="true" />
            Спорная зона
          </span>
          <span>
            <i className="legend-swatch placeholder" aria-hidden="true" />
            Известный выход
          </span>
        </div>
        {currentNode && (
          <dl className="map-card">
            <div className="map-card__title-row">
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
                  <button
                    type="button"
                    className="map-image-thumb"
                    onClick={() => setExpandedImageNode(currentNode)}
                  >
                    <img
                      src={resolveNodeImageUrl(currentNode)}
                      alt={toSafeMapText(
                        currentNode.imageAltText,
                        `Изображение: ${currentNode.label || currentNode.id}`
                      )}
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

  const rootClassName = [
    'map-atlas',
    `map-atlas--${variant}`,
    variant === 'standalone' ? 'map-atlas--standalone-shell' : ''
  ]
    .filter(Boolean)
    .join(' ');

  const header = (
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
  );

  return (
    <section className={rootClassName} data-map-realm={block.map.realm}>
      {variant === 'embedded' && (
        <>
          {header}
          {renderToolbar(false)}
          {renderMapViewport(false)}
          {renderMapFooter()}
        </>
      )}

      {variant === 'standalone' && (
        <div className="map-atlas__standalone-body">
          {header}
          {renderToolbar(false)}
          {renderMapViewport(false)}
          {renderMapFooter()}
        </div>
      )}

      {isFullscreen &&
        typeof document !== 'undefined' &&
        createPortal(
          <div
            className="map-fullscreen-dialog"
            role="dialog"
            aria-modal="true"
            aria-label={`Полноэкранная карта: ${mapTitle}`}
          >
            <div className="map-fullscreen-panel">
              <header className="map-fullscreen-header">
                <div>
                  <h3>{mapTitle}</h3>
                  <p>{mapSubtitle}</p>
                </div>
                <button
                  type="button"
                  className="map-fullscreen-close-button secondary"
                  onClick={() => setIsFullscreen(false)}
                >
                  Закрыть
                </button>
              </header>
              {renderToolbar(true)}
              <div className="map-fullscreen-content">
                {renderMapViewport(true)}
                {renderMapFooter()}
              </div>
            </div>
          </div>,
          document.body
        )}

      {expandedImageNode && (
        <dialog
          className="map-image-dialog"
          open
          aria-label={`Изображение локации ${toSafeMapText(expandedImageNode.label, 'карты')}`}
        >
          <form method="dialog">
            <button type="button" className="secondary" onClick={() => setExpandedImageNode(null)}>
              Закрыть
            </button>
          </form>
          <img
            src={resolveNodeImageUrl(expandedImageNode)}
            alt={toSafeMapText(
              expandedImageNode.imageAltText,
              `Изображение: ${expandedImageNode.label || expandedImageNode.id}`
            )}
          />
        </dialog>
      )}
    </section>
  );
}

function isMapNode(node: MapNodeDto | undefined): node is MapNodeDto {
  return node !== undefined;
}

function isHtmlDivElement(element: HTMLDivElement | null): element is HTMLDivElement {
  return element !== null;
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
