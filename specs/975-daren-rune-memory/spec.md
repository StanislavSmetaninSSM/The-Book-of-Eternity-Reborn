# Feature Specification: Daren Scene 07 Full Literary Page

**Feature Branch**: `work/975-daren-rune-memory`
**Created**: 2026-06-12
**Status**: Implemented locally; Hermes review/PR/merge/closure pending
**Tracked issue and related context**: [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), already-completed prior scene tasks [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971), [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974)

## Source Issue & Scope

- **Source GitHub issue**: #975 — rewrite scene `rune_memory` / “Руны на дверце” as a full Russian dark-fantasy literary page.
- **Parent**: #955 — Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #975 is player-facing story/UX content over shared console/browser QTE route data. It changes default player copy, requires console/browser parity through shared C# data, and must preserve route mechanics, so a focused Spec Kit feature is required.
- **In scope**: one substantial Russian prose page for `rune_memory`, focused objective guards that fail on synopsis-length copy for this scene, and local verification evidence.
- **Out of scope**: rewriting scenes #976-#983, changing already-merged #969-#974 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, or adding browser-only/console-only story forks.

## Current Main Text

> Дарен склоняется к дверце футляра, и синие руны вспыхивают на стекле так быстро, будто дом смотрит ему в глаза. Он должен запомнить узор и повторить его без ошибки, иначе защитный сигнал проснётся раньше кражи.

## User Story

As a player reading Daren's QTE showcase, I want the “Руны на дверце” beat to feel like a tense magical-security scene, so the player experiences Daren's observation, memory, breath/body control, the cold blue rune pattern on the case door/glass, the watching-house pressure, and the narrowing moment before the existing `PatternMemory` action appears.

## Acceptance Criteria

1. Scene `rune_memory` is a substantial Russian prose page, not a one/two-sentence synopsis.
2. Daren remains the active point-of-view protagonist, moving, observing, memorizing, and acting under pressure.
3. The case door / glass / blue runes / magical lock or similar concrete magical-security details are present as an embodied scene, not just as labels.
4. The scene includes pressure from memory and surveillance stakes: the house looking back, pattern drift, waking alarm/ward, guards/witnesses, or a traceable magical response before the theft.
5. The prose naturally leads into the existing rune-pattern `PatternMemory` QTE action and does not introduce a new dialogue runtime or social branch for this magical-security beat.
6. Default player-facing prose contains no implementation terminology such as `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE`.
7. Existing route order, beat id, action id, check type/config, routing, score deltas, reward behavior, profile writes, New Game grants, endpoints, runtime state, and frontend/backend boundaries remain unchanged.
8. Console and browser continue to consume the same shared C# route data.

## Required Tests / Evidence

- A focused guard in `DarenQteShowcaseTests` fails on the current synopsis and passes only after the scene becomes a substantial magical rune-memory page with grouped motif coverage and mechanic invariants.
- Focused Daren test filter passes locally.
- Affected Daren/QTE/docs/browser C# slice passes locally.
- Client and test-project builds pass locally.
- Spec Kit prerequisite helper resolves this feature directory.
- `git diff --check origin/main...HEAD` passes.
- Added-line static scan for production code reports `NO_MATCHES`.

## Local Implementation Evidence

- TDD RED: focused Daren run after adding `DarenRuneMemory_ReadsAsRuneWardMemoryPageWithoutMechanicDrift` failed on the expected compact-synopsis assertion: 47 passed / 1 failed / 0 skipped / 48 total.
- TDD GREEN: focused Daren run after replacing the shared `rune_memory` prose passed: 48 passed / 0 failed / 0 skipped / 48 total.
- Affected Daren/QTE/docs/browser C# slice passed: 317 passed / 0 failed / 0 skipped / 317 total.
- Client and test project builds succeeded with 0 warnings / 0 errors.
- Added production-line static scan for forbidden default-player technical terms returned `NO_MATCHES`.
- Scope check: no frontend/browser/runtime/reward/profile/New Game files changed, and parent #955 plus #976-#983 remain out of scope for Hermes lifecycle handling.
