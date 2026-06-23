import { useEffect, useMemo, useRef, useState, type CSSProperties, type MouseEvent, type ReactNode } from 'react';
import type { UiAction, UiBlock, UiTableBlock, UiTone } from '../api/contracts';
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
  depth = 0,
  onAction,
  availableActions = []
}: {
  block: UiBlock;
  advancedEnabled?: boolean;
  depth?: number;
  onAction?: (action: UiAction) => void;
  availableActions?: UiAction[];
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
                onAction={onAction}
                availableActions={availableActions}
              />
            ))}
          </div>
        </section>
      );

    case 'entityDossier':
      return renderEntityDossier(block, advancedEnabled, depth, onAction, availableActions);

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
type BackendEntityCard = EntityDossierBlock['cards'][number];

function renderEntityDossier(
  block: EntityDossierBlock,
  advancedEnabled: boolean,
  depth: number,
  onAction: ((action: UiAction) => void) | undefined,
  availableActions: UiAction[]
): ReactNode {
  if (depth > 0) {
    return renderEntityCard(entityDossierToCard(block, advancedEnabled, depth), false, depth, onAction, availableActions);
  }

  return <EntityDossierView block={block} advancedEnabled={advancedEnabled} depth={depth} onAction={onAction} availableActions={availableActions} />;
}

function EntityDossierView({
  block,
  advancedEnabled,
  depth,
  onAction,
  availableActions
}: {
  block: EntityDossierBlock;
  advancedEnabled: boolean;
  depth: number;
  onAction?: (action: UiAction) => void;
  availableActions: UiAction[];
}) {
  const dossier = entityDossierToView(block, advancedEnabled);
  const toc = dossier.sections.length > 0;
  const rootRef = useRef<HTMLDivElement | null>(null);
  const [activeSection, setActiveSection] = useState(0);

  useEffect(() => {
    const root = rootRef.current;
    if (!root || dossier.sections.length === 0) return;

    const sections = Array.from(root.querySelectorAll<HTMLElement>('.dossier-section[data-section-index]'));
    if (sections.length === 0) return;

    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        const index = Number(entry.target.getAttribute('data-section-index'));
        if (Number.isFinite(index)) setActiveSection(index);
      }
    }, {
      rootMargin: '-10% 0px -60% 0px',
      threshold: 0
    });

    sections.forEach((section) => observer.observe(section));
    return () => observer.disconnect();
  }, [dossier.sections.length]);

  const scrollToSection = (event: MouseEvent<HTMLAnchorElement>, index: number) => {
    event.preventDefault();
    const root = rootRef.current;
    const target = root?.querySelector<HTMLElement>(`.dossier-section[data-section-index="${index}"]`);
    target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    setActiveSection(index);
  };

  return (
    <div className="dossier-layout" data-entity-type={dossier.entityType} ref={rootRef}>
      <div className="dossier-main">
        <article className="entity-dossier">
          <header className="dossier-header">
            <div>
              <div className="dossier-title-row">
                <span className="dossier-icon" aria-hidden="true">
                  <EntityGlyph icon={dossier.entityType} />
                </span>
                <div>
                  <h2>{dossier.title}</h2>
                  {dossier.subtitle && <p className="dossier-subtitle">{dossier.subtitle}</p>}
                </div>
              </div>
              {dossier.summary && <p className="dossier-summary">{dossier.summary}</p>}
              {renderBadges(dossier.badges)}
            </div>
            {dossier.media && (
              <aside className="dossier-media">
                <MediaPreview media={dossier.media} fallbackTitle={dossier.title} />
              </aside>
            )}
          </header>
          <div className="dossier-body">
            {renderFacts(dossier.facts)}
            {renderMetrics(dossier.metrics)}
            {renderHints(dossier.hints)}
            {renderCleanList(filterRepeatedListItems(dossier.list, duplicateSourcesForCard(dossier)))}
            {renderDossierCards(dossier, onAction, availableActions)}
            {dossier.sections.map((section, index) => renderPrototypeSection(section, index, advancedEnabled, depth + 1, onAction, availableActions))}
          </div>
        </article>
      </div>
      {toc && (
        <nav className="dossier-toc" aria-label="Навигация по досье">
          <p className="dossier-toc__title">Навигация</p>
          {dossier.sections.map((section, index) => (
            <a
              className={activeSection === index ? 'is-active' : undefined}
              href={`#section-${section.id || index}`}
              key={`${section.id}-${index}`}
              data-section={index}
              onClick={(event) => scrollToSection(event, index)}
            >
              <span className="toc-icon" aria-hidden="true">
                <EntityGlyph icon={section.icon || 'default'} />
              </span>
              {section.title}
            </a>
          ))}
        </nav>
      )}
    </div>
  );
}

function renderDossierCards(
  dossier: PrototypeDossier,
  onAction: ((action: UiAction) => void) | undefined,
  availableActions: UiAction[]
): ReactNode {
  if (dossier.cards.length === 0) return null;

  if (dossier.cards.length > 8) {
    return (
      <CollectionBrowser
        cards={dossier.cards}
        onAction={onAction}
        availableActions={availableActions}
        section={{
          id: `${toSafeId(dossier.title)}-cards`,
          title: dossier.title,
          summary: dossier.summary,
          icon: dossier.icon,
          collectionLabel: `${dossier.cards.length} объектов в разделе`,
          presentation: 'collection',
          collapsible: true,
          initiallyExpanded: true,
          facts: [],
          metrics: [],
          hints: [],
          list: [],
          cards: dossier.cards,
          fallbackBlocks: []
        }}
      />
    );
  }

  return (
    <div className="card-grid">
      {dossier.cards.map((card, index) => renderEntityCard(card, false, index + 1, onAction, availableActions))}
    </div>
  );
}

type PrototypeBadge = {
  label: string;
  tone: string;
  icon: string;
};

type PrototypeMedia = {
  title: string;
  url: string;
  fullUrl: string;
  altText: string;
};

type PrototypeFact = {
  label: string;
  value: string;
};

type PrototypeFactRow = {
  label: string;
  value: string;
};

type PrototypeMetric = {
  label: string;
  value: number;
  max: number;
  tone: string;
  note: string;
};

type PrototypeHint = {
  title: string;
  text: string;
  tone: string;
};

type PrototypeCard = {
  title: string;
  subtitle: string;
  summary: string;
  icon: string;
  badges: PrototypeBadge[];
  media: PrototypeMedia | null;
  facts: PrototypeFact[];
  metrics: PrototypeMetric[];
  hints: PrototypeHint[];
  list: string[];
  nested: PrototypeCard[];
  cards: PrototypeCard[];
  primaryAction: UiAction | null;
};

type PrototypeSection = {
  id: string;
  title: string;
  summary: string;
  icon: string;
  collectionLabel: string;
  presentation: string;
  collapsible: boolean;
  initiallyExpanded: boolean;
  facts: PrototypeFact[];
  metrics: PrototypeMetric[];
  hints: PrototypeHint[];
  list: string[];
  cards: PrototypeCard[];
  fallbackBlocks: UiBlock[];
};

type PrototypeDossier = PrototypeCard & {
  entityType: string;
  sections: PrototypeSection[];
};

function entityDossierToView(block: EntityDossierBlock, advancedEnabled: boolean): PrototypeDossier {
  const parts = collectPrototypeParts([], advancedEnabled, 1);

  return {
    ...entityDossierToCard(block, advancedEnabled, 0),
    entityType: toSafeBlockText(block.entityType, 'entity'),
    facts: [...toPrototypeFacts(block.facts), ...parts.facts],
    metrics: [...toPrototypeMetrics(block.metrics), ...parts.metrics],
    hints: [...toPrototypeHints(block.hints), ...parts.hints],
    list: [...toPrototypeList(block.list), ...parts.list],
    cards: [...toPrototypeCards(block.cards, advancedEnabled, 0), ...parts.cards],
    sections: block.sections.map((section) => entitySectionToPrototype(section, advancedEnabled))
  };
}

function entityDossierToCard(block: EntityDossierBlock, advancedEnabled: boolean, depth: number): PrototypeCard {
  const parts = collectPrototypePartsFromSections(block.sections, advancedEnabled, depth + 1);

  return {
    title: toSafeBlockText(block.title, 'Досье'),
    subtitle: toSafeBlockText(block.subtitle, ''),
    summary: toSafeBlockText(block.summary, ''),
    icon: toSafeBlockText(block.entityType, 'default'),
    badges: block.badges.map((badge) => ({
      label: toSafeBlockText(badge.label, 'метка'),
      tone: prototypeTone(badge.tone),
      icon: badge.icon || block.entityType || 'default'
    })),
    media: mediaToPrototype(block.media),
    facts: [...toPrototypeFacts(block.facts), ...parts.facts],
    metrics: [...toPrototypeMetrics(block.metrics), ...parts.metrics],
    hints: [...toPrototypeHints(block.hints), ...parts.hints],
    list: [...toPrototypeList(block.list), ...parts.list],
    nested: [...parts.nested],
    cards: [...toPrototypeCards(block.cards, advancedEnabled, depth + 1), ...parts.cards],
    primaryAction: block.primaryAction ?? null
  };
}

function entitySectionToPrototype(section: EntityDossierSection, advancedEnabled: boolean): PrototypeSection {
  const parts = collectPrototypeParts(section.blocks, advancedEnabled, 1);

  return {
    id: toSafeId(section.id || section.title || 'section'),
    title: toSafeBlockText(section.title, 'Раздел'),
    summary: toSafeBlockText(section.summary, ''),
    icon: toSafeBlockText(section.icon, 'section'),
    collectionLabel: toSafeBlockText(section.collectionLabel || section.summary, ''),
    presentation: toSafeBlockText(section.presentation, ''),
    collapsible: section.collapsible,
    initiallyExpanded: section.initiallyExpanded,
    facts: [...toPrototypeFacts(section.facts), ...parts.facts],
    metrics: [...toPrototypeMetrics(section.metrics), ...parts.metrics],
    hints: [...toPrototypeHints(section.hints), ...parts.hints],
    list: [...toPrototypeList(section.list), ...parts.list],
    cards: [...toPrototypeCards(section.cards, advancedEnabled, 1), ...parts.cards],
    fallbackBlocks: parts.fallbackBlocks
  };
}

function collectPrototypePartsFromSections(
  sections: EntityDossierSection[],
  advancedEnabled: boolean,
  depth: number
) {
  const parts = collectPrototypeParts([], advancedEnabled, depth);
  for (const section of sections) {
    const card = sectionToNestedCard(section, advancedEnabled, depth);
    if (card) parts.nested.push(card);
  }

  return parts;
}

function sectionToNestedCard(
  section: EntityDossierSection,
  advancedEnabled: boolean,
  depth: number
): PrototypeCard | null {
  const parts = collectPrototypeParts(section.blocks, advancedEnabled, depth + 1);
  if (!section.title && partsIsEmpty(parts)) return null;

  return {
    title: toSafeBlockText(section.title, 'Раздел'),
    subtitle: '',
    summary: toSafeBlockText(section.summary, ''),
    icon: toSafeBlockText(section.icon, 'section'),
    badges: [],
    media: null,
    facts: [...toPrototypeFacts(section.facts), ...parts.facts],
    metrics: [...toPrototypeMetrics(section.metrics), ...parts.metrics],
    hints: [...toPrototypeHints(section.hints), ...parts.hints],
    list: [...toPrototypeList(section.list), ...parts.list],
    nested: parts.nested,
    cards: [...toPrototypeCards(section.cards, advancedEnabled, depth + 1), ...parts.cards],
    primaryAction: null
  };
}

function toPrototypeCards(cards: BackendEntityCard[], advancedEnabled: boolean, depth: number): PrototypeCard[] {
  return (cards ?? []).map((card) => ({
    title: toSafeBlockText(card.title, 'Карточка'),
    subtitle: toSafeBlockText(card.subtitle, ''),
    summary: toSafeBlockText(card.summary, ''),
    icon: toSafeBlockText(card.icon, 'default'),
    badges: card.badges.map((badge) => ({
      label: toSafeBlockText(badge.label, 'метка'),
      tone: prototypeTone(badge.tone),
      icon: badge.icon || card.icon || 'default'
    })),
    media: mediaToPrototype(card.media),
    facts: toPrototypeFacts(card.facts),
    metrics: toPrototypeMetrics(card.metrics),
    hints: toPrototypeHints(card.hints),
    list: toPrototypeList(card.list),
    nested: toPrototypeCards(card.nested, advancedEnabled, depth + 1),
    cards: toPrototypeCards(card.cards, advancedEnabled, depth + 1),
    primaryAction: card.primaryAction ?? null
  }));
}

function toPrototypeFacts(facts: EntityDossierBlock['facts']): PrototypeFact[] {
  return (facts ?? []).map((fact) => ({
    label: toSafeBlockText(fact.label, 'Параметр'),
    value: toSafeBlockText(fact.value, '—')
  }));
}

function toPrototypeMetrics(metrics: EntityDossierBlock['metrics']): PrototypeMetric[] {
  return (metrics ?? []).map((metric) => ({
    label: toSafeBlockText(metric.label, 'Показатель'),
    value: Number(metric.value) || 0,
    max: Number(metric.max) || 100,
    tone: prototypeTone(metric.tone),
    note: toSafeBlockText(metric.note, '')
  }));
}

function toPrototypeHints(hints: EntityDossierBlock['hints']): PrototypeHint[] {
  return (hints ?? []).map((hint) => ({
    title: toSafeBlockText(hint.title, 'Подсказка'),
    text: toSafeBlockText(hint.text, ''),
    tone: prototypeTone(hint.tone)
  })).filter((hint) => hint.text || hint.title);
}

function toPrototypeList(items: string[]): string[] {
  return (items ?? []).map((item) => toSafeBlockText(item, '')).filter(Boolean);
}

function collectPrototypeParts(blocks: UiBlock[], advancedEnabled: boolean, depth: number) {
  const facts: PrototypeFact[] = [];
  const metrics: PrototypeMetric[] = [];
  const hints: PrototypeHint[] = [];
  const list: string[] = [];
  const nested: PrototypeCard[] = [];
  const cards: PrototypeCard[] = [];
  const fallbackBlocks: UiBlock[] = [];

  for (const child of blocks) {
    switch (child.kind) {
      case 'entityDossier':
        cards.push(entityDossierToCard(child, advancedEnabled, depth));
        break;
      case 'keyValueGrid':
        for (const item of child.items) {
          const key = toSafeBlockText(item.key, 'Параметр');
          const value = toSafeBlockText(item.value, '—');
          const meter = parseResourceMeter(key, value);
          if (meter) {
            metrics.push({
              label: key,
              value: Math.round(meter.percent),
              max: 100,
              tone: metricTone(meter.kind, meter.percent),
              note: ''
            });
          } else {
            facts.push({ label: key, value });
          }
        }
        break;
      case 'list':
        list.push(...child.items.map((item) => toSafeBlockText(item, 'пункт списка')));
        break;
      case 'text':
        if (child.text.trim()) {
          hints.push({
            title: 'Заметка',
            text: toSafeBlockText(child.text, 'Текст недоступен.'),
            tone: prototypeTone(child.tone)
          });
        }
        break;
      case 'message':
        hints.push({
          title: toSafeBlockText(child.title, 'Сообщение'),
          text: toSafeBlockText(child.message, 'Игровое действие изменило состояние.'),
          tone: prototypeSeverityTone(child.severity)
        });
        break;
      case 'panel': {
        const panelParts = collectPrototypeParts(child.blocks, advancedEnabled, depth + 1);
        cards.push({
          title: toSafeBlockText(child.title, 'Раздел'),
          subtitle: '',
          summary: '',
          icon: 'archive',
          badges: [],
          media: null,
          facts: panelParts.facts,
          metrics: panelParts.metrics,
          hints: panelParts.hints,
          list: panelParts.list,
          nested: panelParts.nested,
          cards: panelParts.cards,
          primaryAction: null
        });
        break;
      }
      case 'image':
        cards.push({
          title: toSafeBlockText(child.title, 'Изображение'),
          subtitle: 'образ',
          summary: '',
          icon: 'memory',
          badges: [],
          media: mediaToPrototype(child),
          facts: [],
          metrics: [],
          hints: [],
          list: [],
          nested: [],
          cards: [],
          primaryAction: null
        });
        break;
      case 'rawJson':
        if (advancedEnabled) fallbackBlocks.push(child);
        break;
      case 'table':
      case 'map':
        fallbackBlocks.push(child);
        break;
    }
  }

  return { facts, metrics, hints, list, nested, cards, fallbackBlocks };
}

function partsIsEmpty(parts: ReturnType<typeof collectPrototypeParts>): boolean {
  return parts.facts.length === 0 &&
    parts.metrics.length === 0 &&
    parts.hints.length === 0 &&
    parts.list.length === 0 &&
    parts.nested.length === 0 &&
    parts.cards.length === 0 &&
    parts.fallbackBlocks.length === 0;
}

function renderPrototypeSection(
  section: PrototypeSection,
  index: number,
  advancedEnabled: boolean,
  depth: number,
  onAction: ((action: UiAction) => void) | undefined,
  availableActions: UiAction[]
): ReactNode {
  const key = section.id || `${section.title}-${index}`;
  const relationClass = isRelationSection(section) ? ' relation-group' : '';
  const content = renderPrototypeSectionContent(section, advancedEnabled, depth, onAction, availableActions);

  return (
    <details
      className={`dossier-section${relationClass}`}
      id={`section-${section.id || index}`}
      data-section-index={index}
      key={key}
      open={section.initiallyExpanded}
    >
      <summary className="dossier-section__summary">
        <header className="dossier-section__header">
          <span className="section-icon" aria-hidden="true">
            <EntityGlyph icon={section.icon || 'section'} />
          </span>
          <div>
            <span className="dossier-section__eyebrow">Раздел досье</span>
            <h3>{section.title}</h3>
            {section.collectionLabel && <p className="collection-label">{section.collectionLabel}</p>}
          </div>
        </header>
        <span className="collapse-pill" aria-hidden="true">
          <EntityGlyph icon="chevron" />
        </span>
      </summary>
      <div className="dossier-section__content">
        {content}
      </div>
    </details>
  );
}

function renderPrototypeSectionContent(
  section: PrototypeSection,
  advancedEnabled: boolean,
  depth: number,
  onAction: ((action: UiAction) => void) | undefined,
  availableActions: UiAction[]
): ReactNode {
  const hasPrimitiveContent = section.facts.length > 0 ||
    section.metrics.length > 0 ||
    section.hints.length > 0 ||
    section.list.length > 0 ||
    section.fallbackBlocks.length > 0;

  return (
    <>
      {renderFacts(section.facts)}
      {renderMetrics(section.metrics)}
      {renderHints(section.hints)}
      {renderCleanList(filterRepeatedListItems(section.list, duplicateSourcesForSection(section)))}
      {shouldRenderAsCollection(section) ? (
        <CollectionBrowser cards={section.cards} section={section} onAction={onAction} availableActions={availableActions} />
      ) : section.cards.length > 0 ? (
        <div className="card-grid">
          {section.cards.map((card, index) => renderEntityCard(card, false, depth + index, onAction, availableActions))}
        </div>
      ) : !hasPrimitiveContent && section.summary ? (
        <p className="card-summary">{section.summary}</p>
      ) : null}
      {section.fallbackBlocks.map((child, childIndex) => (
        <BlockRenderer
          key={`${child.kind}-${childIndex}`}
          block={child}
          advancedEnabled={advancedEnabled}
          depth={depth + 1}
          onAction={onAction}
          availableActions={availableActions}
        />
      ))}
    </>
  );
}

function shouldRenderAsCollection(section: PrototypeSection): boolean {
  return section.cards.length > 8 ||
    section.presentation.trim().toLowerCase() === 'collection';
}

function renderEntityCard(
  card: PrototypeCard,
  nested: boolean,
  depth: number,
  onAction: ((action: UiAction) => void) | undefined,
  availableActions: UiAction[]
): ReactNode {
  const collapsible = shouldCollapseCard(card, nested, depth);
  const hasMedia = Boolean(card.media?.url);
  const cls = nested ? 'nested-card' : 'entity-card';
  const depthAttr = nested ? { 'data-depth': depth } : {};
  const header = renderCardHeader(card, nested);
  const primaryAction = resolveCardPrimaryAction(card, availableActions);
  const visibleList = filterRepeatedListItems(card.list, duplicateSourcesForCard(card));
  const body = (
    <>
      {!collapsible && header}
      {card.summary && <p className="card-summary">{card.summary}</p>}
      {renderCardPrimaryAction(primaryAction, onAction)}
      {renderBadges(card.badges)}
      {renderMetrics(card.metrics)}
      {renderFacts(card.facts)}
      {renderHints(card.hints)}
      {renderCleanList(visibleList)}
      {card.nested.length > 0 && (
        <div className="nested-stack">
          {card.nested.map((child, index) => renderEntityCard(child, true, depth + index + 1, onAction, availableActions))}
        </div>
      )}
      {card.cards.length > 0 && (
        <div className="card-list">
          {card.cards.map((child, index) => renderEntityCard(child, true, depth + index + 1, onAction, availableActions))}
        </div>
      )}
    </>
  );

  if (collapsible) {
    return (
      <details className={`${cls} collapsible-card`} {...depthAttr} key={`${card.title}-${depth}`}>
        <summary className="collapsible-card__summary">
          {header}
          {card.summary && <p>{card.summary}</p>}
          <span className="collapse-pill" aria-hidden="true">
            <EntityGlyph icon="chevron" />
          </span>
        </summary>
        <div className="card-collapsible-body">
          {renderCardPrimaryAction(primaryAction, onAction)}
          {renderBadges(card.badges)}
          {renderMetrics(card.metrics)}
          {renderFacts(card.facts)}
          {renderHints(card.hints)}
          {renderCleanList(visibleList)}
          {card.nested.length > 0 && (
            <div className="nested-stack">
              {card.nested.map((child, index) => renderEntityCard(child, true, depth + index + 1, onAction, availableActions))}
            </div>
          )}
          {card.cards.length > 0 && (
            <div className="card-list">
              {card.cards.map((child, index) => renderEntityCard(child, true, depth + index + 1, onAction, availableActions))}
            </div>
          )}
        </div>
      </details>
    );
  }

  if (hasMedia && card.media) {
    return (
      <article className={`${cls} inline-media-card`} {...depthAttr} key={`${card.title}-${depth}`}>
        <MediaPreview media={card.media} fallbackTitle={card.title} />
        <div className="card-content">{body}</div>
      </article>
    );
  }

  return (
    <article className={cls} {...depthAttr} key={`${card.title}-${depth}`}>
      {body}
    </article>
  );
}

function renderCardHeader(card: PrototypeCard, nested: boolean): ReactNode {
  const Heading = nested ? 'h4' : 'h3';
  return (
    <div className={`${nested ? 'nested-card' : 'entity-card'}__header`}>
      <span className="card-icon" aria-hidden="true">
        <EntityGlyph icon={card.icon || 'default'} />
      </span>
      <div>
        {card.subtitle && <p className="card-overline">{card.subtitle}</p>}
        <Heading>{card.title}</Heading>
      </div>
    </div>
  );
}

function renderCardPrimaryAction(action: UiAction | null, onAction: ((action: UiAction) => void) | undefined): ReactNode {
  if (!action || !onAction) return null;

  return (
    <div className="entity-card__action-row">
      <button
        className="entity-card__open-action"
        type="button"
        onClick={(event) => {
          event.stopPropagation();
          onAction(action);
        }}
      >
        <EntityGlyph icon="open" />
        <span>{toSafeBlockText(action.label, 'Открыть отдельно')}</span>
      </button>
    </div>
  );
}

function resolveCardPrimaryAction(card: PrototypeCard, availableActions: UiAction[]): UiAction | null {
  if (card.primaryAction) return card.primaryAction;

  const title = normalizeEntityActionText(card.title);
  if (!title) return null;

  return availableActions.find((action) => {
    if (!isEntityOpenAction(action)) return false;

    const label = normalizeEntityActionText(action.label);
    const command = normalizeEntityActionText(action.command);
    return label.includes(title) || command.includes(title);
  }) ?? null;
}

function isEntityOpenAction(action: UiAction): boolean {
  const label = normalizeText(action.label);
  if (!label || label.includes('назад') || label.includes('снять') || label.includes('экипировать')) {
    return false;
  }

  return label.includes('открыть отдельно') ||
    label.includes('подробнее') ||
    label.includes('открыть') ||
    label.includes('читать');
}

function normalizeEntityActionText(value: string): string {
  return normalizeText(value)
    .replace(/[«»"]/g, ' ')
    .replace(/^предмет:\s*/i, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function CollectionBrowser({
  cards,
  section,
  onAction,
  availableActions
}: {
  cards: PrototypeCard[];
  section: PrototypeSection;
  onAction?: (action: UiAction) => void;
  availableActions: UiAction[];
}) {
  const [query, setQuery] = useState('');
  const [activeFilter, setActiveFilter] = useState('all');
  const [selectedIndex, setSelectedIndex] = useState(0);
  const filters = useMemo(() => collectionFilters(cards), [cards]);
  const normalizedQuery = normalizeText(query);
  const visibleIndexes = cards
    .map((card, index) => ({ card, index, tags: cardTags(card) }))
    .filter(({ card, tags }) => {
      const matchesQuery = !normalizedQuery || normalizeText(cardPlainText(card)).includes(normalizedQuery);
      const matchesFilter = activeFilter === 'all' || tags.includes(activeFilter);
      return matchesQuery && matchesFilter;
    });
  const selected = visibleIndexes.find((entry) => entry.index === selectedIndex) ?? visibleIndexes[0] ?? null;
  const collectionTitle = section.collectionLabel || formatCollectionCount(cards.length);
  const collectionSummary = section.summary || 'Выберите запись слева, чтобы увидеть подробную карточку.';

  return (
    <div className="collection-browser" data-collection-id={section.id}>
      <div className="collection-browser__overview">
        <div>
          <span className="collection-browser__eyebrow">{section.title}</span>
          <h4>{collectionTitle}</h4>
          <p>{collectionSummary}</p>
        </div>
        <div className="collection-featured">
          {cards.slice(0, 4).map((card, index) => (
            <button
              className="collection-featured-card"
              type="button"
              key={`${card.title}-${index}`}
              onClick={() => setSelectedIndex(index)}
            >
              <span aria-hidden="true"><EntityGlyph icon={card.icon || 'default'} /></span>
              <strong>{card.title}</strong>
              {card.subtitle && <em>{card.subtitle}</em>}
            </button>
          ))}
        </div>
      </div>

      <div className="collection-controls">
        <label className="collection-search">
          <span>Найти объект</span>
          <input
            type="search"
            placeholder="Название, описание, тип, пометка..."
            value={query}
            onChange={(event) => setQuery(event.currentTarget.value)}
          />
        </label>
        <div className="collection-filters" aria-label="Фильтры коллекции">
          {filters.map((filter) => (
            <button
              className={`collection-filter${filter.id === activeFilter ? ' is-active' : ''}`}
              type="button"
              key={filter.id}
              onClick={() => setActiveFilter(filter.id)}
            >
              {filter.label}
            </button>
          ))}
        </div>
      </div>

      <div className="collection-workbench">
        <aside className="collection-list" aria-label="Список объектов">
          <div className="collection-list__meta">
            <strong>{visibleIndexes.length}</strong>
            <span>показано</span>
          </div>
          <div className="collection-list__items">
            {visibleIndexes.map(({ card, index, tags }) => (
              <button
                className={`collection-list-item${selected?.index === index ? ' is-active' : ''}`}
                type="button"
                key={`${card.title}-${index}`}
                onClick={() => setSelectedIndex(index)}
              >
                <span className="collection-list-item__icon" aria-hidden="true">
                  <EntityGlyph icon={card.icon || 'default'} />
                </span>
                <span className="collection-list-item__body">
                  <strong>{card.title}</strong>
                  {card.subtitle && <em>{card.subtitle}</em>}
                  {card.summary && <span>{card.summary}</span>}
                </span>
                <span className="collection-list-item__tags">
                  {tags.slice(0, 2).map((tag) => <i key={tag}>{tagLabel(tag)}</i>)}
                </span>
              </button>
            ))}
          </div>
        </aside>
        <section className="collection-detail-panel" aria-live="polite">
          {selected ? renderEntityCard(selected.card, false, 0, onAction, availableActions) : (
            <div className="collection-detail-empty">
              <strong>Ничего не найдено</strong>
              <p>Попробуйте убрать фильтр или изменить поисковый запрос.</p>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

function renderFacts(items: PrototypeFact[]): ReactNode {
  if (items.length === 0) return null;
  return (
    <div className="fact-grid">
      {items.map((item, index) => {
        const rows = splitStructuredFactValue(item.value);
        return (
          <div className="fact-card" key={`${item.label}-${index}`}>
            <strong>{localizeFactLabel(item.label)}</strong>
            {rows.length > 0 ? (
              <dl className="structured-fact-list">
                {rows.map((row, rowIndex) => (
                  <div key={`${row.label}-${rowIndex}`}>
                    <dt>{row.label}</dt>
                    <dd>{row.value}</dd>
                  </div>
                ))}
              </dl>
            ) : (
              <span>{item.value}</span>
            )}
          </div>
        );
      })}
    </div>
  );
}

function splitStructuredFactValue(value: string): PrototypeFactRow[] {
  const normalized = value.trim();
  if (!normalized) return [];

  const rawParts = normalized
    .split(/[;\n]+/g)
    .map((part) => part.trim())
    .filter(Boolean);

  if (rawParts.length < 2) return [];

  const rows = rawParts
    .map((part) => {
      const separatorIndex = part.indexOf(':');
      if (separatorIndex <= 0) return null;

      const label = localizeFactLabel(part.slice(0, separatorIndex).trim());
      const rowValue = toSafeBlockText(part.slice(separatorIndex + 1).trim(), '—');
      if (!label || !rowValue || rowValue === '—') return null;
      return { label, value: rowValue };
    })
    .filter((row): row is PrototypeFactRow => row != null);

  return rows.length >= 2 ? rows : [];
}

function localizeFactLabel(label: string): string {
  const safe = toSafeBlockText(label, 'Параметр');
  const normalized = safe.trim().toLowerCase();
  const labels: Record<string, string> = {
    base: 'База',
    final: 'Итог',
    strength: 'Сила',
    dexterity: 'Ловкость',
    constitution: 'Выносливость',
    intelligence: 'Интеллект',
    wisdom: 'Мудрость',
    faith: 'Вера',
    attractiveness: 'Привлекательность',
    trade: 'Торговля',
    persuasion: 'Убеждение',
    perception: 'Восприятие',
    luck: 'Удача',
    source: 'Источник',
    target: 'Цель',
    value: 'Значение',
    expiresat: 'Действует до',
    equipmentbonuses: 'Бонусы снаряжения',
    temporarymodifiers: 'Временные модификаторы',
    actionname: 'Действие',
    actiondescription: 'Описание действия',
    damagetype: 'Тип урона',
    basedamage: 'Базовый урон',
    actioncost: 'Стоимость действия',
    cooldown: 'Перезарядка',
    range: 'Дистанция'
  };

  return labels[normalized.replace(/\s+/g, '')] || safe;
}

function renderMetrics(items: PrototypeMetric[]): ReactNode {
  if (items.length === 0) return null;
  return (
    <div className="metric-grid">
      {items.map((item, index) => {
        const percent = Math.max(0, Math.min(100, Math.round((Number(item.value) / Number(item.max || 100)) * 100)));
        const style = { '--metric-value': `${percent}%` } as CSSProperties;
        return (
          <div className="metric-card" data-tone={item.tone || 'gold'} key={`${item.label}-${index}`}>
            <strong>{item.label}</strong>
            <span className="metric-value">
              {item.value}
              <small> / {item.max || 100}</small>
            </span>
            <div className="metric-bar" style={style}><span /></div>
            {item.note && <p>{item.note}</p>}
          </div>
        );
      })}
    </div>
  );
}

function renderHints(items: PrototypeHint[]): ReactNode {
  if (items.length === 0) return null;
  return (
    <div className="hint-grid">
      {items.map((item, index) => (
        <article className="hint-card" data-tone={item.tone || 'gold'} key={`${item.title}-${index}`}>
          <h4>{item.title}</h4>
          <p>{item.text}</p>
        </article>
      ))}
    </div>
  );
}

function renderCleanList(items: string[]): ReactNode {
  if (items.length === 0) return null;
  return (
    <ul className="clean-list">
      {items.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}
    </ul>
  );
}

function filterRepeatedListItems(items: string[], alreadyShown: string[]): string[] {
  if (items.length === 0 || alreadyShown.length === 0) return items;

  const shown = new Set(alreadyShown.map(normalizeListDedupText).filter(Boolean));
  return items.filter((item) => !shown.has(normalizeListDedupText(item)));
}

function duplicateSourcesForCard(card: Pick<PrototypeCard, 'title' | 'subtitle' | 'summary' | 'badges' | 'facts' | 'metrics' | 'hints'>): string[] {
  return [
    card.title,
    card.subtitle,
    card.summary,
    ...card.badges.map((badgeItem) => badgeItem.label),
    ...card.facts.flatMap((fact) => [fact.label, fact.value]),
    ...card.metrics.flatMap((metric) => [metric.label, metric.note, String(metric.value), `${metric.value} / ${metric.max || 100}`]),
    ...card.hints.flatMap((hint) => [hint.title, hint.text])
  ];
}

function duplicateSourcesForSection(section: Pick<PrototypeSection, 'title' | 'summary' | 'collectionLabel' | 'facts' | 'metrics' | 'hints'>): string[] {
  return [
    section.title,
    section.summary,
    section.collectionLabel,
    ...section.facts.flatMap((fact) => [fact.label, fact.value]),
    ...section.metrics.flatMap((metric) => [metric.label, metric.note, String(metric.value), `${metric.value} / ${metric.max || 100}`]),
    ...section.hints.flatMap((hint) => [hint.title, hint.text])
  ];
}

function normalizeListDedupText(value: string): string {
  return normalizeText(value)
    .replace(/[.,;:!?()[\]«»"“”„]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function renderBadges(items: PrototypeBadge[]): ReactNode {
  if (items.length === 0) return null;
  return (
    <div className="badge-row">
      {items.map((badge, index) => (
        <span className="badge" data-tone={badge.tone || 'gold'} key={`${badge.label}-${index}`}>
          {badge.icon && <EntityGlyph icon={badge.icon} />}
          <span>{badge.label}</span>
        </span>
      ))}
    </div>
  );
}

function MediaPreview({ media, fallbackTitle }: { media: PrototypeMedia; fallbackTitle: string }) {
  const [open, setOpen] = useState(false);
  const dialogRef = useRef<HTMLDialogElement | null>(null);
  const alt = media.altText || media.title || fallbackTitle;
  const caption = media.title || media.altText || fallbackTitle;

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <>
      <button className="media-preview" type="button" onClick={() => setOpen(true)}>
        <figure>
          <img src={media.url} alt={alt} loading="lazy" />
          {caption && <figcaption>{caption}</figcaption>}
        </figure>
      </button>
      {open && (
        <dialog className="media-lightbox" ref={dialogRef} onClose={() => setOpen(false)}>
          <div className="media-lightbox__chrome">
            <button className="media-lightbox__close" type="button" aria-label="Закрыть изображение" onClick={() => setOpen(false)}>
              <EntityGlyph icon="close" />
            </button>
          </div>
          <figure>
            <img src={media.fullUrl || media.url} alt={alt} />
            {caption && <figcaption>{caption}</figcaption>}
          </figure>
        </dialog>
      )}
    </>
  );
}

function shouldCollapseCard(card: PrototypeCard, nested: boolean, depth: number): boolean {
  if (!nested) return false;
  const score = card.metrics.length +
    card.facts.length +
    card.hints.length +
    card.list.length +
    (card.nested.length * 2) +
    (card.cards.length * 2);
  if (depth <= 2) return score >= 10 || card.cards.length > 6 || card.nested.length > 6;
  return score >= 8 || card.cards.length > 4 || card.nested.length > 4;
}

function mediaToPrototype(media: EntityDossierBlock['media'] | Extract<UiBlock, { kind: 'image' }>): PrototypeMedia | null {
  if (!media?.url) return null;
  return {
    title: toSafeBlockText(media.title, ''),
    url: media.url,
    fullUrl: media.url,
    altText: 'altText' in media ? toSafeBlockText(media.altText, '') : ''
  };
}

function isRelationSection(section: PrototypeSection): boolean {
  const source = `${section.icon} ${section.title}`.toLowerCase();
  return source.includes('relation') ||
    source.includes('npc') ||
    source.includes('отношени') ||
    source.includes('фракци');
}

function cardPlainText(card: PrototypeCard): string {
  return [
    card.title,
    card.subtitle,
    card.summary,
    ...card.badges.map((badgeItem) => badgeItem.label),
    ...card.facts.flatMap((fact) => [fact.label, fact.value]),
    ...card.list,
    ...card.nested.map(cardPlainText),
    ...card.cards.map(cardPlainText)
  ].join(' ');
}

function cardTags(card: PrototypeCard): string[] {
  const typeSource = normalizeText([
    card.icon,
    card.subtitle,
    ...card.badges.map((badgeItem) => badgeItem.label)
  ].join(' '));
  const source = normalizeText(cardPlainText(card));
  const tags = new Set<string>();

  const isCharacter = typeSource.includes('npc') || typeSource.includes('персонаж') || typeSource.includes('person');
  if (isCharacter) {
    tags.add('characters');
    if (source.includes('повреж') || source.includes('риск') || source.includes('опасн')) tags.add('attention');
    if (card.media?.url) tags.add('media');
    return [...tags];
  }

  if (typeSource.includes('артефакт') || typeSource.includes('artifact')) tags.add('artifacts');
  if (typeSource.includes('документ') || typeSource.includes('document')) tags.add('documents');
  if (typeSource.includes('inventory') || typeSource.includes('item') || typeSource.includes('предмет')) tags.add('items');
  if (source.includes('документ') || source.includes('книга') || source.includes('письмо')) tags.add('documents');
  if (source.includes('инструмент') || source.includes('нож') || source.includes('ключ')) tags.add('tools');
  if (source.includes('запись') || source.includes('список')) tags.add('records');
  if (source.includes('квест') || source.includes('печать')) tags.add('quest');
  if (source.includes('артефакт') || source.includes('руна')) tags.add('artifacts');
  if (source.includes('повреж') || source.includes('риск') || source.includes('опасн')) tags.add('attention');
  if (card.media?.url) tags.add('media');

  return [...tags];
}

function collectionFilters(cards: PrototypeCard[]): Array<{ id: string; label: string }> {
  const available = new Set(cards.flatMap(cardTags));
  return ['all', 'characters', 'items', 'documents', 'tools', 'records', 'quest', 'artifacts', 'attention', 'media']
    .filter((key) => key === 'all' || available.has(key))
    .map((key) => ({ id: key, label: tagLabel(key) }));
}

function tagLabel(tag: string): string {
  const labels: Record<string, string> = {
    all: 'Все',
    characters: 'Персонажи',
    items: 'Предметы',
    documents: 'Документ',
    tools: 'Инструмент',
    records: 'Запись',
    quest: 'Квестовое',
    artifacts: 'Артефакт',
    attention: 'Внимание',
    media: 'Изображение'
  };

  return labels[tag] || tag;
}

function formatCollectionCount(count: number): string {
  if (count === 1) return '1 запись';
  if (count > 1 && count < 5) return `${count} записи`;
  return `${count} записей`;
}

function normalizeText(value: string): string {
  return String(value ?? '').toLocaleLowerCase('ru-RU');
}

function prototypeTone(tone: UiTone): string {
  switch (tone) {
    case 'Success': return 'success';
    case 'Warning': return 'warning';
    case 'Error': return 'danger';
    case 'Accent': return 'accent';
    case 'Muted':
    case 'Subtle':
      return 'muted';
    default:
      return 'gold';
  }
}

function prototypeSeverityTone(severity: string): string {
  switch (severity) {
    case 'Success': return 'success';
    case 'Warning': return 'warning';
    case 'Error': return 'danger';
    default: return 'gold';
  }
}

function metricTone(kind: ResourceMeter['kind'], percent: number): string {
  if (percent <= 33) return 'danger';
  if (percent <= 66) return 'warning';
  if (kind === 'health') return 'success';
  return 'gold';
}

function toSafeId(value: string): string {
  const normalized = value.trim().toLowerCase().replace(/[^a-zа-я0-9_-]+/gi, '-').replace(/^-+|-+$/g, '');
  return normalized || 'section';
}

function EntityGlyph({ icon }: { icon: string }) {
  const normalized = icon.trim().toLowerCase();
  const paths = iconPaths(normalized);

  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      {paths.map((path, index) => <path d={path} key={index} />)}
    </svg>
  );
}

function iconPaths(normalized: string): string[] {
  if (normalized.includes('chevron')) return ['M6 9l6 6 6-6'];
  if (normalized.includes('close')) return ['M18 6 6 18', 'M6 6l12 12'];
  if (normalized.includes('open')) return ['M7 7h10v10', 'M9 15 17 7', 'M11 7h6v6'];
  if (normalized.includes('npc') || normalized.includes('person')) return ['M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4Z', 'M4 21a8 8 0 0 1 16 0'];
  if (normalized.includes('relation') || normalized.includes('faction')) return ['M8 12h8', 'M12 8v8', 'M5 5c3-2 5-2 7 1 2-3 4-3 7-1 2 2 2 6 0 8l-7 7-7-7c-2-2-2-6 0-8Z'];
  if (normalized.includes('quest')) return ['M6 4h9l3 3v13H6Z', 'M9 9h6', 'M9 13h6', 'M9 17h3'];
  if (normalized.includes('secret')) return ['M4 12s3-6 8-6 8 6 8 6-3 6-8 6-8-6-8-6Z', 'M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z'];
  if (normalized.includes('state') || normalized.includes('effect') || normalized.includes('status')) return ['M12 3v18', 'M5 8h14', 'M7 16h10'];
  if (normalized.includes('skill')) return ['M4 20 14 4l6 6L10 20Z', 'M13 5l6 6'];
  if (normalized.includes('inventory') || normalized.includes('item')) return ['M6 7h12l1 13H5Z', 'M9 7a3 3 0 0 1 6 0'];
  if (normalized.includes('memory') || normalized.includes('archive') || normalized.includes('book')) return ['M7 5h10a3 3 0 0 1 3 3v8a3 3 0 0 1-3 3H7a3 3 0 0 1-3-3V8a3 3 0 0 1 3-3Z', 'M8 9h8', 'M8 13h5'];
  if (normalized.includes('link')) return ['M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71', 'M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71'];
  return ['M12 3 3 8l9 5 9-5Z', 'M3 8v8l9 5 9-5V8'];
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

export function BlockList({
  blocks,
  advancedEnabled = false,
  onAction,
  availableActions = []
}: {
  blocks: UiBlock[];
  advancedEnabled?: boolean;
  onAction?: (action: UiAction) => void;
  availableActions?: UiAction[];
}) {
  return (
    <div className="block-list-container">
      {blocks.map((block, i) => (
        <BlockRenderer
          key={`${block.kind}-${i}`}
          block={block}
          advancedEnabled={advancedEnabled}
          onAction={onAction}
          availableActions={availableActions}
        />
      ))}
    </div>
  );
}
