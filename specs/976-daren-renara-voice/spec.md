# Feature Specification: Daren Scene 08 Full Literary Page

**Feature Branch**: `work/976-daren-renara-voice`
**Created**: 2026-06-12
**Status**: Implemented locally; pending Hermes review/PR/merge/closure
**Tracked issue and related context**: [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), already-completed prior scene tasks [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971), [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974), [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975)

## Source Issue & Scope

- **Source GitHub issue**: #976 — rewrite scene `ward_steward_parley` / “Голос Ренары” as a full Russian dark-fantasy literary page.
- **Parent**: #955 — Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #976 is player-facing story/UX content over shared console/browser QTE route data. It changes default player copy, involves a named magical-security NPC/social pressure point, requires console/browser parity through shared C# data, and must preserve route mechanics, so a focused Spec Kit feature is required.
- **In scope**: one substantial Russian prose page for `ward_steward_parley`, focused objective guards that fail on synopsis-length copy for this scene, and local verification evidence.
- **Out of scope**: rewriting scenes #977-#983, changing already-merged #969-#975 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, or adding browser-only/console-only story forks.

## Current Main Text

> У погасших рун к Дарену обращается Ренара Вардовая, управляющая печатями дома, хотя её лицо остаётся только в холодном стекле футляра. Она спрашивает, зачем чужая рука тронула посох; ответ должен усыпить дом, а не дать сигналу имя вора.

## User Story

As a player reading Daren's QTE showcase, I want the “Голос Ренары” beat to feel like a tense magical-security dialogue page, so the player experiences Daren under the gaze of Renara Wardova, sees her voice/body/ward presence in the glass and house seals, and understands why his answer must misdirect the house without changing the existing `PrecisionChoice` action.

## Acceptance Criteria

1. Scene `ward_steward_parley` is a substantial Russian prose page, not a one/two-sentence synopsis.
2. Daren remains the active point-of-view protagonist, observing, thinking, choosing words, and answering under pressure.
3. Renara Wardova is present as a concrete named magical-security authority through voice, reflection, ward/seal presence, body language substitute, or visible social pressure; she is not merely named.
4. The scene includes real interaction/dialogue or visible conversational pressure between Renara and Daren that naturally leads into the existing `PrecisionChoice` answer.
5. The prose carries consequences from the previous rune-memory beat and sets up the next physical-pressure beat: house seals, glass, extinguished runes, signal/trace risk, the staff, the niche/case, or the ward deciding whether to stay quiet.
6. Default player-facing prose contains no implementation terminology such as `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE`.
7. Existing route order, beat id, action id, check type/config, routing, score deltas, reward behavior, profile writes, New Game grants, endpoints, runtime state, and frontend/backend boundaries remain unchanged.
8. Console and browser continue to consume the same shared C# route data.

## Required Tests / Evidence

- A focused guard in `DarenQteShowcaseTests` fails on the current synopsis and passes only after the scene becomes a substantial Renara/ward dialogue page with grouped motif coverage and mechanic invariants.
- Focused Daren test filter passes locally.
- Affected Daren/QTE/docs/browser C# slice passes locally.
- Client and test-project builds pass locally.
- Spec Kit prerequisite helper resolves this feature directory.
- `git diff --check origin/main...HEAD` passes.
- Added-line static scan for production code reports `NO_MATCHES`.

## Local Implementation Evidence

- TDD RED: focused Daren test filter failed as expected after adding the #976 guard: 48 passed / 1 failed / 0 skipped / 49 total; failure reason was the compact `ward_steward_parley` synopsis not meeting substantial Renara ward-dialogue page length.
- GREEN focused Daren filter: 49 passed / 0 failed / 0 skipped / 49 total.
- Affected Daren/QTE/docs/browser C# slice: 318 passed / 0 failed / 0 skipped / 318 total.
- Client build and test-project build both completed with 0 warnings / 0 errors.
- Working-tree `git diff --check` had no whitespace errors; added-line production C# forbidden-term scan returned `NO_MATCHES`.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.
