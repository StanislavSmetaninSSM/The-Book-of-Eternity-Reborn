# Feature Specification: GM Workers Live Regression

**Feature Branch**: `work/1189-gm-workers-live-regression`

**Created**: 2026-06-21

**Status**: Draft

**Input**: GitHub issue #1189: integrated live multi-agent regression during console playtest.

## Source Issues & Scope

- **Source GitHub issue(s)**: #1189 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1189
- **Issue type**: E2E / runtime / GM worker hardening task.
- **Spec Kit justification**: Required. This is a live E2E regression over Agent Console, GM bridge, hidden worker launch, proposal inbox diagnostics, validation repair, and GM-facing worker contracts. It spans multiple sessions and must leave durable run evidence for future agents.
- **Contract scope**: runtime-state, validation, GM-facing docs, console, Agent Console, E2E runbook, audit report.
- **Out of scope**: browser client, QTE, Gemini CLI or other deprecated agents, new external worker types, changing worker authority semantics unless a blocker is found and filed separately.

## User Scenarios & Testing

### User Story 1 - Live Delegation Proves Hidden Worker Use (Priority: P1)

A maintainer can run a short console playtest where the main Codex GM uses hidden Codex workers for both narrative/analysis support and validation/repair support without exposing extra worker windows to the player.

**Why this priority**: This is the core risk in #1189. Prior tests covered isolated worker flows; this verifies the integrated play path.

**Independent Test**: Run a disposable console session through Agent Console and GM bridge, enable Codex worker profiles, force or request both delegation types, and preserve worker audit/proposal artifacts.

**Acceptance Scenarios**:

1. **Given** a disposable console session with GM worker profiles enabled, **When** the main GM delegates a narrative draft or analysis task, **Then** a proposal appears in the worker inbox and the player remains in the normal single-GM console flow.
2. **Given** a validation issue during a turn or repair rehearsal, **When** the main GM/runtime delegates validation repair, **Then** a worker proposal is created or a precise failure is recorded without crashing the turn.

### User Story 2 - GM Controls Proposals (Priority: P1)

The main GM can inspect proposals and decide whether to use, modify, accept, or reject worker output; workers never become canonical game authority.

**Why this priority**: Multi-agent architecture is safe only if the main GM remains in control and diagnostics are readable.

**Independent Test**: Inspect proposal inbox diagnostics, audit events, and final GM output after worker proposals are produced.

**Acceptance Scenarios**:

1. **Given** worker proposals exist, **When** proposal diagnostics are inspected, **Then** they show task type, worker id, proposal id, summary/findings/draft text, and any apply decision in a readable form.
2. **Given** a worker proposes a repair, **When** the repair is accepted or rejected, **Then** the audit log records the decision and canonical state changes only through the existing apply/validation gates.

### User Story 3 - Readiness Is Assessed With Follow-Ups (Priority: P2)

A future agent can read the runbook and audit report to know whether GM Workers are ready for regular live E2E use, and any blocker/major issue has a focused GitHub bug with logs.

**Why this priority**: The output of this issue is operational confidence, not only code.

**Independent Test**: Review the committed runbook/report and issue comments after the live run.

**Acceptance Scenarios**:

1. **Given** the live run completes, **When** the report is opened, **Then** it includes launch commands, run root, snapshots/proposals/audit paths, outcomes, timing, and residual risks.
2. **Given** a blocker or major issue is found, **When** the issue is closed or deferred, **Then** a focused bug exists with reproduction steps and artifact locations.

### Edge Cases

- Worker profile disabled or missing: the run must record skip/fallback diagnostics rather than silently claiming delegation worked.
- Codex worker timeout or malformed proposal: the report must distinguish runtime failure from GM decision failure.
- Validation repair may not occur naturally in a short playtest; a controlled repair rehearsal is acceptable if it uses the same worker validation-repair path and records why it was used.
- Worker windows must not be visible to the player. If the main GM bridge requires one visible terminal, the report must distinguish it from hidden subordinate workers.
- Run-generated `game_session`, worker tasks, proposals, logs, and snapshots should remain outside the repository unless a small curated report/runbook is intentionally committed.

## Requirements

### Functional Requirements

- **FR-001**: The work MUST provide a reproducible runbook for integrated live GM Workers testing in a console playthrough.
- **FR-002**: The live evidence MUST demonstrate narrative or analysis delegation and validation-repair delegation in one scenario or two short scenarios.
- **FR-003**: Worker launch MUST be hidden/background from the player perspective; no normal player flow may require managing worker windows.
- **FR-004**: The main GM MUST receive worker proposals through the existing proposal inbox/diagnostics and retain final control over use or repair.
- **FR-005**: The test MUST not reintroduce Gemini CLI guidance or settings; Codex CLI is the supported worker target.
- **FR-006**: The test MUST exclude browser and QTE surfaces.
- **FR-007**: Any blocker or major issue found during the live run MUST become a focused GitHub bug with reproduction steps and artifact paths.
- **FR-008**: The final report MUST include launch commands, run root, session source, worker profiles used, proposal ids, audit files, verification commands, readiness score, and residual risks.

### Key Entities

- **Live Worker Run**: Disposable session, process launch metadata, Agent Console URL, GM bridge status, worker profile settings, and artifact root.
- **Worker Proposal Evidence**: Proposal id, worker id, task id, task type, summary, findings or draft text, changed-file decision if any, and audit status.
- **Readiness Finding**: Severity, reproduction steps, artifact paths, fix status, and follow-up issue if not fixed in this branch.

## Success Criteria

### Measurable Outcomes

- **SC-001**: At least one narrative or analysis worker proposal is produced and referenced in the report.
- **SC-002**: At least one validation-repair worker proposal or controlled validation-repair failure record is produced and referenced in the report.
- **SC-003**: The report confirms whether subordinate worker launches were hidden/background from the player perspective.
- **SC-004**: Focused GM worker/Agent Console verification passes before merge.
- **SC-005**: The final readiness assessment classifies the system as ready, conditionally ready, or not ready for regular live E2E worker use.

## Verification Plan

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "GmWorker|GmBridge|AgentConsole"`
- **Documentation/contract verification**: Run documentation/source-guard tests if docs or examples change beyond the new runbook/report.
- **Frontend verification**: N/A, browser is out of scope.
- **Manual/player-facing verification**: Disposable console + Agent Console + GM bridge + Codex worker live run using `docs/e2e/gm-workers-live-regression-runbook.md`.

## Assumptions

- `codex --dangerously-bypass-approvals-and-sandbox` is available locally for the interactive main GM bridge, and `codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -` is available locally for hidden non-interactive workers.
- Existing worker contracts from `specs/1113-gm-worker-bridges/` and `OtherGuides/GM_Worker_Bridges.md` remain authoritative.
- The final branch may add runbook/report/spec artifacts and only changes runtime code if the live run exposes a focused blocker that can be safely fixed under #1189.
