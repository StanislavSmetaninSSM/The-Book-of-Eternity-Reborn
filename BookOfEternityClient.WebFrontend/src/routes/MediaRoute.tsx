import { useEffect, useState } from 'react';
import type { BrowserGameScreenDto } from '../api/contracts';
import { QteScenePanel } from '../components/QteScenePanel';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { ShellPanel } from '../components/ShellPanel';
import { isSuccess, useShell } from '../context/ShellContext';
import { formatMediaDate, formatMediaSize } from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';

export default function MediaRoute() {
  const { advancedEnabled, readyState } = useShell();

  if (!readyState) {
    return null;
  }

  if (!isSuccess(readyState.game)) {
    return <EmptyOrFailure result={readyState.game} advancedEnabled={advancedEnabled} errorTitle="Медиа требуют внимания" empty={{
      title: 'Медиа появятся вместе со сценой',
      message: 'Галерея и быстрые сцены станут доступны, когда активная глава предоставит игровые материалы.',
      action: 'Откройте книгу и продолжите историю; этот раздел заполнится по мере появления сцен.'
    }} />;
  }

  const game = readyState.game.data;
  const galleryCount = game.media.gallery.length;

  return (
    <ShellPanel title="Медиа" eyebrow="галерея, атлас и быстрые сцены">
      <p className="muted">Материалы текущей главы: {galleryCount > 0 ? `${galleryCount} образов в галерее` : 'галерея ждёт первые образы'}.</p>
      <div className="split-grid three media-section-grid" aria-label={`Медиа текущей главы, изображений ${game.media.gallery.length}`}>
        <QteScenePanel qte={game.qte} />
        <MediaGalleryPanel media={game.media} />
        <MediaAtlasPanel map={game.media.map} realmLabel={game.theme.label} />
      </div>
    </ShellPanel>
  );
}

function MediaGalleryPanel({ media }: { media: BrowserGameScreenDto['media'] }) {
  return (
    <section className="summary-card media-gallery-panel" aria-labelledby="media-gallery-title">
      <div>
        <p className="panel-eyebrow">галерея</p>
        <h2 id="media-gallery-title">Образы главы</h2>
        <p>{media.sceneImagePrompt ? toPlayerFacingText(media.sceneImagePrompt, 'Образ текущей сцены уточняется.') : 'Книга пока не передала образ текущей сцены.'}</p>
      </div>

      {media.gallery.length > 0 ? (
        <div className="media-gallery-grid">
          {media.gallery.map((item) => (
            <article key={item.mediaId} className="media-gallery-card">
              <a href={item.url} target="_blank" rel="noreferrer">
                <img src={item.url} alt={item.fileName} loading="lazy" />
              </a>
              <div>
                <h3>{toPlayerFacingText(item.fileName, 'Изображение сцены')}</h3>
                <dl className="kv-list">
                  <div><dt>Тип</dt><dd>{toPlayerFacingText(item.contentType, 'изображение')}</dd></div>
                  <div><dt>Размер</dt><dd>{formatMediaSize(item.length)}</dd></div>
                  <div><dt>Обновлено</dt><dd>{formatMediaDate(item.modifiedAtUtc)}</dd></div>
                </dl>
                <a className="media-gallery-card__open" href={item.url} target="_blank" rel="noreferrer">Открыть изображение</a>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <p className="muted">Сохранённые изображения появятся здесь после генерации или добавления сцен в локальную галерею.</p>
      )}
    </section>
  );
}

function MediaAtlasPanel({ map, realmLabel }: { map: BrowserGameScreenDto['media']['map']; realmLabel: string }) {
  const currentNode = map.nodes.find((node) => node.id === map.currentNodeId || node.isCurrent);
  const defaultLayer = map.layers.find((layer) => layer.isDefault)?.id ?? map.layers[0]?.id ?? 'all';
  const defaultZ = currentNode?.z ?? map.zLevels[0]?.z ?? 0;
  const [selectedLayer, setSelectedLayer] = useState(defaultLayer);
  const [selectedZ, setSelectedZ] = useState(defaultZ);
  const [showPolitical, setShowPolitical] = useState(false);

  useEffect(() => {
    setSelectedLayer(defaultLayer);
    setSelectedZ(defaultZ);
  }, [defaultLayer, defaultZ]);

  const layers = map.layers.length > 0 ? map.layers : [{ id: 'all', label: 'Все слои', isDefault: true }];
  const zLevels = map.zLevels.length > 0 ? map.zLevels : [{ z: selectedZ, label: `уровень ${selectedZ}` }];
  const visibleNodes = map.nodes.filter((node) => (selectedLayer === 'all' || node.layer === selectedLayer) && node.z === selectedZ);

  return (
    <section className="media-atlas-panel" aria-labelledby="media-atlas-title">
      <div>
        <p className="panel-eyebrow">атлас</p>
        <h2 id="media-atlas-title">{toPlayerFacingText(map.title, 'Атлас текущего мира')}</h2>
        <p>{toPlayerFacingText(realmLabel || map.realm, 'Текущее царство')} · {currentNode ? `сейчас: ${toPlayerFacingText(currentNode.label, 'текущая точка')}` : 'точка героя уточняется'}</p>
      </div>

      <div className="media-atlas-controls">
        <label>
          <span>Выберите уровень</span>
          <select value={selectedZ} onChange={(event) => setSelectedZ(Number(event.currentTarget.value))}>
            {zLevels.map((level) => (
              <option key={level.z} value={level.z}>{toPlayerFacingText(level.label, `уровень ${level.z}`)}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Выберите слой</span>
          <select value={selectedLayer} onChange={(event) => setSelectedLayer(event.currentTarget.value)}>
            {layers.map((layer) => (
              <option key={layer.id} value={layer.id}>{toPlayerFacingText(layer.label, 'слой карты')}</option>
            ))}
          </select>
        </label>
        <label className="checkbox-control">
          <input type="checkbox" checked={showPolitical} onChange={(event) => setShowPolitical(event.currentTarget.checked)} />
          <span>Политическое влияние</span>
        </label>
      </div>

      {visibleNodes.length > 0 ? (
        <div className="media-atlas-node-grid">
          {visibleNodes.map((node) => {
            const influenceEntries = Object.entries(node.influence).filter(([, value]) => value !== 0);
            return (
              <article key={node.id} className={`media-atlas-node${node.isCurrent ? ' is-current' : ''}`}>
                <header>
                  <h3>{toPlayerFacingText(node.label, 'Точка карты')}</h3>
                  <span className="availability-pill">{node.isCurrent ? 'текущая точка' : toPlayerFacingText(node.type, 'точка')}</span>
                </header>
                <dl className="kv-list">
                  <div><dt>Слой</dt><dd>{toPlayerFacingText(node.layer, 'слой')}</dd></div>
                  <div><dt>Уровень</dt><dd>{node.z}</dd></div>
                  <div><dt>Координаты</dt><dd>{node.x}, {node.y}</dd></div>
                  <div><dt>Владелец</dt><dd>{node.ownerFactionName ? toPlayerFacingText(node.ownerFactionName, 'фракция') : 'не указан'}</dd></div>
                </dl>
                {node.details.length > 0 && (
                  <dl className="kv-list">
                    {node.details.map((detail) => (
                      <div key={`${node.id}-${detail.key}`}><dt>{toPlayerFacingText(detail.key, 'Деталь')}</dt><dd>{toPlayerFacingText(detail.value, '—')}</dd></div>
                    ))}
                  </dl>
                )}
                {showPolitical && (
                  <div className="media-atlas-influence" aria-label="Политическое влияние">
                    <h4>Политическое влияние</h4>
                    {influenceEntries.length > 0 ? (
                      <ul>
                        {influenceEntries.map(([faction, value]) => (
                          <li key={faction}><span>{toPlayerFacingText(faction, 'фракция')}</span><strong>{value}</strong></li>
                        ))}
                      </ul>
                    ) : (
                      <p className="muted">Влияние для этой точки пока не отмечено.</p>
                    )}
                  </div>
                )}
              </article>
            );
          })}
        </div>
      ) : (
        <p className="muted">На выбранном уровне и слое пока нет точек карты. Выберите другой слой или продолжите главу.</p>
      )}
    </section>
  );
}
