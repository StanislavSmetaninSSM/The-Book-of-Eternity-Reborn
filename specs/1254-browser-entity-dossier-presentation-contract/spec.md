# Feature Specification: Browser Entity Dossier Presentation Contract

Source issue: <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1254>

Canonical format document:
[`docs/web-ui/browser-entity-dossier-format.md`](../../docs/web-ui/browser-entity-dossier-format.md)

## User Need

Players need browser command outputs for complex game entities to be readable,
localized, and navigable. Current and future browser implementations must not
degrade rich game data into tables, raw fields, flattened strings, or long
unbounded lists.

## Required Format

All tasks in the #1254-#1262 issue set MUST follow the canonical dossier format.
The format includes:

- Entity dossier header with title, role/subtitle, summary, badges, and media.
- Fact groups, metric cards, gameplay hints, and meaningful sections.
- Nested cards for all nested objects.
- Collapsible top-level sections open by default.
- Collapsible dense nested cards when needed.
- Localized labels and enum-like values.
- SVG/icon registry usage instead of emoji-first UI.
- Media preview and lightbox support.
- Large collection handling with thresholds:
  - 0: empty state
  - 1-8: direct cards
  - 9-20: grouped compact/collapsible view
  - 21+: collection browser with overview, featured items, search, filters,
    compact list, and detail pane

## In Scope

- Browser renderer contract for complex entity outputs.
- Backend semantic block contract needed to support the renderer.
- Migration guidance for NPCs, Mortal World entities, afterlife entities,
  media, localization, and anti-pattern tests.
- Task handoff requirements for agents working on #1254-#1262.

## Out Of Scope

- Final implementation of every migrated command in this specification update.
- Redesign of unrelated browser screens that do not render complex command
  entities.
- Console client presentation changes, except where browser/console semantic
  parity is explicitly required by a child issue.

## Acceptance Criteria

- The canonical format document exists and is referenced by all #1254-#1262
  tasks.
- Large collections are explicitly covered and must not render as long full-card
  lists.
- Related implementations can be reviewed against the acceptance checks in the
  format document.
- Any future deviation requires a tracked GitHub issue and an explicit update to
  this spec or the canonical format document.
