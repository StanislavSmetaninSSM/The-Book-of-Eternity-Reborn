# Feature Specification: Daren Scene 01 Full Literary Page

**Feature Branch**: `work/969-daren-approach-manor`
**Created**: 2026-06-12
**Status**: Draft for autonomous implementation
**Source Issues**: [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), base scenario [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919), prior Daren handoff specs [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), [#959](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959), [#960](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/960), [#961](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/961)

## Source Issues & Scope

- **Source GitHub issue**: #969 — rewrite scene `approach_manor` / “Подступ к поместью” as a full Russian dark-fantasy literary page.
- **Parent**: #955 — Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #969 is player-facing story/UX content over shared console/browser QTE route data. It changes default player copy and must preserve console/browser parity and QTE mechanics, so a focused Spec Kit feature is required.
- **In scope**: one substantial Russian prose page for `approach_manor`, focused objective guards that fail on synopsis-length copy for that scene, and local verification evidence.
- **Out of scope**: rewriting scenes #970-#983, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a dialogue system, or adding browser-only/console-only story forks.

## User Scenarios & Testing

### Scenario 1 - The first Daren scene reads like a page, not a synopsis (Priority: P1)

A player starts the Daren showcase and the first scene gives a full scene-page: Daren in wet grass outside the manor, patrol light, the wall/old linden/gate choice, bodily tension, atmosphere, stakes, and a natural lead-in to the first QTE choice.

**Independent Test**: Add or update focused tests in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` so `approach_manor` fails when its chapter narrative is still only one/two short summary sentences or lacks scene-level craft.

**Acceptance Scenarios**:
1. **Given** the Daren route starts at `approach_manor`, **When** the chapter narrative is inspected, **Then** it has substantial multi-sentence prose, not a compact objective summary.
2. **Given** the scene is rendered to console or browser through the shared QTE route, **When** the player reads it, **Then** it remains in-world Russian prose and does not expose `GM`, `DTO`, `API`, `debug`, `Spec Kit`, score/debug framing, or implementation terminology.
3. **Given** the upcoming action is still the approach choice, **When** the prose ends, **Then** the QTE goal is clear without changing the existing action id, check type, routing, or score deltas.

---

### Scenario 2 - Shared route authority and route mechanics remain unchanged (Priority: P1)

Both clients consume the same C# route data; the task changes only the first scene's authored player copy and narrowly scoped tests.

**Independent Test**: Existing Daren/QTE contract tests continue to pass. Add focused checks that compare pre-existing route mechanics and assert `approach_manor` remains the start chapter with the same check type and route order.

**Acceptance Scenarios**:
1. **Given** the route definition is loaded, **When** beat ids and action metadata are inspected, **Then** `approach_manor` remains the start chapter and no Daren beat is added, removed, or reordered.
2. **Given** the browser Daren showcase uses QTE DTOs, **When** it receives the first scene, **Then** it receives the shared C# prose rather than a React-only text fork.
3. **Given** reward and ending behavior from #919/#960, **When** this issue is implemented, **Then** no reward/profile/New Game marker behavior changes.

---

### Scenario 3 - Parent #955 remains open until all per-scene children close (Priority: P2)

Closing #969 only finishes scene 01. It must not imply the whole Daren interactive-book umbrella is complete.

**Independent Test**: PR/issue evidence should state that #970-#983 and parent #955 remain separate follow-ups.

**Acceptance Scenarios**:
1. **Given** #969 is ready to merge, **When** the PR body and issue comment are prepared, **Then** they explicitly say parent #955 is not closed by this task alone.
2. **Given** the Spec Kit tasks include lifecycle work, **When** Codex finishes implementation, **Then** Hermes still owns independent review, PR merge, issue closure, and parent follow-up handling.

## Functional Requirements

- **FR-001**: `approach_manor` chapter narrative MUST be rewritten as a substantial Russian dark-fantasy literary scene page with Daren as protagonist.
- **FR-002**: The scene MUST include atmosphere/setting, Daren's movement or bodily state, observation/intent, patrol/lantern/staff-wall pressure, and stakes that lead naturally into the approach choice.
- **FR-003**: The scene MUST be player-facing and in-world; default prose MUST NOT expose technical/agent/debug/API/DTO/GM/Spec Kit terminology.
- **FR-004**: The scene MUST stay in shared C# Daren route data consumed by console and browser; no React-only or console-only prose fork is allowed.
- **FR-005**: Implementation MUST preserve `approach_manor` beat id, title, start chapter position, action id/check type, routing, score deltas, Daren route id, ending tiers, reward profile, New Game grant, endpoints, and runtime state.
- **FR-006**: Tests/source guards MUST fail on the current synopsis-length `approach_manor` text before production prose is changed.
- **FR-007**: Verification MUST include focused `DarenQteShowcaseTests`, the affected Daren/QTE/docs/browser slice, and `git diff --check`.
- **FR-008**: The final evidence MUST treat #969 as a child scene task and must not close parent #955.

## Non-Functional Requirements

- **NFR-001**: Prose should match Stanislav's target form: a full dark-fantasy novella page with action, atmosphere, subtext/body language where appropriate, and a natural lead-in to the QTE.
- **NFR-002**: Tests should use objective structural proxies for this scene (length/sentence count/Daren presence/required motifs/technical-term absence), while independent human/Codex review checks literary quality.
- **NFR-003**: The change should stay small and reviewable: ideally `QteSceneService.Daren.cs`, `DarenQteShowcaseTests.cs`, and Spec Kit artifacts only.

## Success Criteria

- `approach_manor` no longer reads as a one/two-sentence synopsis; it reads as a substantial scene page.
- Focused RED/GREEN evidence shows the new guard fails before prose replacement and passes after implementation.
- Focused and affected local tests pass with exact counts recorded.
- Independent review approves both scope/quality and no QTE mechanic drift.
- PR merges to `main`, #969 closes as completed, and #955 remains open for the remaining child scenes.
