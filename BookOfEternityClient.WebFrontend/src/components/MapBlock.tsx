import { useEffect, useMemo, useState } from 'react';
import type { MapNodeDto, UiMapBlock } from '../api/contracts';
import { sanitizePlayerMessage, toPlayerFacingText } from '../utils/playerCopy';

interface MapBlockProps {
  block: UiMapBlock;
  variant?: 'full' | 'compact';
}

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
  const [zoom, setZoom] = useState(1);
  const mapResetKey = block.map;

  useEffect(() => {
    setSelectedZ(defaultZ);
    setSelectedLayer(defaultLayer);
    setSelectedNodeId(defaultNodeId);
    setExpandedImageNode(null);
    setZoom(1);
  }, [defaultLayer, defaultNodeId, defaultZ, mapResetKey]);
  const nodes = useMemo(
    () => allNodes.filter((node) => node.z === selectedZ && (node.layer || 'world') === selectedLayer),
    [allNodes, selectedLayer, selectedZ]
  );
  const nodeById = new Map(nodes.map((node) => [node.id, node] as const));
  const currentNode = nodes.find((node) => node.id === selectedNodeId) ??
    nodes.find((node) => node.id === block.map.currentNodeId) ??
    nodes.find((node) => node.isCurrent) ??
    nodes[0];
  const viewBox = scaleViewBox(buildViewBox(nodes), zoom);

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

      <div className="map-toolbar" aria-label="Управление картой">
        <label>
          Уровень
          <select value={selectedZ} onChange={(event) => setSelectedZ(Number(event.target.value))}>
            {zLevels.map((level) => (
              <option key={level.z} value={level.z}>{toSafeMapText(level.label || String(level.z), 'уровень')}</option>
            ))}
          </select>
        </label>
        <label>
          Слой
          <select value={selectedLayer} onChange={(event) => setSelectedLayer(event.target.value)}>
            {layers.map((layer) => (
              <option key={layer.id} value={layer.id}>{toSafeMapText(layer.label || layer.id, 'слой')}</option>
            ))}
          </select>
        </label>
        <button type="button" className="secondary" onClick={() => setZoom((value) => Math.max(0.5, value * 0.82))}>Приблизить</button>
        <button type="button" className="secondary" onClick={() => setZoom((value) => Math.min(2.5, value * 1.22))}>Отдалить</button>
        <button type="button" className="secondary" onClick={() => setZoom(1)}>Сброс</button>
      </div>

      <div className="map-atlas-frame">
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
          <defs>
            <filter id="browser-atlas-texture">
              <feTurbulence type="fractalNoise" baseFrequency="0.8" numOctaves="2" seed="17" />
              <feColorMatrix type="saturate" values="0" />
            </filter>
          </defs>
          <rect className="atlas-texture" x="-2000" y="-2000" width="4000" height="4000" filter="url(#browser-atlas-texture)" />

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

          {nodes.map((node) => (
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
              onClick={() => setSelectedNodeId(node.id)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') setSelectedNodeId(node.id);
              }}
            >
              <circle cx={node.x} cy={-node.y} r={node.isCurrent ? 0.72 : 0.52} />
              <text x={node.x} y={-node.y - 1.05}>{toSafeMapText(node.label || node.id, 'точка карты')}</text>
            </g>
          ))}
        </svg>
        {nodes.length === 0 && <p className="map-empty">На карте пока нет видимых точек.</p>}
      </div>

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

function buildViewBox(nodes: MapNodeDto[]): string {
  if (nodes.length === 0) return '-20 -20 40 40';

  const xs = nodes.map((node) => node.x);
  const ys = nodes.map((node) => -node.y);
  const minX = Math.min(...xs) - 3;
  const maxX = Math.max(...xs) + 9;
  const minY = Math.min(...ys) - 4;
  const maxY = Math.max(...ys) + 4;
  return [minX, minY, Math.max(12, maxX - minX), Math.max(12, maxY - minY)].join(' ');
}

function scaleViewBox(viewBox: string, zoom: number): string {
  const [x, y, width, height] = viewBox.split(' ').map(Number);
  const nextWidth = width * zoom;
  const nextHeight = height * zoom;
  return [
    x + (width - nextWidth) / 2,
    y + (height - nextHeight) / 2,
    nextWidth,
    nextHeight
  ].join(' ');
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

function isContested(node: MapNodeDto): boolean {
  const values = Object.values(node.influence ?? {})
    .map(Number)
    .filter((value) => value >= 25)
    .sort((a, b) => b - a);
  return values.length >= 2 && values[0] - values[1] <= 10;
}

function toSafeMapText(value: string | null | undefined, fallback: string): string {
  return sanitizePlayerMessage(value, fallback).safe;
}

function resolveNodeImageUrl(node: MapNodeDto): string {
  return toSafeMapText(node.imageUrl, '');
}
