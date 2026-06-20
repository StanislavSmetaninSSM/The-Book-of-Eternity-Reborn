# Feature Specification: Console Client Live Player-Readiness Pass

**Feature Branch**: `1157-console-player-readiness`

**Created**: 2026-06-20

**Status**: Draft

**Input**: GitHub issue #1157 asks for a live console-client player-readiness pass with a Codex GM bridge, command output audit, blocking fix work, and verification evidence.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1157 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1157
- **Issue type**: task / audit / hardening
- **Spec Kit justification**: This is player-facing console UX work expected to span live E2E testing, command-output audit, possible multi-file fixes, tests, and follow-up issue triage.
- **Contract scope**: player-facing, console, agent-console, GM bridge, tests, docs. Browser/frontend work is explicitly out of scope.
- **Out of scope**: Browser UI parity fixes assigned to GLM; game-balance redesign; broad narrative rewrites; changing GM-authored contracts unless a discovered blocker requires a tracked follow-up or explicit in-scope fix.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Live Console Adventure Can Be Tested (Priority: P1)

A tester can run the real console client against a disposable session with a live Codex GM bridge and drive play only through player-visible Agent Console observations.

**Why this priority**: Without a real playtest path, console quality cannot be judged from unit tests or JSON inspection.

**Independent Test**: Build the console client, create a disposable session, start the GM bridge with `codex --dangerously-bypass-approvals-and-sandbox`, start Agent Console, and execute the baseline route while preserving artifacts.

**Acceptance Scenarios**:

1. **Given** a clean disposable session, **When** the console client and GM bridge are launched, **Then** the Agent Console exposes player-visible snapshots and actions without requiring edits to production data.
2. **Given** a live GM bridge, **When** the player sends natural commands or slash commands, **Then** the run records snapshots, events, bridge status, and player-facing output for later triage.

---

### User Story 2 - Command Output Is Audited As Player-Facing UI (Priority: P1)

A tester can inspect each covered console command as a normal player and classify whether the output is useful, localized, navigable, and free of debug-only terminology.

**Why this priority**: The console client is currently the primary playable client, so command output quality directly determines playability.

**Independent Test**: During the live run, execute the covered mortal-world and reachable afterlife commands, then classify every response with severity and artifact links.

**Acceptance Scenarios**:

1. **Given** a player-visible command response, **When** it contains named entities such as effects, books, NPCs, factions, quests, items, skills, or world news, **Then** details are discoverable or the player receives a clear in-world reason why details are unavailable.
2. **Given** a covered command response, **When** it includes raw JSON, DTO/API terms, internal field names, malformed markup, or dead-end navigation, **Then** the defect is fixed if narrow and high-impact, or recorded as a precise follow-up issue.

---

### User Story 3 - Blocking Console Defects Are Repaired With Evidence (Priority: P2)

When the pass finds P0/P1 or repeated high-impact P2 console defects, the agent can fix them under the tracked issue and prove the repair with automated or live evidence.

**Why this priority**: The audit should improve the playable client, not only produce notes.

**Independent Test**: For each in-scope fix, add a failing regression or source-guard test first, apply the repair, rerun focused tests, rerun affected playtest steps, and record the result.

**Acceptance Scenarios**:

1. **Given** a blocking defect is found in a console command, **When** it is narrow enough to repair safely, **Then** a regression test fails before the fix and passes after the fix.
2. **Given** a defect is too broad or out of scope, **When** it is triaged, **Then** a GitHub issue includes symptom, command/action, expected behavior, actual behavior, severity, and artifact references.

### Edge Cases

- The GM bridge may fail to become ready or may reject a prompt; this is a test-harness failure unless caused by the player-facing client flow.
- The session may start in afterlife, mortal world, or repair mode; the route begins from the visible state and records which lifecycle was tested.
- Some commands may be unavailable in the current lifecycle; unavailable commands pass only if they explain the limitation in player-facing terms.
- If a command exposes a summary with no detail authority, the pass treats it as at least P2 unless an explicit in-world reason is shown.
- Any required manual JSON edit during play is a failure, not a valid recovery.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pass MUST use a disposable copy of game session data and MUST NOT mutate production or developer sessions.
- **FR-002**: The live GM bridge MUST be launched with `codex --dangerously-bypass-approvals-and-sandbox` unless the run records a tool-availability blocker.
- **FR-003**: During the play segment, the player side MUST rely only on player-visible Agent Console snapshots, events, options, and command responses.
- **FR-004**: The pass MUST cover the visible current lifecycle and, when reachable, mortal-world commands for status, inventory/items, books, quests, NPCs, factions, location/map, world news, effects, skills, combat or QTE entry points, and lifecycle transitions.
- **FR-005**: The pass MUST classify failures using P0/P1/P2/P3 severity rules and preserve enough artifact context for reproduction.
- **FR-006**: In-scope console defects fixed during the pass MUST include regression tests, source guards, or documented manual-live evidence when automated coverage is not feasible.
- **FR-007**: Player-facing console output touched by fixes MUST use Russian in-world terminology and MUST NOT expose raw JSON, DTO/API/endpoint terms, internal file paths, or agent meta-language outside explicit debug surfaces.
- **FR-008**: Browser/frontend parity work MUST be left to GLM-labelled tasks unless a console fix has a direct shared-renderer regression that cannot be repaired safely without touching shared code.
- **FR-009**: The final issue update MUST list commands run, live run result, defects fixed, follow-up issues filed, verification commands, and residual risk.

### Key Entities *(include if feature involves data)*

- **Playtest Run**: A disposable execution attempt with commit SHA, run root, seed session, launch commands, lifecycle route, and result.
- **Observation Artifact**: Snapshot, event dump, stdout/stderr log, bridge status, or run note generated during the live pass.
- **Console Defect**: A player-facing problem with severity, trigger command/action, expected behavior, actual behavior, and artifact references.
- **Repair Evidence**: Regression test, source guard, focused command result, full test result, or live rerun evidence proving a fix.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least one live console run is attempted with a disposable session and exact launch metadata recorded.
- **SC-002**: At least 12 distinct player-facing command or action surfaces are observed, or the run records the blocker that prevented reaching that count.
- **SC-003**: 100% of P0/P1 defects found during the run are either fixed before closure or converted into precise GitHub issues before moving to unrelated work.
- **SC-004**: 100% of in-scope code fixes have focused verification evidence, and broad C# verification is run before merge when code changes land.
- **SC-005**: The final #1157 comment allows another developer to reproduce the run route and understand remaining console playability risks without reading hidden local files.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "<focused-filter>"`; full suite before merging code changes.
- **Documentation/contract verification**: Source-guard or documentation coverage tests when prompts, examples, or contracts change; otherwise N/A.
- **Frontend verification**: N/A; browser/frontend work is out of scope.
- **Manual/player-facing verification**: Live Agent Console run with Codex GM bridge; command-output audit; affected steps rerun after fixes.

## Assumptions

- The current machine has a working `codex` CLI and can launch the GM bridge locally.
- `FileSystemExample\game_session` or another repo seed can be copied as the baseline disposable session.
- Agent Console remains the player-observation boundary; direct JSON inspection is allowed only for setup, teardown, and post-failure triage.
- If the live route uncovers broad product decisions rather than narrow defects, those decisions become follow-up issues instead of ad hoc implementation.
