import type { ReactNode } from 'react';
import type { UiBlock, UiTone } from '../api/contracts';
import { toPlayerFacingText } from '../utils/playerCopy';

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

export function BlockRenderer({ block }: { block: UiBlock }): ReactNode {
  switch (block.kind) {
    case 'text':
      return <p className={`block-text ${toneClassName(block.tone)}`}>{block.text}</p>;

    case 'panel':
      return (
        <section className="block-panel">
          <h4 className="block-panel__title">{toPlayerFacingText(block.title, 'Панель')}</h4>
          <div className="block-panel__body">
            {block.blocks.map((child, i) => <BlockRenderer key={`${child.kind}-${i}`} block={child} />)}
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
                <tr>{block.columns.map((col) => <th key={col}>{col}</th>)}</tr>
              </thead>
              <tbody>
                {block.rows.map((row, i) => (
                  <tr key={i}>{row.cells.map((cell, j) => <td key={j}>{cell}</td>)}</tr>
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
          {block.items.map((item, i) => <li key={i}>{item}</li>)}
        </ListTag>
      );
    }

    case 'keyValueGrid':
      return (
        <dl className="block-kv">
          {block.items.map((item) => (
            <div key={item.key} className="block-kv__row">
              <dt>{item.key}</dt>
              <dd>{item.value}</dd>
            </div>
          ))}
        </dl>
      );

    case 'message': {
      const severityClass = `block-message--${block.severity.toLowerCase()}`;
      return (
        <div className={`block-message ${severityClass}`}>
          <strong>{toPlayerFacingText(block.title, 'Сообщение')}</strong>
          <p>{toPlayerFacingText(block.message, '')}</p>
        </div>
      );
    }

    case 'image':
      return (
        <figure className="block-image">
          {block.url ? (
            <img src={block.url} alt={block.altText || block.title} loading="lazy" />
          ) : (
            <p className="block-text--muted">Изображение недоступно</p>
          )}
          {block.title && <figcaption>{block.title}</figcaption>}
        </figure>
      );

    case 'map':
      return (
        <div className="block-map">
          <h4>{toPlayerFacingText(block.title, 'Карта')}</h4>
          <p className="block-text--muted">Карта: {block.map.nodes.length} точек, {block.map.links.length} связей</p>
          <ul className="block-map__nodes">
            {block.map.nodes.slice(0, 20).map((node) => (
              <li key={node.id} className={node.isCurrent ? 'is-current' : ''}>
                {node.label} {node.isCurrent && '← вы здесь'}
              </li>
            ))}
            {block.map.nodes.length > 20 && <li className="block-text--muted">…и ещё {block.map.nodes.length - 20}</li>}
          </ul>
        </div>
      );

    case 'rawJson':
      return (
        <details className="block-raw">
          <summary>{toPlayerFacingText(block.title, 'Данные')}</summary>
          <pre>{JSON.stringify(block.json, null, 2)}</pre>
        </details>
      );
  }
}

export function BlockList({ blocks }: { blocks: UiBlock[] }) {
  return (
    <div className="block-list-container">
      {blocks.map((block, i) => <BlockRenderer key={`${block.kind}-${i}`} block={block} />)}
    </div>
  );
}
