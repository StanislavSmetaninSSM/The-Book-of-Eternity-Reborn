import type { CSSProperties, ReactNode } from 'react';
import type { UiBlock, UiTableBlock, UiTone } from '../api/contracts';
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

export function BlockRenderer({
  block,
  advancedEnabled = false,
  depth = 0
}: {
  block: UiBlock;
  advancedEnabled?: boolean;
  depth?: number;
}): ReactNode {
  switch (block.kind) {
    case 'text': {
      const { safe, hasTechnical } = sanitizePlayerMessage(block.text, 'Текст действия недоступен.');
      return <p className={`block-text ${hasTechnical ? 'block-text--muted' : toneClassName(block.tone)}`}>{safe}</p>;
    }

    case 'panel':
      return (
        <section className={`block-panel${depth > 0 ? ' block-panel--nested' : ''}`} data-block-depth={depth}>
          <h4 className="block-panel__title">{toPlayerFacingText(block.title, 'Панель')}</h4>
          <div className="block-panel__body">
            {block.blocks.map((child, i) => (
              <BlockRenderer
                key={`${child.kind}-${i}`}
                block={child}
                advancedEnabled={advancedEnabled}
                depth={depth + 1}
              />
            ))}
          </div>
        </section>
      );

    case 'entityDossier':
      return renderEntityDossier(block, advancedEnabled, depth);

    case 'table':
      {
        const structuredBonus = renderStructuredBonusTable(block);
        if (structuredBonus) return structuredBonus;
      }
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

    case 'keyValueGrid': {
      const rows = block.items.map((item) => {
        const key = toSafeBlockText(item.key, 'параметр');
        const value = toSafeBlockText(item.value, 'значение');
        return { key, value, meter: parseResourceMeter(key, value) };
      });
      const hasMeters = rows.some((row) => row.meter);

      return (
        <dl className={`block-kv${hasMeters ? ' block-kv--with-meters' : ''}`}>
          {rows.map((row) => (
            <div key={row.key} className={`block-kv__row${row.meter ? ' block-kv__row--meter' : ''}`}>
              <dt>{row.key}</dt>
              <dd>{row.meter ? renderResourceMeter(row.key, row.value, row.meter) : row.value}</dd>
            </div>
          ))}
        </dl>
      );
    }

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

      return null;
  }
}

type EntityDossierBlock = Extract<UiBlock, { kind: 'entityDossier' }>;
type EntityDossierSection = EntityDossierBlock['sections'][number];

function renderEntityDossier(block: EntityDossierBlock, advancedEnabled: boolean, depth: number): ReactNode {
  const title = toSafeBlockText(block.title, 'Досье');
  const subtitle = toSafeBlockText(block.subtitle, '');
  const summary = toSafeBlockText(block.summary, '');
  const media = block.media;

  return (
    <article className="entity-dossier" data-entity-type={toSafeBlockText(block.entityType, 'entity')}>
      <div className={`entity-dossier__header${media?.url ? ' entity-dossier__header--with-media' : ''}`}>
        <div className="entity-dossier__identity">
          <span className="entity-dossier__sigil" aria-hidden="true">
            <EntityGlyph icon={block.entityType || 'entity'} />
          </span>
          <div>
            <h3>{title}</h3>
            {subtitle && <p className="entity-dossier__subtitle">{subtitle}</p>}
          </div>
        </div>

        {block.badges.length > 0 && (
          <div className="entity-dossier__badges" aria-label="Метки досье">
            {block.badges.map((badge, index) => (
              <span
                className={`entity-dossier__badge entity-dossier__badge--${badge.tone.toLowerCase()}`}
                key={`${badge.label}-${index}`}
              >
                <EntityGlyph icon={badge.icon || 'badge'} />
                {toSafeBlockText(badge.label, 'метка')}
              </span>
            ))}
          </div>
        )}

        {media?.url && (
          <figure className="entity-dossier__media">
            <img src={media.url} alt={media.altText || media.title || title} loading="lazy" />
            {(media.title || media.altText) && (
              <figcaption>{toSafeBlockText(media.title || media.altText, 'Образ')}</figcaption>
            )}
          </figure>
        )}
      </div>

      {summary && <p className="entity-dossier__summary">{summary}</p>}

      {block.sections.length > 0 && (
        <div className="entity-dossier__sections">
          {block.sections.map((section, index) => renderEntityDossierSection(section, index, advancedEnabled, depth + 1))}
        </div>
      )}
    </article>
  );
}

function renderEntityDossierSection(
  section: EntityDossierSection,
  index: number,
  advancedEnabled: boolean,
  depth: number
): ReactNode {
  const title = toSafeBlockText(section.title, 'Раздел');
  const summary = toSafeBlockText(section.summary, '');
  const key = section.id || `${title}-${index}`;
  const content = (
    <div className="entity-dossier-section__body">
      {summary && <p className="entity-dossier-section__summary">{summary}</p>}
      {section.blocks.map((child, childIndex) => (
        <BlockRenderer
          key={`${child.kind}-${childIndex}`}
          block={child}
          advancedEnabled={advancedEnabled}
          depth={depth + 1}
        />
      ))}
    </div>
  );

  if (section.collapsible) {
    return (
      <details className="entity-dossier-section" key={key} open={section.initiallyExpanded}>
        <summary>
          <EntityGlyph icon={section.icon || 'section'} />
          <span>{title}</span>
        </summary>
        {content}
      </details>
    );
  }

  return (
    <section className="entity-dossier-section" key={key}>
      <h4>
        <EntityGlyph icon={section.icon || 'section'} />
        <span>{title}</span>
      </h4>
      {content}
    </section>
  );
}

function EntityGlyph({ icon }: { icon: string }) {
  const normalized = icon.trim().toLowerCase();
  const path = normalized.includes('skill')
    ? 'M4 15l7-7 3 3 6-6M5 19h14'
    : normalized.includes('archive')
      ? 'M5 5h14v14H5zM8 8h8M8 12h8M8 16h5'
      : normalized.includes('relation') || normalized.includes('npc')
        ? 'M8 19c0-3 8-3 8 0M8.5 8.5a3.5 3.5 0 117 0 3.5 3.5 0 01-7 0'
        : 'M12 3l2.4 5 5.6.8-4 3.9.9 5.5L12 15.6 7.1 18.2l.9-5.5-4-3.9 5.6-.8L12 3z';

  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d={path} />
    </svg>
  );
}

type StructuredBonusField = {
  field: string;
  value: string;
};

function renderStructuredBonusTable(block: UiTableBlock): ReactNode | null {
  if (!isStructuredBonusTable(block)) return null;

  const groups = new Map<string, StructuredBonusField[]>();
  for (const row of block.rows) {
    const title = toSafeBlockText(row.cells[0], 'Бонус');
    const field = toSafeBlockText(row.cells[1], 'Поле');
    const value = toSafeBlockText(row.cells[2], '—');
    if (!title || !field || !value || value === '—') continue;

    const existing = groups.get(title) ?? [];
    if (!(field === 'Кратко' && value === title)) {
      existing.push({ field, value });
    }
    groups.set(title, existing);
  }

  if (groups.size === 0) return null;

  return (
    <section className="structured-bonus-list" aria-label="Структурные бонусы">
      <h4 className="structured-bonus-list__title">{toPlayerFacingText(block.title, 'Структурные бонусы')}</h4>
      <div className="structured-bonus-list__grid">
        {[...groups.entries()].map(([title, fields], index) => (
          <article className="structured-bonus-card" key={`${title}-${index}`}>
            <h5>{title}</h5>
            {fields.length > 0 && (
              <dl>
                {fields.map((field, fieldIndex) => (
                  <div key={`${field.field}-${fieldIndex}`}>
                    <dt>{field.field}</dt>
                    <dd>{field.value}</dd>
                  </div>
                ))}
              </dl>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}

function isStructuredBonusTable(block: UiTableBlock): boolean {
  const title = block.title.trim().toLowerCase();
  if (title !== 'структурные бонусы') return false;
  if (block.columns.length < 3) return false;

  return block.columns[0].trim().toLowerCase() === 'бонус' &&
    block.columns[1].trim().toLowerCase() === 'поле' &&
    block.columns[2].trim().toLowerCase() === 'значение';
}

function toSafeBlockText(value: string | null | undefined, fallback: string): string {
  return sanitizePlayerMessage(value, fallback).safe;
}

type ResourceMeter = {
  kind: 'health' | 'energy' | 'poise';
  percent: number;
};

function parseResourceMeter(key: string, value: string): ResourceMeter | null {
  const normalizedKey = key.trim().toLowerCase();
  const kind =
    normalizedKey === 'здоровье' || normalizedKey === 'health'
      ? 'health'
      : normalizedKey === 'энергия' || normalizedKey === 'energy'
        ? 'energy'
        : normalizedKey === 'равновесие' || normalizedKey === 'poise' || normalizedKey === 'balance'
          ? 'poise'
          : null;
  if (!kind) return null;

  const match = /(-?\d+(?:[.,]\d+)?)\s*%/.exec(value);
  if (!match) return null;

  const parsed = Number.parseFloat(match[1].replace(',', '.'));
  if (!Number.isFinite(parsed)) return null;

  return { kind, percent: Math.min(100, Math.max(0, parsed)) };
}

function renderResourceMeter(key: string, value: string, meter: ResourceMeter) {
  const style = { '--meter-value': `${meter.percent}%` } as CSSProperties;

  return (
    <span
      className={`command-resource-meter command-resource-meter--${meter.kind}`}
      aria-label={`${key}: ${value}`}
    >
      <span className="command-resource-meter__track" aria-hidden="true">
        <span className="command-resource-meter__fill" style={style} />
      </span>
      <span className="command-resource-meter__value">{value}</span>
    </span>
  );
}

export function BlockList({ blocks, advancedEnabled = false }: { blocks: UiBlock[]; advancedEnabled?: boolean }) {
  return (
    <div className="block-list-container">
      {blocks.map((block, i) => <BlockRenderer key={`${block.kind}-${i}`} block={block} advancedEnabled={advancedEnabled} />)}
    </div>
  );
}
