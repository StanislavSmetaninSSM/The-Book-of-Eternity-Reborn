# Feature Specification: Daren Scene 12 Full Literary Page

**Feature Branch**: `work/980-daren-staff-theft`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#980](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/980), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#979](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/979), next scene [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981)

## Source Issues & Scope

- **Source GitHub issue**: #980 - rewrite scene `staff_theft` / "Кража посоха" as a full Russian dark-fantasy literary page.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #980 changes player-facing story/UX content over shared console/browser QTE route data. It is the next open per-scene Daren literary-page child after #979, must preserve console/browser parity through shared C# data, and must not drift route mechanics, rewards, endpoints, runtime state, or sibling scene scope.
- **Contract scope**: player-facing, console, browser, shared route data, C# source guard tests. No GM-facing prompt, docs, examples, validation, runtime-state, or frontend contract change is intended because this scene is client-owned authored showcase prose and does not add a GM-authored capability.
- **In scope**: one substantial Russian prose page for `staff_theft`, a focused objective guard that fails on synopsis-length copy, and local verification evidence.
- **Out of scope**: rewriting scenes #981-#983 or result/aftermath issues #988-#1014, changing already-merged #969-#979 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, inventing an unrelated NPC dialogue slot, or adding browser-only/console-only story forks.

## Current Main Text

> Дарен снимает посох с бархатных держателей, и вокруг него едва слышно качаются тонкие кольца с тревожным звоном. За спиной остаётся замок, чья царапина на накладке может привести стражу сюда, поэтому добычу нужно уложить на ремень без нового голоса.

## User Scenarios & Testing

### User Story 1 - Staff Theft Reads As a Page (Priority: P1)

As a player reading Daren's QTE showcase, I want the "Кража посоха" beat to feel like a tense dark-fantasy theft scene, so I experience Daren removing the staff from its velvet holders, controlling the ringing suspension hardware, absorbing the weight and balance of the stolen relic, and securing the loot without giving the house or pursuit a new sound.

**Why this priority**: This is the only user-visible value of #980 and continues the parent #955 goal of replacing synopsis beats with interactive-book prose.

**Independent Test**: The scene can be tested independently by reading `staff_theft` from the shared route data and verifying the authored prose and unchanged action contract.

**Acceptance Scenarios**:

1. **Given** the player reaches `staff_theft`, **When** the scene text is rendered by console or browser, **Then** the text is a substantial Russian literary page centered on Daren stealing and securing the staff rather than a one/two-sentence summary.
2. **Given** the previous route-choice/orangery and earlier lock/rune/grate pressure, **When** the scene begins, **Then** the prose naturally carries forward staff-case/relic burden, old lock/evidence risk, alarm/listening-house pressure, or pursuit danger into the theft action.
3. **Given** the existing staff-theft action beat, **When** route data is inspected, **Then** the beat id, title, action id, `BalanceMeter` check, characteristic, difficulty, routing, score deltas, and rewards remain unchanged.

### Edge Cases

- The scene may include the house, guards, or pursuit pressure as atmosphere, but must not create a new dialogue runtime or unrelated social encounter.
- The prose must remain player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE` terms in default narrative.
- The page must stay within the existing broad Daren narrative length guard while still being substantial enough to reject synopsis-length copy.
- Console and browser must continue to consume the same shared route text through `DarenShowcaseBeat.PlayerText` and `QteChapter.Narrative`.

## Requirements

### Functional Requirements

- **FR-001**: `staff_theft` MUST be a substantial Russian dark-fantasy literary page rather than a one/two-sentence synopsis.
- **FR-002**: The scene MUST keep Daren as the active point-of-view protagonist through observation, intent, movement, hand control, breath, and balance.
- **FR-003**: The scene MUST include relic/theft setting details around the staff: velvet holders or supports, thin rings/suspension hardware, the staff or case/futlyar, weight, balance, and the act of fastening it to the belt/strap.
- **FR-004**: The scene MUST carry forward relevant pressure from the old lock/scratch, route-choice/orangery, listening house, alarm, or pursuit without rewriting those scenes.
- **FR-005**: The scene MUST make noise/evidence stakes clear: ringing, chime, scrape, scratch, dust, displaced velvet, trace, guards, or pursuit can expose Daren.
- **FR-006**: The focused test guard MUST use grouped motif checks, including staff/relic and holders; rings/suspension/chime noise; Daren hands/breath/body balance; belt/strap/futlyar securing; evidence/old-lock/scratch stakes; alarm/listening-house/pursuit pressure; and a natural `BalanceMeter` lead-in.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `BalanceMeter` type, characteristic, difficulty, routing targets, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing prose MUST NOT expose implementation or agent terminology.

### Key Entities

- **Daren staff-theft beat**: The shared authored scene `staff_theft` / "Кража посоха" presented to both console and browser players.
- **Existing QTE action contract**: The unchanged `staff_theft_action` contract that controls the existing balance action and route progression.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The new focused `DarenQteShowcaseTests` guard fails against the current synopsis and passes after the scene is rewritten.
- **SC-002**: The `staff_theft` narrative is at least 1500 characters, has at least 12 scene sentences, and mentions Daren at least 5 times.
- **SC-003**: The scene satisfies all required grouped motif checks without relying on a single generic token bucket.
- **SC-004**: Focused Daren tests and the affected Daren/QTE/docs/browser C# slice pass locally.
- **SC-005**: Diff inspection and tests show no mechanics, reward, endpoint, runtime-state, route-order, or frontend drift.

## Verification Plan

- **C# verification**:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"`
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`
- **Documentation/contract verification**: Spec Kit prerequisite check and contract/checklist reconciliation; no GM-facing docs expected because no GM-authored capability or runtime contract changes.
- **Frontend verification**: Not required unless frontend/React files change or a browser rendering bug is found.
- **Manual/player-facing verification**: Read the final `staff_theft` prose and inspect the diff for prohibited implementation terminology and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/980-daren-staff-theft` was created from `origin/main` at `4bb6896`.
- Baseline tests and Spec Kit prerequisite evidence are recorded in `plan.md` and `tasks.md` before Codex implementation starts.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #980 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from the shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
