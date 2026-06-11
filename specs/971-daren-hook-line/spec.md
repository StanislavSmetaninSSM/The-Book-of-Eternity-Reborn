# Feature Specification: Daren Scene 03 Full Literary Page

**Feature Branch**: `work/971-daren-hook-line`
**Created**: 2026-06-12
**Status**: Draft for autonomous implementation
**Tracked issue and related context**: [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), already-completed prior scene tasks [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970)

## Source Issues & Scope

- **Source GitHub issue**: #971 — rewrite scene `gadget_infiltration` / “Крюк и леска” as a full Russian dark-fantasy literary page.
- **Parent**: #955 — Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #971 is player-facing story/UX content over shared console/browser QTE route data. It changes default player copy and must preserve console/browser parity and QTE mechanics, so a focused Spec Kit feature is required.
- **In scope**: one substantial Russian prose page for `gadget_infiltration`, focused objective guards that fail on synopsis-length copy for this scene, and local verification evidence.
- **Out of scope**: rewriting scenes #972-#983, changing already-merged #969/#970 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding new gadget mechanics, or adding browser-only/console-only story forks.

## User Scenarios & Testing

### Scenario 1 - The hook-and-line climb reads like a tense infiltration page (Priority: P1)

A player reaches `gadget_infiltration` and reads a full scene: after Mira's whisper, Daren reaches the tower wall, studies the balcony/courtyard, prepares the folding hook and line, feels the cold stone and wet rope/metal, times the throw against guard sounds, and starts the ascent under threat of a clatter or first shout.

**Independent Test**: Add or update focused tests in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` so `gadget_infiltration` fails when its chapter narrative is still only a short summary or lacks gadget/climb/courtyard tension.

**Acceptance Scenarios**:
1. **Given** the route reaches `gadget_infiltration`, **When** the narrative is inspected, **Then** it has substantial multi-paragraph Russian prose with Daren as active protagonist, not a compact objective summary.
2. **Given** the upcoming action is the folding hook `ChargeRelease` check, **When** the prose ends, **Then** the player understands the throw/anchor/ascent risk without changing action metadata.
3. **Given** the route has just passed Mira, **When** continuity is reviewed, **Then** the scene may reference the prior whisper/password pressure but stays focused on the hook-and-line infiltration.

## Functional Requirements

- **FR-001**: `gadget_infiltration` chapter narrative MUST be rewritten as a substantial Russian dark-fantasy literary scene page with Daren as protagonist.
- **FR-002**: The scene MUST include tower/cold stone/balcony/courtyard spatial pressure, the folding hook and line as tactile objects, Daren's bodily movement/preparation, guard/sound/light stakes, and a natural lead-in to the existing hook launch action.
- **FR-003**: The scene MUST be player-facing and in-world; default prose MUST NOT expose technical/agent/debug/API/DTO/GM/Spec Kit/QTE terminology.
- **FR-004**: The scene MUST stay in shared C# Daren route data consumed by console and browser; no React-only or console-only prose fork is allowed.
- **FR-005**: Implementation MUST preserve `gadget_infiltration` beat id/title/order, action id/check type/config, routing, score deltas, Daren route id, ending tiers, reward profile, New Game grant, endpoints, and runtime state.
- **FR-006**: Tests/source guards MUST fail on the current synopsis-length `gadget_infiltration` text before production prose is changed.
- **FR-007**: Verification MUST include focused `DarenQteShowcaseTests`, the affected Daren/QTE/docs/browser slice, and `git diff --check`.
- **FR-008**: The final evidence MUST treat #971 as a child scene task and must not close parent #955.

## Non-Functional Requirements

- **NFR-001**: Prose should match Stanislav's target form: a full dark-fantasy novella page with action, atmosphere, body language, sensory detail, tension, and a natural lead-in to the QTE.
- **NFR-002**: Tests should use objective structural proxies for this scene (length/sentence count/Daren/gadget/climb/stakes motifs/technical-term absence), while independent review checks literary quality.
- **NFR-003**: The change should stay small and reviewable: ideally `QteSceneService.Daren.cs`, `DarenQteShowcaseTests.cs`, and Spec Kit artifacts only.

## Success Criteria

- `gadget_infiltration` no longer reads as a one/two-sentence synopsis; it reads as a substantial hook-and-line infiltration page.
- Focused RED/GREEN evidence shows the new guard fails before prose replacement and passes after implementation.
- Focused and affected local tests pass with exact counts recorded.
- Independent review approves both scope/quality and no QTE mechanic drift.
- PR merges to `main`, #971 closes as completed, and #955 remains open for the remaining child scenes.
