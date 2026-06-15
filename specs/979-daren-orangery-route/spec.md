# Feature Specification: Daren Scene 11 Full Literary Page

**Feature Branch**: `work/979-daren-orangery-route`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#979](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/979), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#978](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/978), next scene [#980](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/980)

## Source Issues & Scope

- **Source GitHub issue**: #979 - rewrite scene `route_decision` / "Развилка в оранжерее" as a full Russian dark-fantasy literary page.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #979 changes player-facing story/UX content over shared console/browser QTE route data. It is the next open per-scene Daren literary-page child after #978, must preserve console/browser parity through shared C# data, and must not drift route mechanics, rewards, endpoints, runtime state, or sibling scene scope.
- **Contract scope**: player-facing, console, browser, shared route data, C# source guard tests. No GM-facing prompt, docs, examples, validation, runtime-state, or frontend contract change is intended because this scene is client-owned authored showcase prose and does not add a GM-authored capability.
- **In scope**: one substantial Russian prose page for `route_decision`, a focused objective guard that fails on synopsis-length copy, and local verification evidence.
- **Out of scope**: rewriting scenes #980-#983 or result/aftermath issues #988-#1014, changing already-merged #969-#978 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, adding social NPC dialogue where this beat has no named social slot, or adding browser-only/console-only story forks.

## Current Main Text

> В оранжерее перед Дареном раскрываются три выхода: мокрое стекло, служебная калитка и яркая арка. Ему нужно выбрать путь, который смоет след и не отдаст погоне направление, пока посох ещё можно вынести тихо.

## User Scenarios & Testing

### User Story 1 - Orangery Route Choice Reads As a Page (Priority: P1)

As a player reading Daren's QTE showcase, I want the "Развилка в оранжерее" beat to feel like a tense dark-fantasy route-choice scene, so I experience Daren entering the wet glasshouse after the alarm-pulse corridor, weighing three concrete exits, misdirecting pursuit, and committing to a path rather than receiving a compact briefing.

**Why this priority**: This is the only user-visible value of #979 and continues the parent #955 goal of replacing synopsis beats with interactive-book prose.

**Independent Test**: The scene can be tested independently by reading `route_decision` from the shared route data and verifying the authored prose and unchanged action contract.

**Acceptance Scenarios**:

1. **Given** the player reaches `route_decision`, **When** the scene text is rendered by console or browser, **Then** the text is a substantial Russian literary page centered on Daren inside the orangery rather than a one/two-sentence summary.
2. **Given** the previous alarm-pulse corridor pressure, **When** the scene begins, **Then** the prose naturally carries forward red alarm residue, breath/body control, trace danger, pursuit pressure, or staff-case/posoh stakes into the route choice.
3. **Given** the existing route-choice action beat, **When** route data is inspected, **Then** the beat id, title, action id, `PrecisionChoice` check, characteristic, config, routing, score deltas, and rewards remain unchanged.

### Edge Cases

- The scene must not force Mira-style dialogue or invent an NPC slot because `route_decision` is an action/choice beat, not a named social scene.
- The prose must remain player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE` terms in default narrative.
- The page must stay within the existing broad Daren narrative length guard while still being substantial enough to reject synopsis-length copy.
- Console and browser must continue to consume the same shared route text through `DarenShowcaseBeat.PlayerText` and `QteChapter.Narrative`.

## Requirements

### Functional Requirements

- **FR-001**: `route_decision` MUST be a substantial Russian dark-fantasy literary page rather than a one/two-sentence synopsis.
- **FR-002**: The scene MUST keep Daren as the active point-of-view protagonist through observation, intent, movement, breath, and route-choice control.
- **FR-003**: The scene MUST include setting and atmosphere around the orangery/greenhouse: wet glass, plants, condensation, moon or alarm light, and the listening house.
- **FR-004**: The scene MUST carry forward relevant pressure from the previous alarm-pulse corridor and staff-case burden without rewriting that scene.
- **FR-005**: The scene MUST present three concrete exits/routes (wet glass route, service gate, bright arch, or equivalent existing-route options) and make pursuit/trace-misdirection stakes clear.
- **FR-006**: The focused test guard MUST use grouped motif checks, including orangery/wet glass/plants; red alarm or moonlit residue; Daren body/breath/step control; three exits/routes; pursuit, trace-washing, or misdirection stakes; and a natural `PrecisionChoice` route-selection lead-in.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `PrecisionChoice` type, characteristic, difficulty, config, routing targets, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing prose MUST NOT expose implementation or agent terminology.

### Key Entities

- **Daren route-decision beat**: The shared authored scene `route_decision` / "Развилка в оранжерее" presented to both console and browser players.
- **Existing QTE action contract**: The unchanged `route_decision_action` contract that controls the existing route-choice action and route progression.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The new focused `DarenQteShowcaseTests` guard fails against the current synopsis and passes after the scene is rewritten.
- **SC-002**: The `route_decision` narrative is at least 1500 characters, has at least 12 scene sentences, and mentions Daren at least 5 times.
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
- **Manual/player-facing verification**: Read the final `route_decision` prose and inspect the diff for prohibited implementation terminology and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/979-daren-orangery-route` was created from `origin/main` at `426004f`.
- Baseline tests and Spec Kit prerequisite evidence will be recorded in `plan.md` and `tasks.md` before Codex implementation starts.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #979 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from the shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
