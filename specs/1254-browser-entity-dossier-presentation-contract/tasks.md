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

- [x] #1255 Define semantic entity UI block contract for dossiers/cards.
- [x] #1256 Render semantic entity dossiers with cards, nested cards, icons, and readable spacing.
- [x] #1257 Add localization and semantic formatting layer for entity fields and enum values.
- [x] #1258 Migrate NPC command output to entity dossier cards.
- [x] #1259 Migrate Mortal World entity commands to dossier cards.
- [x] #1260 Migrate afterlife entity command output to entity dossier cards.
- [x] #1261 Add command-surface audit tests for entity presentation anti-patterns.
- [x] #1262 Media preview and lightbox support for entity cards.

## Mandatory Review Checklist For Child Tasks

- [x] The implementation follows `docs/web-ui/browser-entity-dossier-format.md`.
- [x] The implementation was compared against the accepted prototype paths above.
- [x] Complex entity details are not rendered as normal tables.
- [x] Nested data is shown as nested cards.
- [x] Large collections follow the threshold rules and use collection browser at 21+ items.
- [x] Stable labels and enum-like values are localized to Russian.
- [x] Media previews open in a lightbox when media is present.
- [x] Browser checks show no text overlap and no horizontal overflow.

## Closure Evidence

- #1255-#1262 are implemented and closed in GitHub.
- #1253 is handled by the command-surface audit coverage from #1261 plus the
  dossier/card migrations from #1258-#1260.
- Focused regression suites to keep current before closing this epic:
  `BrowserCommandPresentationAuditTests`, `BrowserApiContractTests`, and
  frontend player-facing/block-renderer tests.
