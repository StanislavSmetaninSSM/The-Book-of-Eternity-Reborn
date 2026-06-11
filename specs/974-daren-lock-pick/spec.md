# Feature Specification: Daren Scene 06 Full Literary Page

**Feature Branch**: `work/974-daren-lock-pick`
**Created**: 2026-06-12
**Status**: Draft for autonomous implementation
**Tracked issue and related context**: [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), already-completed prior scene tasks [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971), [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973)

## Source Issue & Scope

- **Source GitHub issue**: #974 — rewrite scene `lock_pick` / “Замок кабинета” as a full Russian dark-fantasy literary page.
- **Parent**: #955 — Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #974 is player-facing story/UX content over shared console/browser QTE route data. It changes default player copy, requires console/browser parity through shared C# data, and must preserve route mechanics, so a focused Spec Kit feature is required.
- **In scope**: one substantial Russian prose page for `lock_pick`, focused objective guards that fail on synopsis-length copy for this scene, and local verification evidence.
- **Out of scope**: rewriting scenes #975-#983, changing already-merged #969-#973 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, or adding browser-only/console-only story forks.

## Current Main Text

> У двери кабинета Дарен слышит, как старый замок отвечает отмычке сухими штифтами. Ему нужно открыть проход без царапин и шума, потому что любой след на накладке приведёт стражу к посоху.

## User Story

As a player reading Daren's QTE showcase, I want the “Замок кабинета” beat to feel like a tense tactile burglary scene, so the player experiences Daren's breath, hands, lock-picking craft, the old cabinet-door mechanism, the danger of scratches/noise, and the narrowing moment before the existing `LockPinSet` action appears.

## Acceptance Criteria

1. Scene `lock_pick` is a substantial Russian prose page, not a one/two-sentence synopsis.
2. Daren remains the active point-of-view protagonist, moving, listening, judging, and acting under pressure.
3. The cabinet door / old lock / keyhole / plate / pins / picks or similar concrete lock details are present as a tactile scene, not just as labels.
4. The scene includes pressure from stealth and evidence stakes: noise, scratches, witnesses/guards, or the risk that the path to the staff becomes traceable.
5. The prose naturally leads into the existing lock-picking `LockPinSet` QTE action and does not introduce a new dialogue runtime or social branch for this non-social beat.
6. Default player-facing prose contains no implementation terminology such as `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE`.
7. Existing route order, beat id, action id, check type/config, routing, score deltas, reward behavior, profile writes, New Game grants, endpoints, runtime state, and frontend/backend boundaries remain unchanged.
8. Console and browser continue to consume the same shared C# route data.

## Required Tests / Evidence

- A focused guard in `DarenQteShowcaseTests` fails on the current synopsis and passes only after the scene becomes a substantial tactile lock-picking page with grouped motif coverage and mechanic invariants.
- Focused Daren test filter passes locally.
- Affected Daren/QTE/docs/browser C# slice passes locally.
- Client and test-project builds pass locally.
- Spec Kit prerequisite helper resolves this feature directory.
- `git diff --check origin/main...HEAD` passes.
- Added-line static scan for production code reports `NO_MATCHES`.
