# Research: Browser Command Result UX Audit

## Browser Act Findings

- `/нпс`: backend returns many detailed blocks, but the browser first viewport is dominated by summary/count tables. The data exists, yet the user sees counts before useful NPC reading/navigation.
- `/инв`: browser shows money, equipment, and item table, but no readable route to item descriptions, bonuses, structured bonuses, combat effects, document contents, or other console-style item details.
- `/статус`: browser output is dry key/value text with internal/English labels such as `Realm` and `Month of Beginnings`, no visual bars, and weak effect/detail presentation.
- `/фракции`: overview exposes detail actions, but a faction detail renders a generic semicolon list such as `detail: true; detail: ...; detail: #c79a3b; level: 3; detail: 220`, including implementation fields and unlabeled numbers.
- `/craft`: live Browser Act verification found that default local-turn output still used protocol copy (`Браузерная команда`, `pending`, `interactive/write`) even after raw JSON blocks were hidden.
- `/gacha`: default blocked output in the wrong realm contained `currentRealm` / `realm` contract wording, causing the generic default projection to hide useful player text until the source copy was rewritten.
- `/chaos_sea`: overview labels used `Pending ...` in a player card.

## Initial Root Cause

The most severe defect is not React layout; the backend projection sometimes emits generic reference-detail blocks instead of curated player-facing projections. React faithfully renders poor blocks. The first fix should therefore target `ExplorerMortalWorldCommandResultBuilder` and add tests in `ExplorerWebCommandServiceTests`.

## Decision

Fix command-result projections in small slices:

1. Faction detail projection: remove implementation leakage and render meaningful labels.
2. Inventory detail navigation and item projections.
3. NPC summary/detail navigation balance.
4. Status localization and richer browser presentation.

React renderer changes remain available for later if corrected blocks still cannot produce a good browser surface.

## Follow-Up Finding

The main scene lifecycle panel still contains the phrase `Браузерный запись хода`. This is outside the command-result DTO projection fixed here and should be tracked separately as browser scene/lifecycle copy polish.
