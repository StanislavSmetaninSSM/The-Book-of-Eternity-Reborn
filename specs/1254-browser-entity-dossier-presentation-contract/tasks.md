# Tasks: Browser Entity Dossier Presentation Contract

Source issue: <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1254>

Canonical format document:
[`docs/web-ui/browser-entity-dossier-format.md`](../../docs/web-ui/browser-entity-dossier-format.md)

Accepted prototype reference:

- `C:\Users\Ёж\Downloads\PROTOTYPE`
- `E:\Games\boe-other-agent-prototype-view`

## Contract Tasks

- [x] Document the canonical entity dossier/card format.
- [x] Document mandatory large collection behavior and thresholds.
- [x] Link the canonical format to #1254-#1262 task issues.
- [x] Record the accepted prototype paths for implementation agents.

## Implementation Tasks Tracked By GitHub Issues

- [ ] #1255 Define semantic entity UI block contract for dossiers/cards.
- [ ] #1256 Render semantic entity dossiers with cards, nested cards, icons, and readable spacing.
- [ ] #1257 Add localization and semantic formatting layer for entity fields and enum values.
- [ ] #1258 Migrate NPC command output to entity dossier cards.
- [ ] #1259 Migrate Mortal World entity commands to dossier cards.
- [ ] #1260 Migrate afterlife entity command output to dossier cards.
- [ ] #1261 Add command-surface audit tests for entity presentation anti-patterns.
- [ ] #1262 Media preview and lightbox support for entity cards.

## Mandatory Review Checklist For Child Tasks

- [ ] The implementation follows `docs/web-ui/browser-entity-dossier-format.md`.
- [ ] The implementation was compared against the accepted prototype paths above.
- [ ] Complex entity details are not rendered as normal tables.
- [ ] Nested data is shown as nested cards.
- [ ] Large collections follow the threshold rules and use collection browser at 21+ items.
- [ ] Stable labels and enum-like values are localized to Russian.
- [ ] Media previews open in a lightbox when media is present.
- [ ] Browser checks show no text overlap and no horizontal overflow.
