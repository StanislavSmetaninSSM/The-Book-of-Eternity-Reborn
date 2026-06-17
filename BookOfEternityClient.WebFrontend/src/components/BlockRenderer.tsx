import type { ReactNode } from 'react';
import type { UiBlock, UiTone } from '../api/contracts';
import { browserUiAssets } from '../browserUiAssets';
import { sanitizePlayerMessage, toPlayerFacingText } from '../utils/playerCopy';
import { JsonTreeViewer } from './JsonTreeViewer';
import { MapBlock } from './MapBlock';

function toneClassName(tone: UiTone): string {
  switch (tone) {
    case 'Muted': return 'block-text--muted';
    case 'Subtle': return 'block-text--subtle';
    case 'Accent': return 'block-text--accent';
    case 'Success': return 'block-text--success';
    case 'Warning': return 'block-text--warning';
    case 'Error': return 'block-text--error';
    default: return '';
  }
}

export function BlockRenderer({ block, advancedEnabled = false }: { block: UiBlock; advancedEnabled?: boolean }): ReactNode {
  switch (block.kind) {
    case 'text': {
      const { safe, hasTechnical } = sanitizePlayerMessage(block.text, 'Текст действия недоступен.');
      return <p className={`block-text ${hasTechnical ? 'block-text--muted' : toneClassName(block.tone)}`}>{safe}</p>;
    }

    case 'panel':
      return (
        <section className="block-panel">
          <h4 className="block-panel__title">{toPlayerFacingText(block.title, 'Панель')}</h4>
          <div className="block-panel__body">
            {block.blocks.map((child, i) => <BlockRenderer key={`${child.kind}-${i}`} block={child} advancedEnabled={advancedEnabled} />)}
          </div>
        </section>
      );

    case 'table':
      return (
        <div className="block-table">
          {block.title && <h4 className="block-table__title">{toPlayerFacingText(block.title, 'Таблица')}</h4>}
          <div className="block-table__scroll">
            <table>
              <thead>
                <tr>{block.columns.map((col) => <th key={col}>{toSafeBlockText(col, 'Столбец')}</th>)}</tr>
              </thead>
              <tbody>
                {block.rows.map((row, i) => (
                  <tr key={i}>{row.cells.map((cell, j) => <td key={j}>{toSafeBlockText(cell, '—')}</td>)}</tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      );

    case 'list': {
      const ListTag = block.ordered ? 'ol' : 'ul';
      return (
        <ListTag className="block-list">
          {block.items.map((item, i) => <li key={i}>{toSafeBlockText(item, 'пункт списка')}</li>)}
        </ListTag>
      );
    }

    case 'keyValueGrid':
      return (
        <dl className="block-kv">
          {block.items.map((item) => (
            <div key={item.key} className="block-kv__row">
              <dt>{toSafeBlockText(item.key, 'параметр')}</dt>
              <dd>{toSafeBlockText(item.value, 'значение')}</dd>
            </div>
          ))}
        </dl>
      );

    case 'message': {
      const severityClass = `block-message--${block.severity.toLowerCase()}`;
      const title = sanitizePlayerMessage(block.title, 'Сообщение').safe;
      const message = sanitizePlayerMessage(block.message, 'Игровое действие изменило состояние.').safe;
      return (
        <div className={`block-message ${severityClass}`}>
          <strong>{title}</strong>
          <p>{message}</p>
        </div>
      );
    }

    case 'image':
      return (
        <figure className={`block-image${block.url ? '' : ' block-image--fallback'}`}>
          {block.url ? (
            <img src={block.url} alt={block.altText || block.title} loading="lazy" />
          ) : (
            <>
              <img
                src={browserUiAssets.galleryEmptyArchive.url}
                alt=""
                loading="lazy"
                aria-hidden="true"
                onError={(event) => { event.currentTarget.hidden = true; }}
              />
              <p className="block-text--muted">Образ пока не проявился.</p>
            </>
          )}
          {block.title && <figcaption>{toSafeBlockText(block.title, 'Изображение')}</figcaption>}
        </figure>
      );

    case 'map':
      return <MapBlock block={block} />;

    case 'rawJson':
      if (advancedEnabled) {
        return <JsonTreeViewer data={block.json} title={toPlayerFacingText(block.title, 'Подробные сведения')} defaultExpanded={false} />;
      }

      return (
        <p className="block-text block-text--muted">
          <strong>{toPlayerFacingText(block.title, 'Подробные сведения')}</strong> — Подробные сведения доступны в расширенном режиме.
        </p>
      );
  }
}

function toSafeBlockText(value: string | null | undefined, fallback: string): string {
  return sanitizePlayerMessage(value, fallback).safe;
}

export function BlockList({ blocks, advancedEnabled = false }: { blocks: UiBlock[]; advancedEnabled?: boolean }) {
  return (
    <div className="block-list-container">
      {blocks.map((block, i) => <BlockRenderer key={`${block.kind}-${i}`} block={block} advancedEnabled={advancedEnabled} />)}
    </div>
  );
}
