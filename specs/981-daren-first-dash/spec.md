# Feature Specification: Daren Scene 13 Full Literary Page

**Feature Branch**: `work/981-daren-first-dash`
**Created**: 2026-06-15
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#980](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/980), next scene [#982](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/982)

## Source Issues & Scope

- **Source GitHub issue**: #981 - rewrite scene `pursuit` / "Первый рывок" as a full Russian dark-fantasy literary page.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #981 changes player-facing story/UX content over shared console/browser QTE route data. It is the next open per-scene Daren literary-page child after #980, must preserve console/browser parity through shared C# data, and must not drift route mechanics, rewards, endpoints, runtime state, or sibling scene scope.
- **Contract scope**: player-facing, console, browser, shared route data, C# source guard tests. No GM-facing prompt, docs, examples, validation, runtime-state, or frontend contract change is intended because this scene is client-owned authored showcase prose and does not add a GM-authored capability.
- **In scope**: one substantial Russian prose page for `pursuit`, a focused objective guard that fails on synopsis-length copy, and local verification evidence.
- **Out of scope**: rewriting scenes #982-#983 or result/aftermath issues #988-#1014, changing already-merged #969-#980 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, inventing an unrelated social encounter, or adding browser-only/console-only story forks.

## Current Main Text

> За спиной Дарена распахивается зал, и капитан Орвальд Шпиль уже кричит во дворе, где открытое окно сужается в полоску ночи. Если Лукьян стал свидетелем у двери, этот рывок отдаст погоне лицо и имя; точный момент ещё может сбить их темп.

## User Scenarios & Testing

### User Story 1 - First Dash Reads As a Page (Priority: P1)

As a player reading Daren's QTE showcase, I want the "Первый рывок" beat to feel like a tense dark-fantasy escape scene, so I experience Daren launching from the staff-theft hall toward the open window while Captain Orvald Shpil's pursuit, possible Lukyan witness pressure, lanterns, guards, the stolen staff, and exact timing make the first dash matter.

**Why this priority**: This is the only user-visible value of #981 and continues the parent #955 goal of replacing synopsis beats with interactive-book prose.

**Independent Test**: The scene can be tested independently by reading `pursuit` from the shared route data and verifying the authored prose and unchanged action contract.

**Acceptance Scenarios**:

1. **Given** the player reaches `pursuit`, **When** the scene text is rendered by console or browser, **Then** the text is a substantial Russian literary page centered on Daren making the first escape dash rather than a one/two-sentence summary.
2. **Given** the prior staff-theft beat, **When** the scene begins, **Then** the prose carries forward the stolen staff, belt/strap balance, ringing/evidence risk, and the house/guards waking behind him.
3. **Given** the existing first-dash action beat, **When** route data is inspected, **Then** the beat id, title, action id, `TimingBar` check, characteristic, difficulty, routing, score deltas, and rewards remain unchanged.

### Edge Cases

- Captain Orvald Shpil, Lukyan, guards, lanterns, or courtyard voices may appear as pursuit pressure, but the scene must not create a new dialogue runtime or branch state.
- The prose may include shouted commands or visible social pressure if they serve the chase, but Daren remains the active point-of-view protagonist.
- The prose must remain player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE` terms in default narrative.
- The page must stay within the existing broad Daren narrative length guard while still being substantial enough to reject synopsis-length copy.
- Console and browser must continue to consume the same shared route text through `DarenShowcaseBeat.PlayerText` and `QteChapter.Narrative`.

## Requirements

### Functional Requirements

- **FR-001**: `pursuit` MUST be a substantial Russian dark-fantasy literary page rather than a one/two-sentence synopsis.
- **FR-002**: The scene MUST keep Daren as the active point-of-view protagonist through observation, intent, breath, legs, shoulders, hands, and timing.
- **FR-003**: The scene MUST include the physical first-dash setting: the opened hall or door behind him, the open window or threshold, courtyard/night air, lanterns or guards, and the narrowing escape line.
- **FR-004**: The scene MUST carry forward prior staff-theft pressure: the stolen staff, belt/strap/futlyar balance, possible ring/noise/evidence risk, and the house/guards waking behind him.
- **FR-005**: The scene MUST include named pursuit or witness pressure from Captain Orvald Shpil and/or Lukyan when relevant, without making either NPC the point-of-view character or adding a new dialogue system.
- **FR-006**: The scene MUST make timing stakes clear: Daren must hit the exact moment before the courtyard closes, guards converge, lanterns catch him, or pursuit receives his rhythm/face/name.
- **FR-007**: The focused test guard MUST use grouped motif checks, including hall/window/courtyard; Daren body/breath/step timing; stolen staff/belt/balance; Orvald/Lukyan/witness or guard pressure; lantern/voice/pursuit control; and a natural `TimingBar` lead-in.
- **FR-008**: The implementation MUST preserve route order, beat id, title, action id, action label, `TimingBar` type, characteristic, difficulty, routing targets, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-009**: Default player-facing prose MUST NOT expose implementation or agent terminology.

### Key Entities

- **Daren first-dash beat**: The shared authored scene `pursuit` / "Первый рывок" presented to both console and browser players.
- **Existing QTE action contract**: The unchanged `pursuit_action` contract that controls the existing timing/speed action and route progression to `chase_chain`.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The new focused `DarenQteShowcaseTests` guard fails against the current synopsis and passes after the scene is rewritten.
- **SC-002**: The `pursuit` narrative is at least 1500 characters, has at least 12 scene sentences, and mentions Daren at least 5 times.
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
- **Manual/player-facing verification**: Read the final `pursuit` prose and inspect the diff for prohibited implementation terminology and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/981-daren-first-dash` was created from `origin/main` at `583bf5b`.
- Baseline tests and Spec Kit prerequisite evidence are recorded in `plan.md` and `tasks.md` before Codex implementation starts.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Assumptions

- Issue #981 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from the shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
