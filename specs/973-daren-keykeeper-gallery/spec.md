# Feature Specification: Daren Scene 05 Full Literary Page

**Feature Branch**: `work/973-daren-keykeeper-gallery`
**Created**: 2026-06-12
**Status**: Draft for autonomous implementation
**Tracked issue and related context**: [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), already-completed prior scene tasks [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971), [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972)

## Source Issue & Scope

- **Source GitHub issue**: #973 — rewrite scene `guard_interrogation` / “Ключник в галерее” as a full Russian dark-fantasy literary page.
- **Parent**: #955 — Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #973 is player-facing story/UX content over shared console/browser QTE route data. It changes default player copy and includes a named NPC/social pressure point, so a focused Spec Kit feature is required.
- **In scope**: one substantial Russian prose page for `guard_interrogation`, named NPC interaction with Лукьян Седой Ключник, focused objective guards that fail on synopsis-length copy for this scene, and local verification evidence.
- **Out of scope**: rewriting scenes #974-#983, changing already-merged #969-#972 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, or adding browser-only/console-only story forks.

## Current Main Text

> У служебной двери Дарена останавливает Лукьян Седой Ключник, старый страж с фонарём ниже глаз и связкой дверных колец на руке. Вопрос звучит тихо, но подозрение уже стоит между ними: ответ решит, станет ли Лукьян свидетелем или случайной тенью.

## User Story

As a player reading Daren's QTE showcase, I want the “Ключник в галерее” beat to feel like a full tense encounter with a person, so the player experiences Lукьян's body language, suspicion, keys/lantern, Daren's social improvisation, and the stakes of being seen before the choice/action appears.

## Acceptance Criteria

1. Scene `guard_interrogation` is a substantial Russian prose page, not a one/two-sentence synopsis.
2. Daren remains the active point-of-view protagonist, observing and responding under pressure.
3. Lукьян Седой Ключник is personified through age, posture, lantern/keys, voice, suspicion, or similar concrete details.
4. The scene includes real dialogue or visible social-pressure exchange between Daren and Lукьян.
5. The prose naturally leads into the existing social/choice QTE action and does not introduce a new dialogue runtime.
6. Default player-facing prose contains no implementation terminology such as `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE`.
7. Existing route order, beat id, action id, check type/config, routing, score deltas, reward behavior, profile writes, New Game grants, endpoints, runtime state, and frontend/backend boundaries remain unchanged.
8. Console and browser continue to consume the same shared C# route data.

## Required Tests / Evidence

- A focused guard in `DarenQteShowcaseTests` fails on the current synopsis and passes only after the scene becomes a substantial social-pressure page with grouped motif coverage and mechanic invariants.
- Focused Daren test filter passes locally.
- Affected Daren/QTE/docs/browser C# slice passes locally.
- Client and test-project builds pass locally.
- Spec Kit prerequisite helper resolves this feature directory.
- `git diff --check origin/main...HEAD` passes.
- Added-line static scan for production code reports `NO_MATCHES`.
