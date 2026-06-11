# Feature Specification: Daren Scene 04 Full Literary Page

**Feature Branch**: `work/972-daren-stealth-crossing`
**Created**: 2026-06-12
**Status**: Draft for autonomous implementation
**Tracked issue and related context**: [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), already-completed prior scene tasks [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971)

## Source Issue & Scope

- **Source GitHub issue**: #972 — rewrite scene `stealth_crossing` / “Галерея без звука” as a full Russian dark-fantasy literary page.
- **Parent**: #955 — Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #972 is player-facing story/UX content over shared console/browser QTE route data. It changes default player copy and must preserve console/browser parity and QTE mechanics, so a focused Spec Kit feature is required.
- **In scope**: one substantial Russian prose page for `stealth_crossing`, focused objective guards that fail on synopsis-length copy for this scene, and local verification evidence.
- **Out of scope**: rewriting scenes #973-#983, changing already-merged #969-#971 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new stealth system, or adding browser-only/console-only story forks.

## Current Main Text

> Дарен входит в галерею, где портреты смотрят из пыли, а сонный страж дышит за тонкой полосой света. Каждый шаг должен утонуть в тишине: один лишний шум разбудит фонарь, а чистый проход не оставит стражам нитки к кабинету.

## User Story

As a player reading Daren's QTE showcase, I want the “Галерея без звука” beat to feel like a complete stealth scene inside a dark-fantasy novella, so the timed/noise QTE grows out of Daren's movement, breath, body control, and the danger of waking the gallery guard rather than appearing as a short mechanical briefing.

## Acceptance Criteria

1. Scene `stealth_crossing` is a substantial Russian prose page, not a one/two-sentence synopsis.
2. Daren remains the active point-of-view protagonist; the text follows his movement through the gallery.
3. The scene includes gallery atmosphere: portraits, dust, a narrow strip of light, sleeping/breathing guard presence, floor/wood/stone sounds, and the sense that one small noise will expose him.
4. The prose naturally leads into the existing stealth/noise QTE action and does not introduce a new mechanic.
5. Default player-facing prose contains no implementation terminology such as `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE`.
6. Existing route order, beat id, action id, check type/config, routing, score deltas, reward behavior, profile writes, New Game grants, endpoints, runtime state, and frontend/backend boundaries remain unchanged.
7. Console and browser continue to consume the same shared C# route data.

## Required Tests / Evidence

- A focused guard in `DarenQteShowcaseTests` fails on the current synopsis and passes only after the scene becomes a substantial page with grouped motif coverage and mechanic invariants.
- Focused Daren test filter passes locally.
- Affected Daren/QTE/docs/browser C# slice passes locally.
- Client and test-project builds pass locally.
- Spec Kit prerequisite helper resolves this feature directory.
- `git diff --check origin/main...HEAD` passes.
- Added-line static scan for production code reports `NO_MATCHES`.
