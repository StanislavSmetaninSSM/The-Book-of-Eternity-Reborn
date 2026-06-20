# Feature Specification: Console Readiness Sequence 2

**Feature Branch**: `feature/1163-console-readiness`

**Created**: 2026-06-20

**Status**: Draft for autonomous implementation

**Input**: GitHub issues #1158, #1160, #1161, #1162, #1163, #1164, #1165, and #1166.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1158 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1158
  - #1160 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1160
  - #1161 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1161
  - #1162 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1162
  - #1163 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1163
  - #1164 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1164
  - #1165 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1165
  - #1166 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1166
- **Issue type**: console readiness hardening, Agent Console observability, GM worker E2E, afterlife output sweep, live playtest.
- **Spec Kit justification**: Required. The work spans multiple GitHub issues, Agent Console runtime behavior, player-facing console UX, afterlife output, QTE observability, GM worker tests/docs, and live E2E evidence.
- **Contract scope**: player-facing console output, Agent Console snapshot contract, GM worker profile/guidance cleanup, local E2E tests, afterlife command rendering, live playtest artifacts. Browser/frontend work is out of scope unless a shared C# command-result contract must compile after a console-only change.
- **Out of scope**: Browser UI/frontend parity tasks, GLM-owned browser work, broad narrative rewrites, QTE balance redesign beyond testability/observability, new afterlife mechanics, cloud services, telemetry.

## User Scenarios & Testing

### User Story 1 - GM Worker Reliability Is Testable (Priority: P1)

A maintainer can prove that the local GM worker bridge does not report ready when the underlying Codex worker process is dead, and can run live Codex worker E2E flows for narrative proposal and validation repair delegation.

**Independent Test**: Focused GM worker/Agent Console tests reproduce the false-ready state and cover live worker dispatch outcomes with deterministic or live-safe assertions.

**Acceptance Scenarios**:

1. **Given** a configured Codex worker process fails to start or exits before accepting work, **When** the bridge reports worker status, **Then** it does not expose a false ready/available state.
2. **Given** a narrative draft delegation request, **When** a live Codex worker is launched in the local test harness, **Then** the main GM receives a proposal artifact without exposing extra user windows.
3. **Given** a validation failure delegation request, **When** a live Codex worker is launched in the local test harness, **Then** the validation-repair path produces a traceable proposal or a precise failure report.

### User Story 2 - QTE State Is Observable Through Agent Console (Priority: P1)

An automated player can observe a live QTE mini-game frame through Agent Console snapshots without scraping terminal pixels or reading hidden game-session files.

**Independent Test**: A headless/scripted Agent Console or service-level test starts at least one timed QTE and one choice/non-timed QTE, reads structured QTE frame state, submits input, and verifies state progression.

**Acceptance Scenarios**:

1. **Given** a live QTE frame is running, **When** Agent Console publishes a snapshot, **Then** the snapshot includes QTE id/type/title, phase, visible prompt, expected input shape, timing/progress fields where meaningful, and latest feedback.
2. **Given** a QTE has no active frame, **When** Agent Console publishes a snapshot, **Then** the QTE frame field is absent or explicitly inactive without stale data.

### User Story 3 - Afterlife Output Is Player-Facing By Default (Priority: P1)

A player using afterlife commands sees localized, navigable summaries and details instead of default raw JSON/audit dumps.

**Independent Test**: Reusable Chaos Sea and Shining Abode command-display saves validate, and command-output tests prove default afterlife views do not include raw JSON blocks or "Полный JSON" sections unless the command is explicitly diagnostic.

**Acceptance Scenarios**:

1. **Given** a normal player-facing afterlife command, **When** it renders dense data, **Then** it uses Russian labels, summaries, selectable details or clear detail commands, and no raw JSON by default.
2. **Given** a debug/audit command intentionally exposes raw data, **When** it renders, **Then** the command is labeled as diagnostic and remains outside ordinary player flow.

### User Story 4 - Console Polish Pass 2 Improves Playability (Priority: P1)

A player can use ordinary console commands without seeing internal keys, DTO/API/endpoint words, raw enum values, dead-end menus, or unlocalized mechanical fields.

**Independent Test**: A command-by-command audit runs against prepared test saves and records quality findings. Narrow defects are fixed with RED/GREEN tests; broader issues become precise GitHub follow-ups.

**Acceptance Scenarios**:

1. **Given** ordinary player commands in Mortal World and reachable non-mortal modes, **When** the audit runs, **Then** each command is classified for readability, localization, navigation, detail access, and debug leakage.
2. **Given** a narrow high-friction defect is found, **When** the branch fixes it, **Then** a regression test fails before the fix and passes after it.

### User Story 5 - Second Live Playtest Measures Friction (Priority: P2)

After the polish and observability gaps are addressed, a second live console playtest can judge player friction, not only blocker bugs.

**Independent Test**: A live Agent Console run records launch metadata, snapshots, route notes, command quality findings, issue links, and a final console playability score.

**Acceptance Scenarios**:

1. **Given** the console client starts from a valid reusable test save, **When** the live playtest runs, **Then** it covers guardian interaction, mortal exploration, status/inventory/books/effects/NPC/faction/world-news views, QTE or simple challenge, lifecycle transition, rewards, and new-life/afterlife inspection where feasible.
2. **Given** the playtest ends, **When** the report is written, **Then** it names blockers, high-friction issues, polish issues, fixed defects, residual risk, and the current 1-10 playability score.

## Requirements

### Functional Requirements

- **FR-001**: Remove deprecated Gemini CLI worker profiles/guidance from default settings and docs, while keeping Codex as the supported local worker test target.
- **FR-002**: GM worker status MUST not report ready/available solely because a stale file, stale process id, or unaccepted bridge prompt exists.
- **FR-003**: Narrative-draft and validation-repair worker E2E tests MUST exercise Codex worker launch or document a live-environment blocker with precise reproduction steps.
- **FR-004**: Agent Console snapshots MUST expose structured active QTE frame state while live QTE frames are running.
- **FR-005**: QTE frame state MUST be mechanics-aware enough for tests to understand current prompt, expected input, timing/progress, and latest feedback without OCR.
- **FR-006**: Normal afterlife command output MUST NOT show raw JSON or "Полный JSON" blocks by default.
- **FR-007**: Any retained raw/audit afterlife output MUST be available only through explicitly diagnostic/player-advanced surfaces.
- **FR-008**: Console polish fixes MUST use Russian in-world player-facing labels and avoid raw internal ids/enums unless the id is itself meaningful in-world.
- **FR-009**: Dense screens touched by this feature SHOULD expose summary -> detail -> back/return navigation where practical, or create a follow-up issue when this is too broad.
- **FR-010**: The final live playtest MUST use a disposable session and MUST NOT mutate the user's production `game_session`.
- **FR-011**: Browser/frontend files MUST NOT be changed unless a shared C# change otherwise breaks compilation; if touched, GLM/browser task ownership must be noted separately.

### Key Entities

- **Worker Profile**: A local GM-worker configuration describing command, arguments, visibility, environment, and proposal protocol.
- **Worker Status**: Runtime state proving whether a worker process is live, accepting work, busy, failed, or stale.
- **QTE Frame Snapshot**: Structured Agent Console state for the active mini-game frame.
- **Afterlife Output Surface**: A Chaos Sea, Shining Abode, spiritual conflict, or afterlife profile command rendered to a player.
- **Command Quality Finding**: A classified player-facing issue with command, mode/save, expected behavior, actual output, severity, and fix/follow-up link.
- **Live Playtest Run**: A disposable end-to-end console run with launch commands, session source, snapshots, actions, notes, and final score.

## Success Criteria

- **SC-001**: Focused tests cover false-ready worker status, deprecated Gemini removal, and at least one worker delegation E2E or documented live blocker.
- **SC-002**: Focused tests cover structured Agent Console QTE frame snapshots for at least one timed QTE and one non-timed/choice-style QTE.
- **SC-003**: Afterlife output sweep records and fixes or tracks all default raw JSON/"Полный JSON" player-facing leaks found in the prepared saves.
- **SC-004**: Console polish audit covers ordinary commands and records fixed defects or follow-up issues for unresolved high-friction findings.
- **SC-005**: The second live playtest report includes artifact references, command coverage, friction findings, and a playability score.
- **SC-006**: Fresh verification evidence is recorded before PR/merge: focused tests, relevant broad tests, build, Spec Kit prerequisite check, diff hygiene, and live E2E notes.

## Verification Plan

- **Spec Kit prerequisite check**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` with `SPECIFY_FEATURE_DIRECTORY=specs/1163-console-readiness-sequence`.
- **Focused C# tests**: targeted filters for `GmWorker`, `AgentConsole`, `Qte`, `Afterlife`, and any new command-output tests.
- **Build**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- **Broad C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore` before merge if code changes land.
- **Docs/contracts**: `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests` if afterlife runtime contracts, GM prompts, examples, or manifests change.
- **Manual/live evidence**: live Agent Console playtest artifacts for #1166 after implementation tasks complete.

## Assumptions

- `codex --dangerously-bypass-approvals-and-sandbox` is available for live worker tests on this machine.
- Existing reusable Mortal World, Chaos Sea, and Shining Abode saves are sufficient for command-output coverage; if not, missing fixture coverage becomes an explicit finding.
- Some broad player-facing UX gaps may be too large for this branch; they must become linked follow-up issues rather than silent deferrals.
- Browser client work remains GLM-owned.
