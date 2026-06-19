# Data Model: Explicit GM Worker Bridges

## WorkerBridgeProfile

Represents a user-configured local worker agent.

- `workerId`: Stable id, unique across profiles.
- `displayName`: Human-readable name.
- `launchCommand`: Local CLI command to start the worker bridge.
- `role`: Worker purpose, for example `validation-repair`, `lore-review`, `npc-analysis`, `qte-design`, or `console-output-audit`.
- `enabled`: Whether the profile may be launched.
- `permissions`: WorkerScopePolicy reference or inline allowed task/file policy.
- `timeoutSeconds`: Maximum time for startup and per-task response.
- `maxConcurrentTasks`: MVP should default to one.

Validation rules:

- `workerId`, `displayName`, `launchCommand`, and `role` are required.
- Disabled profiles must not launch.
- Unknown roles are allowed only if their scope policy is explicit.

## WorkerScopePolicy

Defines what a worker may read and what it may propose changing.

- `taskTypes`: Allowed task type list.
- `readPaths`: File/path patterns the task packet may include.
- `proposalWritePaths`: File/path patterns the worker proposal may modify.
- `proposalOnly`: If true, file changes are rejected even when present.
- `requiresValidation`: Whether apply gate must run game validation before acceptance.

Validation rules:

- MVP validation repair policies must set `requiresValidation = true`.
- Proposal-only policies must reject patch application.
- `game_state/control/` writes require explicit task-specific permission.

## WorkerTaskPacket

Scoped request sent to a worker.

- `taskId`: Unique task id.
- `workerId`: Target worker profile id.
- `taskType`: For MVP, `validation-repair`.
- `createdAtUtc`: Dispatch time.
- `sourceTurn`: Optional turn/session metadata.
- `validationIssues`: Validation issues to repair.
- `contextFiles`: Read-only snapshot references and hashes.
- `allowedProposalPaths`: Paths the worker may propose changing.
- `responseContract`: Expected proposal schema version.
- `instructions`: GM-facing task instructions.

Validation rules:

- `taskId`, `taskType`, `createdAtUtc`, and `responseContract` are required.
- `allowedProposalPaths` must be non-empty for patch-capable tasks.
- Context file hashes must be recorded for auditability.

## WorkerProposal

Worker response to a task packet.

- `proposalId`: Unique proposal id.
- `taskId`: Source task id.
- `workerId`: Worker profile id.
- `status`: `completed`, `declined`, `failed`, or `needs-review`.
- `summary`: Human-readable summary.
- `changedFiles`: List of proposed file changes with path, hash before/after, and patch or replacement reference.
- `findings`: Optional analysis-only notes.
- `selfCheck`: Worker-reported checks and caveats.
- `createdAtUtc`: Proposal time.

Validation rules:

- Proposal `taskId` and `workerId` must match an existing dispatched task.
- `changedFiles` outside `allowedProposalPaths` must be rejected.
- Malformed proposals are rejected and audited.

## ApplyGateDecision

Result of evaluating a proposal.

- `decisionId`: Unique decision id.
- `proposalId`: Source proposal id.
- `result`: `accepted`, `rejected`, `needs-main-gm-review`, or `validation-failed`.
- `scopeCheck`: Pass/fail and details.
- `validationCheck`: Pass/fail and command/evidence where applicable.
- `appliedFiles`: Files applied to canonical state.
- `rejectionReasons`: Player/GM-readable reasons.
- `decidedAtUtc`: Decision time.

Validation rules:

- Accepted decisions must have passed scope checks.
- Repair proposals with `requiresValidation = true` must pass validation before acceptance.

## WorkerBridgeStatus

Runtime status for a worker profile.

- `workerId`
- `state`: `stopped`, `starting`, `ready`, `busy`, `failed`, `timed-out`, `disabled`
- `lastHeartbeatUtc`
- `currentTaskId`
- `lastError`
- `launchCommandDisplay`

Validation rules:

- Disabled workers remain `disabled`.
- Busy workers must name `currentTaskId`.

## WorkerAuditEvent

Durable event stream for worker orchestration.

- `eventId`
- `eventType`: `profile-loaded`, `worker-started`, `worker-ready`, `task-dispatched`, `proposal-received`, `proposal-applied`, `proposal-rejected`, `worker-failed`, `worker-timeout`
- `workerId`
- `taskId`
- `proposalId`
- `timestampUtc`
- `summary`
- `details`

Validation rules:

- Every dispatch must have a corresponding terminal event.
- Every apply gate decision must be auditable.
