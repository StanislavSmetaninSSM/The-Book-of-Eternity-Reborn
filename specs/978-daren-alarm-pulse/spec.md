# Feature Specification: Daren Scene 10 Full Literary Page

**Feature Branch**: `work/978-daren-alarm-pulse`
**Created**: 2026-06-12
**Status**: Spec ready for Codex implementation
**Tracked issue and related context**: [#978](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/978), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977)

## Source Issues & Scope

- **Source GitHub issue**: #978 - rewrite scene `timed_rhythm` / "Пульс сигнализации" as a full Russian dark-fantasy literary page.
- **Parent**: #955 - Daren's QTE training route must feel like an interactive book, not a mechanical QTE test.
- **Spec Kit justification**: #978 changes player-facing story/UX content over shared console/browser QTE route data. It is a rhythm/timing action scene after the staff-case/heavy-grate pressure of #977, must preserve console/browser parity through shared C# data, and must not drift route mechanics, rewards, endpoints, runtime state, or sibling scene scope.
- **Contract scope**: player-facing, console, browser, shared route data, C# source guard tests. No GM-facing prompt, docs, examples, validation, runtime-state, or frontend contract change is intended because this scene is client-owned authored showcase prose and does not add a GM-authored capability.
- **In scope**: one substantial Russian prose page for `timed_rhythm`, a focused objective guard that fails on synopsis-length copy, and local verification evidence.
- **Out of scope**: rewriting scenes #979-#983 or result/aftermath scenes #988-#1014, changing already-merged #969-#977 prose except for neutral shared test helpers if unavoidable, closing parent #955, changing QTE mechanics/check types/routing/scoring/rewards/profile/New Game grants/endpoints/runtime state, adding a new dialogue runtime, adding social NPC dialogue where this beat has no named social slot, or adding browser-only/console-only story forks.

## Current Main Text

> В коридоре Дарен видит сигнальный кристалл, который бьёт красным светом по полу и стенам. Между вспышками остаются короткие паузы, и он должен двигаться в их ритме, пока тревога не поймала его тень.

## User Scenarios & Testing

### User Story 1 - Alarm Pulse Scene Reads As a Page (Priority: P1)

As a player reading Daren's QTE showcase, I want the "Пульс сигнализации" beat to feel like a tense dark-fantasy timing scene, so I experience Daren moving with his breath, shadow, staff-case burden, red crystal pulses, silence, and alarm pressure rather than receiving a compact briefing.

**Why this priority**: This is the only user-visible value of #978 and continues the parent #955 goal of replacing synopsis beats with interactive-book prose.

**Independent Test**: The scene can be tested independently by reading `timed_rhythm` from the shared route data and verifying the authored prose and unchanged action contract.

**Acceptance Scenarios**:

1. **Given** the player reaches `timed_rhythm`, **When** the scene text is rendered by console or browser, **Then** the text is a substantial Russian literary page centered on Daren crossing the alarm-lit corridor rather than a one/two-sentence summary.
2. **Given** the previous heavy-grate/staff-case pressure, **When** the scene begins, **Then** the prose naturally carries forward Daren's burden, pain, blood/breath/body control, staff/posoh stakes, or awakened-house danger into the corridor rhythm.
3. **Given** the existing rhythm action beat, **When** route data is inspected, **Then** the beat id, title, action id, `RhythmPulse` check, Speed characteristic, config, routing, score deltas, and rewards remain unchanged.

### Edge Cases

- The scene must not force Mira-style dialogue or add a named NPC exchange because `timed_rhythm` is a physical/timing stealth beat, not a social slot.
- The prose must remain player-facing and in-world, with no `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, or `QTE` terms in default narrative.
- The page must stay within the existing broad Daren narrative length guard while still being substantial enough to reject synopsis-length copy.
- Console and browser must continue to consume the same shared route text through `DarenShowcaseBeat.PlayerText` and `QteChapter.Narrative`.

## Requirements

### Functional Requirements

- **FR-001**: `timed_rhythm` MUST be a substantial Russian dark-fantasy literary page rather than a one/two-sentence synopsis.
- **FR-002**: The scene MUST keep Daren as the active point-of-view protagonist through observation, intent, movement, breath, and body-timing control.
- **FR-003**: The scene MUST include setting and atmosphere around the corridor, signal crystal, red light/pulses, shadows, floor/walls, and the listening house.
- **FR-004**: The scene MUST carry forward relevant pressure from the previous heavy-grate/staff-case beat without rewriting that scene.
- **FR-005**: The scene MUST make the rhythm stakes clear: Daren must move between crystal pulses/pauses without letting red light catch his boot, shadow, staff-case, or trace and wake the alarm.
- **FR-006**: The focused test guard MUST use grouped motif checks, including signal crystal/red corridor; pulse/pauses/rhythm/intervals; Daren body/breath/step/shadow control; staff/posoh/heavy-grate continuity; silence/noise/alarm/guards/trace stakes; and the natural RhythmPulse action lead-in.
- **FR-007**: The implementation MUST preserve route order, beat id, title, action id, action label, `RhythmPulse` type, Speed characteristic, difficulty, config, routing targets, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, and frontend/backend boundaries.
- **FR-008**: Default player-facing prose MUST NOT expose implementation or agent terminology.

### Key Entities

- **Daren timed-rhythm beat**: The shared authored scene `timed_rhythm` / "Пульс сигнализации" presented to both console and browser players.
- **Existing QTE action contract**: The unchanged `timed_rhythm_action` contract that controls the existing speed/rhythm action and route progression.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The new focused `DarenQteShowcaseTests` guard fails against the current synopsis and passes after the scene is rewritten.
- **SC-002**: The `timed_rhythm` narrative is at least 1500 characters, has at least 12 scene sentences, and mentions Daren at least 5 times.
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
- **Manual/player-facing verification**: Read the final `timed_rhythm` prose and inspect the diff for prohibited implementation terminology and mechanics drift.
- **Static checks**:
  - `git diff --check origin/main...HEAD`
  - Added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting, excluding docs/spec artifacts as appropriate.

## Local Setup Evidence

- Branch `work/978-daren-alarm-pulse` was created from `origin/main` at `3918fcd`.
- Focused baseline before #978 implementation: `FullyQualifiedName~DarenQteShowcaseTests` passed 50 / failed 0 / skipped 0 / total 50.
- Affected baseline before #978 implementation: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests` passed 319 / failed 0 / skipped 0 / total 319.
- Hermes remains responsible for independent review, PR, merge, issue closure, parent #955 boundary confirmation, and cleanup.

## Implementation Evidence

- Spec Kit prerequisite check during Codex implementation resolved `FEATURE_DIR=E:\Games\worktrees\boe-978-daren-alarm-pulse\specs\978-daren-alarm-pulse` with `contracts/` and `tasks.md`.
- RED TDD evidence after adding `DarenTimedRhythm_ReadsAsAlarmPulsePageWithoutMechanicDrift`: focused Daren command passed 50 / failed 1 / skipped 0 / total 51; expected failure was the old `timed_rhythm` synopsis failing the substantial-page assertion.
- GREEN focused Daren evidence after rewriting the scene: focused Daren command passed 51 / failed 0 / skipped 0 / total 51.
- Affected Daren/QTE/docs/browser C# slice after implementation passed 320 / failed 0 / skipped 0 / total 320.
- Builds after implementation: client project succeeded with 0 warnings / 0 errors; test project succeeded with 0 warnings / 0 errors.
- `git diff --check origin/main...HEAD` returned exit 0 with no whitespace errors.
- Added-line static scan over non-Spec changed files returned `NO_MATCHES`.
- Final diff scope reconciliation: only `BookOfEternityClient/Services/QteSceneService.Daren.cs`, `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, and this #978 Spec Kit directory are intended to change; no frontend, GM-facing docs/examples, runtime state, endpoints, QTE mechanics, rewards, sibling scenes, result/aftermath issues, or parent #955 lifecycle changes are in scope.

## Assumptions

- Issue #978 and parent #955 are sufficient tracked tasks; no new GitHub issue is needed.
- `BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the shared authority for both console and browser Daren QTE route prose.
- No frontend files need to change because browser parity comes from the shared C# route data.
- No GM-facing prompt/example update is required because the change rewrites authored client showcase prose without adding or changing a GM-authored game capability, command, mechanic, state field, validation rule, response field, or runtime contract.
