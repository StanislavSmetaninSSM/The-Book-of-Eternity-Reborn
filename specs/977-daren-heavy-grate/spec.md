# Feature Specification: Daren Scene 09 Full Literary Page

**Feature Branch**: `work/977-daren-heavy-grate`
**Created**: 2026-06-12
**Status**: Implemented locally; pending Hermes review/PR/merge/closure
**Tracked issue and related context**: [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976)

## Source Issues & Scope

- **Source GitHub issue**: #977 - rewrite scene `physical_pressure` / "Тяжёлая решётка" as a full Russian dark-fantasy literary page.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #977 changes player-facing story/UX content over shared console/browser QTE route data. It is a physical action-pressure scene following Renara/rune/glass danger, must preserve console/browser parity through shared C# data, and must not drift route mechanics, rewards, endpoints, or runtime state.
- **Contract scope**: player-facing, console, browser, shared route data, C# source guard tests. No GM-facing prompt, docs, examples, validation, runtime-state, or frontend contract change is intended because this scene is client-owned authored showcase prose and does not add a GM-authored capability.
- **In scope**: one substantial Russian prose page for `physical_pressure`, a focused objective guard that fails on synopsis-length copy, and local verification evidence.
- **Out of scope**: rewriting scenes #978-#983 or result/aftermath scenes, changing already-merged #969-#976 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, adding social NPC dialogue where this beat has no named social slot, or adding browser-only/console-only story forks.

## Current Main Text

> После голоса Ренары футляр с посохом выходит из ниши, но над ним тяжёлая решётка начинает падать, давя Дарену на плечо. Ему нужно удержать железо до последнего дюйма: если оно сорвётся, грохот разнесёт тревогу по крылу.

## User Scenarios & Testing

### User Story 1 - Heavy Grate Scene Reads As a Page (Priority: P1)

As a player reading Daren's QTE showcase, I want the "Тяжёлая решётка" beat to feel like a tense physical dark-fantasy scene, so I experience Daren's body, craft, breath, iron pressure, and alarm stakes as the staff case moves out of the niche.

**Why this priority**: This is the only user-visible value of #977 and continues the parent #955 goal of replacing synopsis beats with interactive-book prose.

**Independent Test**: The scene can be tested independently by reading `physical_pressure` from the shared route data and verifying the authored prose and unchanged action contract.

**Acceptance Scenarios**:

1. **Given** the player reaches `physical_pressure`, **When** the scene text is rendered by console or browser, **Then** the text is a substantial Russian literary page centered on Daren holding the heavy grate rather than a one/two-sentence summary.
2. **Given** the previous Renara/rune/staff-case pressure, **When** the scene begins, **Then** the prose naturally carries forward Renara, glass, rune, staff, niche, house, or ward danger into the physical pressure.
3. **Given** the existing physical action beat, **When** route data is inspected, **Then** the beat id, title, action id, `MashInput` check, Strength characteristic, config, routing, score deltas, and rewards remain unchanged.

### Edge Cases

- The scene must not force Mira-style dialogue or add a named NPC exchange because `physical_pressure` is a physical/action pressure beat, not a social slot.
- The prose must remain player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE` terms in default narrative.
- The page must stay within the existing broad Daren narrative length guard while still being substantial enough to reject synopsis-length copy.
- Console and browser must continue to consume the same shared route text through `DarenShowcaseBeat.PlayerText` and `QteChapter.Narrative`.

## Requirements

### Functional Requirements

- **FR-001**: `physical_pressure` MUST be a substantial Russian dark-fantasy literary page rather than a one/two-sentence synopsis.
- **FR-002**: The scene MUST keep Daren as the active point-of-view protagonist through observation, intent, body control, and physical action.
- **FR-003**: The scene MUST include setting and atmosphere around the cabinet, niche, staff case, iron grate, house silence, and alarm risk.
- **FR-004**: The scene MUST carry forward relevant pressure from the previous Renara/rune/glass/staff-case beat without rewriting that scene.
- **FR-005**: The scene MUST make the physical stakes clear: Daren must hold or lift the falling heavy grate until the staff case clears the niche without a noise that wakes the wing.
- **FR-006**: The focused test guard MUST use grouped motif checks, including heavy grate/iron/weight; Daren body/shoulders/hands/breath/control; staff/case/Renara/rune continuity; silence/noise/alarm/guards/wing stakes; and the natural holding/lifting/last-inch action lead-in.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `MashInput` type, Strength characteristic, difficulty, config, routing targets, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing prose MUST NOT expose implementation or agent terminology.

### Key Entities

- **Daren physical-pressure beat**: The shared authored scene `physical_pressure` / "Тяжёлая решётка" presented to both console and browser players.
- **Existing QTE action contract**: The unchanged `physical_pressure_action` contract that controls the existing strength/mash action and route progression.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The new focused `DarenQteShowcaseTests` guard fails against the current synopsis and passes after the scene is rewritten.
- **SC-002**: The `physical_pressure` narrative is at least 1500 characters, has at least 12 scene sentences, and mentions Daren at least 5 times.
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
- **Manual/player-facing verification**: Read the final `physical_pressure` prose and inspect the diff for prohibited implementation terminology and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Implementation Evidence

- TDD RED: focused Daren test filter failed as expected after adding the #977 guard: 49 passed / 1 failed / 0 skipped / 50 total; failure reason was the compact `physical_pressure` synopsis not meeting substantial heavy-grate page length.
- GREEN focused Daren filter: 50 passed / 0 failed / 0 skipped / 50 total.
- Affected Daren/QTE/docs/browser C# slice: 319 passed / 0 failed / 0 skipped / 319 total.
- Client build and test-project build both completed with 0 warnings / 0 errors.
- Spec Kit prerequisites resolved this feature directory with `contracts/` and `tasks.md`.
- Working-tree `git diff --check` reported no whitespace errors; Git printed LF-to-CRLF normalization warnings for the two tracked edited files.
- Working-tree added-line static scan excluding Spec Kit docs returned `NO_MATCHES`.
- No frontend/React files changed, so frontend verification was not run.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #977 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from the shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
