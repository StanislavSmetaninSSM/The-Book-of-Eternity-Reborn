# Browser Entity Dossier Format

Source epic: <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1254>

This document is the canonical browser UI format for complex player-facing game
entities. Browser command output work under issues #1254-#1262 MUST follow this
format exactly unless a newer tracked issue explicitly changes the contract.

The accepted prototype is the reference for the visual direction and interaction
model. The contract below is authoritative for implementation.

## Reference Prototype

Before implementing tasks under #1254-#1262, open and inspect the accepted
prototype. It shows the intended card hierarchy, nested cards, collapsible
sections, large collection browser, spacing, and visual treatment.

Local prototype locations used during review:

- Original reviewed prototype: `C:\Users\Ёж\Downloads\PROTOTYPE`
- Codex working copy: `E:\Games\boe-other-agent-prototype-view`

Reference screenshots generated during review:

- `C:\Users\Ёж\Downloads\PROTOTYPE\collection-browser-30-items.png`
- `E:\Games\boe-other-agent-prototype-view\collection-browser-30-items.png`
- `E:\Games\boe-other-agent-prototype-view\collection-browser-filtered-artifacts.png`
- `E:\Games\boe-other-agent-prototype-view\collection-browser-all-30.png`

The prototype is a visual and interaction reference. This document remains the
source of truth when the prototype and written contract differ.

## Goals

- Present complex game data as a readable in-world dossier, not as developer
  data.
- Preserve the richness already available in the console client while using
  browser-native layout: cards, nested cards, media previews, filters, and
  detail panes.
- Scale from small entity details to large collections without forcing the
  player to scroll through long undifferentiated lists.

## Required Dossier Structure

Every complex entity SHOULD render as an entity dossier:

- Header: title, subtitle/role, short player-facing summary, meaningful badges,
  and optional media preview.
- Fact groups: stable short facts such as type, state, slot, faction, relation,
  or access rule.
- Metrics: numeric values as visual meters or prominent numeric cards, not as
  dense key/value rows.
- Gameplay hints: only when useful for decisions. Hints MUST NOT restate obvious
  UI behavior.
- Sections: grouped by player meaning, for example `Состояние`, `Навыки`,
  `Инвентарь и документы`, `Отношения`, `Квесты`, `Новости`.
- Nested data: rendered as nested cards inside the relevant parent card.

Top-level dossier sections SHOULD be collapsible and open by default. Dense
nested cards SHOULD be collapsible when they contain several facts, lists,
metrics, or child cards. Do not collapse everything by default.

## Mandatory Rendering Rules

- Tables are forbidden for normal entity details. Use cards, fact groups,
  metrics, lists, badges, and nested cards.
- Do not flatten structured data into one long line.
- Do not join fields with `;`, comma-heavy technical strings, or raw JSON-like
  text.
- Do not expose raw DTO names, endpoint names, enum values, file paths, or
  internal keys unless an explicit advanced/debug mode is active.
- Stable labels, known keys, enum-like values, item slots, quality names,
  relation statuses, and mechanics terms MUST be localized to Russian.
- Dynamic game text MUST be escaped/sanitized before rendering.
- Layout MUST avoid overlapping text, clipped Russian labels, horizontal
  overflow, and nested cards escaping parent bounds.
- Use project-styled SVG glyphs/icons. Emoji are not the default solution.
- If an entity contains image/media data, show a reduced preview in a deliberate
  side/inline position. Clicking the preview MUST open a larger lightbox/photo
  view.

## Nested Object Rules

Nested objects are meaningful subentities, not text fragments. They MUST render
as nested cards with their own title, subtitle, facts, badges, lists, metrics, or
child cards.

Examples:

- Item structural bonuses: each bonus is a nested card with target, value type,
  value, condition, source, and gameplay meaning.
- NPC skills: each skill is a nested card with mastery, scaling attribute,
  practical use, risk, and related quests/relations when available.
- World news details: consequences, hooks, stakes, open questions, related
  people, and player-known facts are separate nested cards/lists.
- Quest details: stages, known blockers, rewards, related NPCs/factions, and
  next useful action are separate blocks.

## Large Collection Rules

Collections MUST NOT render as a single long list of full cards when the item
count is high.

Use these thresholds unless a newer tracked issue changes them:

- `0` items: show an in-world empty state.
- `1-8` items: render normal cards directly in the section.
- `9-20` items: render grouped compact cards and collapsible groups; use filters
  when categories are meaningful.
- `21+` items: render a collection browser.

The collection browser MUST include:

- Collection overview: item count and a short explanation of what the collection
  represents.
- Featured items: 3-5 important/recent/relevant items.
- Search: by title, description, type, visible labels, and meaningful gameplay
  terms.
- Filters: localized chips such as `Все`, `Документы`, `Инструменты`,
  `Квестовое`, `Артефакты`, `Повреждено`, `Новое`, `С изображением`, depending
  on available data.
- Compact list: scrollable inside the collection surface, not a page-length
  chain of full cards.
- Detail pane: selecting an item opens the full dossier/card details in a
  separate panel.
- No horizontal overflow. On narrow screens, the list and detail pane stack
  vertically.

The dossier section remains an overview; the collection browser owns browsing
large item sets. A 30-item inventory MUST stay readable without creating a
9000px page section.

## Navigation

Large dossiers SHOULD include a section navigation surface such as a sticky table
of contents when viewport width allows it. The navigation must not make the main
content too narrow. If it causes cards to become cramped, hide or collapse it at
that breakpoint.

## Entity Coverage

This format applies to at least:

- NPCs and NPC detail sections.
- Inventory items, documents, books, item bonuses, and structural bonuses.
- Skills, abilities, statuses, effects, and combat/spiritual mechanics.
- Quests, personal quests, rewards, stages, and blockers.
- Factions, relations, reputation, progress, and faction trackers.
- World news, world events, state flags, and progress records.
- Locations, maps, exits, media, and generated images.
- Afterlife entities: Chaos Sea, Shining Abode, guardians, residents, relics,
  soul data, spiritual conflict data, and archive data.

## Backend Contract Direction

The backend should not send generic `UiTableBlock`, generic key/value grids, or
flattened detail strings for complex entities and expect React to infer meaning.
Prefer semantic blocks that map directly to the dossier renderer:

- Entity dossier/card.
- Media preview.
- Fact group.
- Badge/status group.
- Metric group.
- Gameplay hint.
- Nested entity card.
- Collection group / collection browser data.

When semantic data is unavailable, the browser may still render a readable
fallback card, but fallback output MUST obey the same anti-flattening and
localization rules.

## Acceptance Checks For Every Related Task

Each implementation or migration task under this contract must verify:

- No player-facing tables for complex entity details.
- No semicolon-joined structured strings.
- No raw JSON, DTO fields, endpoint names, raw enum values, or untranslated keys.
- Nested data is represented as nested cards.
- Large collections use the threshold rules and collection browser pattern.
- Media previews open in lightbox when media is present.
- Russian labels fit without overlap at desktop and narrow widths.
- No horizontal overflow.
- Search/filter/select interactions work for collection browsers.
