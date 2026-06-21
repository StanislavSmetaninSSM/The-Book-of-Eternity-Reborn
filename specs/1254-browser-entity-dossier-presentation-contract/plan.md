# Implementation Plan: Browser Entity Dossier Presentation Contract

Source issue: <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1254>

Canonical format document:
[`docs/web-ui/browser-entity-dossier-format.md`](../../docs/web-ui/browser-entity-dossier-format.md)

Accepted prototype to inspect before implementation:
`C:\Users\Ёж\Downloads\PROTOTYPE`

Codex working copy of the prototype:
`E:\Games\boe-other-agent-prototype-view`

## Technical Direction

Use the canonical format document as the UX and acceptance contract for issues
#1254-#1262. Inspect the accepted prototype before changing renderer behavior;
use it for card hierarchy, collapsible sections, collection browser behavior,
spacing, and visual rhythm.

Implementation should proceed in this order:

1. Backend semantic UI block contract.
2. Frontend semantic dossier/card renderer.
3. Localization and semantic formatting layer.
4. NPC migration.
5. Mortal World entity migration.
6. Afterlife entity migration.
7. Media preview/lightbox support.
8. Anti-pattern audit tests.

## Renderer Requirements

- Render complex entities through cards and nested cards, not tables.
- Use a reusable collection-browser component for large collections.
- Keep media handling reusable across entity cards and nested cards.
- Keep Russian-first labels and avoid raw internal values.
- Keep browser output semantically equivalent to console output where both expose
  the same player information.

## Verification Direction

Every implementation task should include:

- Unit/component tests for semantic rendering decisions where feasible.
- Browser visual checks for at least one dense entity and one large collection.
- Source/audit tests for banned patterns: tables for complex entities,
  semicolon-joined structured data, raw JSON/debug fields, and untranslated
  stable keys.

## Risks

- Generic backend blocks may not contain enough semantic data for good frontend
  rendering. Prefer adding typed semantic blocks over complex React guessing.
- Sticky navigation can make content too narrow. Hide/collapse it at breakpoints
  where cards become cramped.
- Large collection browsers need explicit category/tag data; deriving categories
  from arbitrary text is only an interim fallback.
