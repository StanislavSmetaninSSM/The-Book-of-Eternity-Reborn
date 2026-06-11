# Feature Specification: Daren Scene 02 Full Literary Page

**Feature Branch**: `work/970-daren-mira-whisper`
**Created**: 2026-06-12
**Status**: Draft for autonomous implementation
**Source Issues**: [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), base scenario [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919), prior Daren handoff specs [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), [#959](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959), [#960](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/960), [#961](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/961), completed scene [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969)

## Source Issues & Scope

- **Source GitHub issue**: #970 — rewrite scene `informant_parley` / “Шёпот Миры” as a full Russian dark-fantasy literary page.
- **Parent**: #955 — Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #970 is player-facing story/UX content over shared console/browser QTE route data. It changes default player copy and must preserve console/browser parity and QTE mechanics, so a focused Spec Kit feature is required.
- **In scope**: one substantial Russian prose page for `informant_parley`, focused objective guards that fail on synopsis-length copy for this scene, and local verification evidence.
- **Out of scope**: rewriting scenes #971-#983, changing already-merged #969 prose except for a test helper if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a dialogue system, or adding browser-only/console-only story forks.

## User Scenarios & Testing

### Scenario 1 - Mira's meeting reads like a scene, not an informant briefing (Priority: P1)

A player reaches the Daren informant beat and reads an actual social scene: Daren slips to the rear-road awning, recognizes Mira as an old contact with charged history, notices her wet ribbon/body language, trades tense dialogue, and earns or risks her trust before the password/PrecisionChoice action.

**Independent Test**: Add or update focused tests in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` so `informant_parley` fails when its chapter narrative is still only a short summary or lacks Mira/Daren interaction and dialogue.

**Acceptance Scenarios**:
1. **Given** the Daren route reaches `informant_parley`, **When** the chapter narrative is inspected, **Then** it has substantial multi-paragraph Russian prose with Daren as active protagonist and Mira as a present character, not a compact objective summary.
2. **Given** this is a social/cast beat, **When** the player reads the prose, **Then** Daren and Mira have voiced interaction/dialogue or immediately visible social pressure that naturally leads to the existing answer choice.
3. **Given** the scene uses the user-provided Mira example as the quality bar, **When** the final text is reviewed, **Then** it matches the example's level of scene construction without copying the example verbatim or leaking technical terminology.

---

### Scenario 2 - Shared route authority and route mechanics remain unchanged (Priority: P1)

Both clients consume the same C# route data; the task changes only the second scene's authored player copy and narrowly scoped tests.

**Independent Test**: Existing Daren/QTE contract tests continue to pass. Add focused checks that compare pre-existing route mechanics and assert `informant_parley` keeps the same action label, action id, check type, and choice ids.

**Acceptance Scenarios**:
1. **Given** the route definition is loaded, **When** beat ids and action metadata are inspected, **Then** `informant_parley` remains in the same order and no Daren beat is added, removed, or reordered.
2. **Given** the browser Daren showcase uses QTE DTOs, **When** it receives the Mira scene, **Then** it receives the shared C# prose rather than a React-only text fork.
3. **Given** reward and ending behavior from #919/#960, **When** this issue is implemented, **Then** no reward/profile/New Game marker behavior changes.

---

### Scenario 3 - Parent #955 remains open until all per-scene children close (Priority: P2)

Closing #970 only finishes scene 02. It must not imply the whole Daren interactive-book umbrella is complete.

**Independent Test**: PR/issue evidence should state that #971-#983 and parent #955 remain separate follow-ups.

## Functional Requirements

- **FR-001**: `informant_parley` chapter narrative MUST be rewritten as a substantial Russian dark-fantasy literary scene page with Daren as protagonist.
- **FR-002**: The scene MUST include Mira as an active present NPC/contact, her relationship/subtext with Daren, body language or visible tension, and dialogue or voiced exchange appropriate to an informant parley.
- **FR-003**: The scene MUST include setting/atmosphere near the rear-road awning, Daren's movement/observation/intent, Mira's wet ribbon or equivalent identifying detail from the issue, the risk of guards/pursuit/source exposure, and stakes leading naturally into the existing precision choice.
- **FR-004**: The scene MUST be player-facing and in-world; default prose MUST NOT expose technical/agent/debug/API/DTO/GM/Spec Kit/QTE terminology.
- **FR-005**: The scene MUST stay in shared C# Daren route data consumed by console and browser; no React-only or console-only prose fork is allowed.
- **FR-006**: Implementation MUST preserve `informant_parley` beat id/title/order, action id/check type, existing choice ids/outcomes, routing, score deltas, Daren route id, ending tiers, reward profile, New Game grant, endpoints, and runtime state.
- **FR-007**: Tests/source guards MUST fail on the current synopsis-length `informant_parley` text before production prose is changed.
- **FR-008**: Verification MUST include focused `DarenQteShowcaseTests`, the affected Daren/QTE/docs/browser slice, and `git diff --check`.
- **FR-009**: The final evidence MUST treat #970 as a child scene task and must not close parent #955.

## Non-Functional Requirements

- **NFR-001**: Prose should match Stanislav's target form: a full dark-fantasy novella page with action, atmosphere, subtext/body language, tense dialogue, and a natural lead-in to the QTE.
- **NFR-002**: Tests should use objective structural proxies for this scene (length/sentence count/Daren/Mira/dialogue presence/required motifs/technical-term absence), while independent human/Codex review checks literary quality.
- **NFR-003**: The change should stay small and reviewable: ideally `QteSceneService.Daren.cs`, `DarenQteShowcaseTests.cs`, and Spec Kit artifacts only.

## Success Criteria

- `informant_parley` no longer reads as a one/two-sentence synopsis; it reads as a substantial Mira meeting scene page.
- Focused RED/GREEN evidence shows the new guard fails before prose replacement and passes after implementation.
- Focused and affected local tests pass with exact counts recorded.
- Independent review approves both scope/quality and no QTE mechanic drift.
- PR merges to `main`, #970 closes as completed, and #955 remains open for the remaining child scenes.
