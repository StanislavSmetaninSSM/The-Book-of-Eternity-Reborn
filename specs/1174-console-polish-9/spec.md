# Feature Specification: Console Client Polish Pass 2 to 9/10

**Feature Branch**: `work/1174-console-polish-9`

**Created**: 2026-06-20

**Status**: Draft for autonomous implementation

**Input**: GitHub issues #1174, #1175, #1176, #1177, #1178, #1179, #1180, #1181, #1182, and #1183.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1174 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1174
  - #1175 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1175
  - #1176 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1176
  - #1177 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1177
  - #1178 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1178
  - #1179 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1179
  - #1180 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1180
  - #1181 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1181
  - #1182 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1182
  - #1183 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1183
- **Issue type**: console UX hardening, command coverage audit, dry sweep, live E2E, acceptance scoring.
- **Spec Kit justification**: Required. The work is an epic, spans multiple issues and sessions, changes player-facing console UX, touches Mortal World and afterlife command surfaces, and requires durable acceptance criteria.
- **Contract scope**: player-facing console output, command coverage documentation, test fixture coverage, dry command sweep, live console playtest artifacts. Browser and QTE work are explicitly out of scope.
- **Out of scope**:
  - Browser client, React/Vite/WebFrontend, and browser command rendering.
  - QTE engine, QTE balancing, QTE frame visibility, QTE test mode, and QTE scenario work.
  - Broad new mechanics or narrative rewrites unless a narrow console-output defect cannot be tested without a small fixture copy update.

## User Scenarios & Testing

### User Story 1 - Every Non-QTE Console Command Has Coverage Context (Priority: P1)

A maintainer can open a command matrix and see every ordinary non-QTE console command, where it is available, what test data proves it, and what quality risks remain.

**Independent Test**: Review the command matrix and verify that each non-QTE command has realm, lifecycle, test data, expected output shape, and status.

**Acceptance Scenarios**:

1. **Given** a console command exists in help, routing, or command result builders, **When** the matrix is read, **Then** the command appears unless it is explicitly QTE or browser-only.
2. **Given** a command needs fixture data, **When** the matrix is read, **Then** it names the current save/session fixture or marks the data gap with a follow-up action.

### User Story 2 - Dry Sweep Finds Console Regressions Before Live Play (Priority: P1)

A developer can run a reproducible dry sweep over non-QTE commands to catch hangs, markup crashes, empty output, raw JSON, internal ids, and missing navigation signals.

**Independent Test**: Run the documented dry sweep command against prepared test data and inspect the generated report.

**Acceptance Scenarios**:

1. **Given** prepared Mortal World and afterlife fixtures, **When** the sweep runs, **Then** each non-QTE command receives pass, warn, fail, or skipped status with a reason.
2. **Given** player-facing output contains raw JSON, internal keys, DTO/API words, unlocalized enum/null values, or Spectre markup errors, **When** the sweep evaluates it, **Then** the report flags it.

### User Story 3 - Mortal World Commands Are Useful Without JSON Knowledge (Priority: P1)

A player can use ordinary Mortal World commands and receive readable Russian summaries, detail choices, and back navigation without reading raw JSON or internal fields.

**Independent Test**: Run focused command-display tests and inspect console output for status, inventory/items, books, effects, skills, quests, NPCs, factions, map/location, and world news.

**Acceptance Scenarios**:

1. **Given** a dense Mortal World entity list, **When** the player opens the command, **Then** the first screen gives useful names and summaries rather than raw storage structures.
2. **Given** the player opens one entity, **When** details are available, **Then** the detail screen uses localized labels and exposes all meaningful data without raw JSON by default.
3. **Given** the player finishes reading details, **When** navigation is offered, **Then** the player can return to the list or close without retyping the root command.

### User Story 4 - Afterlife Commands Are Player-Facing By Default (Priority: P1)

A player can inspect Chaos Sea and Shining Abode state through localized, navigable screens instead of default audit dumps.

**Independent Test**: Run focused afterlife command-display tests and dry sweep afterlife fixtures.

**Acceptance Scenarios**:

1. **Given** a normal afterlife player command, **When** it renders, **Then** it does not include raw JSON or "Полный JSON" sections by default.
2. **Given** audit data is still needed for advanced diagnostics, **When** it is exposed, **Then** the command or section is explicitly diagnostic and outside ordinary player flow.

### User Story 5 - Live Console Playtest Measures Player Friction (Priority: P2)

After matrix, sweep, and narrow polish fixes, a live Codex-GM run checks whether the console feels playable rather than merely non-crashing.

**Independent Test**: Run a disposable console session with Codex-GM bridge and record route, command coverage, friction findings, fixes/follow-ups, and final score.

**Acceptance Scenarios**:

1. **Given** a disposable session, **When** the playtest runs, **Then** it uses player-visible console/Agent Console flow and does not require manual JSON editing as normal play.
2. **Given** the run ends, **When** the report is written, **Then** it assigns a 1-10 score and explains why 9/10 is or is not justified.

## Requirements

### Functional Requirements

- **FR-001**: The command matrix MUST cover ordinary non-QTE console commands across Mortal World, Chaos Sea, Shining Abode, GM bridge/live-turn surfaces, and explicit debug/audit commands.
- **FR-002**: QTE commands and browser/frontend commands MUST be explicitly excluded from this pass.
- **FR-003**: The dry sweep MUST use disposable or fixture game data and MUST NOT mutate the user's live `BookOfEternityClient/game_session` as a normal test path.
- **FR-004**: Player-facing console output touched by this feature MUST use Russian in-world labels and MUST NOT show raw JSON, DTO/API/endpoint words, internal file paths, internal ids, raw enum values, or `null` unless an explicit diagnostic mode is selected.
- **FR-005**: Dense command surfaces SHOULD use summary -> select -> detail -> back/close navigation. If this is too broad for a surface, the branch MUST record a precise follow-up issue instead of silently accepting the gap.
- **FR-006**: Narrow console defects fixed in this feature MUST have RED/GREEN regression tests or a documented reason why automated coverage is not feasible.
- **FR-007**: Afterlife runtime or GM-authored contract changes MUST update the relevant afterlife documentation/examples/tests in the same change. Pure display-only changes that do not alter contracts MUST state that no GM docs are required.
- **FR-008**: The final live playtest MUST record launch commands, session source, route, command coverage, issues found, verification commands, and residual risk.
- **FR-009**: The final score rubric MUST classify remaining problems as blocker, major, minor, or polish. 9/10 is allowed only if no blocker or major non-QTE console-flow issue remains known.

### Key Entities

- **Command Matrix Entry**: Command alias, realm, lifecycle, input mode, fixture/save source, expected output, detail/navigation expectation, current status, and issue link.
- **Dry Sweep Result**: Command execution record with pass/warn/fail/skipped status, symptom category, captured output excerpt, and recommended action.
- **Console Quality Finding**: Player-facing defect with command, mode, expected output, actual output, severity, fix/follow-up, and verification evidence.
- **Live Playtest Report**: Disposable run summary with route, artifacts, command coverage, player-friction notes, score, and residual risk.

## Success Criteria

- **SC-001**: The repository contains a command matrix for all ordinary non-QTE console commands with fixture coverage status.
- **SC-002**: A reproducible dry sweep exists and produces a readable report for non-QTE command output.
- **SC-003**: Focused tests cover the new sweep or command-quality guards and every narrow output/navigation fix made by this branch.
- **SC-004**: Mortal World command-output defects found by the matrix/sweep are fixed or converted to precise follow-up issues.
- **SC-005**: Afterlife command-output defects found by the matrix/sweep are fixed or converted to precise follow-up issues.
- **SC-006**: A live Codex-GM console playtest without QTE is attempted after dry fixes and has a report with a 1-10 score.
- **SC-007**: Fresh verification evidence is recorded before merge: focused tests, build, diff hygiene, and live/manual evidence where applicable.

## Verification Plan

- **Spec Kit prerequisite check**:
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` with `SPECIFY_FEATURE_DIRECTORY=specs/1174-console-polish-9`.
- **Build**:
  `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- **Focused C# tests**:
  targeted filters for command-display, afterlife display, fixture integrity, dry sweep, and each fixed defect.
- **Broad C# verification**:
  `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests"` when code changes land.
- **Afterlife docs verification**:
  `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"` if afterlife runtime contracts or GM-facing docs change.
- **Manual/player-facing verification**:
  dry sweep report plus live Codex-GM console playtest without QTE.

## Assumptions

- Existing reusable Mortal World, Chaos Sea, and Shining Abode fixtures are the starting point; missing fixture coverage becomes a finding.
- The `codex --dangerously-bypass-approvals-and-sandbox` command is available for the live GM bridge test.
- Some UX gaps may be too broad for this branch. Those gaps must become linked follow-up issues with enough detail for another agent.
- Browser client work remains assigned elsewhere and must not be touched in this feature.
