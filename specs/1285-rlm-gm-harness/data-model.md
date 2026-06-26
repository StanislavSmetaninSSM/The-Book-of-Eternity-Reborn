# Data Model: RLM-Inspired GM Harness

## GM Trajectory Record

Represents one live GM turn, repair loop, terminal-protocol attempt, or delegated worker step.

Fields:
- `recordId`: stable unique id.
- `sessionId`: current game session id when available.
- `turnId`: turn number or request id.
- `realm`: Mortal World, Chaos Sea, Shining Abode, or unknown.
- `mode`: ordinary turn, validation repair, terminal protocol, worker proposal, rollback.
- `contextPackPath`: session-local context pack reference.
- `templateVersions`: compact template/context versions used by the prompt.
- `dispatch`: bridge dispatch attempts, busy retries, timeouts, and final bridge status.
- `outputs`: output file references and hashes where applicable.
- `validation`: accepted/rejected status, issue ids/kinds, repair packet references.
- `repair`: attempt count, terminal signal status, final repair outcome.
- `workerEvents`: worker task ids, roles, proposal ids, apply/reject decisions.
- `rollbackEvents`: baseline id, restore/pre-rollback result, changed file summary.
- `rubric`: compact success/friction metrics.
- `createdAt`: timestamp.

Validation rules:
- Must not store full giant prompts or secrets.
- Must be readable when a turn is interrupted.
- Must reference detailed logs by path/hash instead of embedding them wholesale.

## Experience Lesson

Derived compact hint from one or more trajectories.

Fields:
- `lessonId`: stable id.
- `sourceRecordIds`: trajectory records used.
- `realm`, `mode`, `issueKinds`, `taskTypes`: matching signals.
- `contractVersion` and `templateVersion`: staleness guard.
- `badPattern`: what went wrong.
- `acceptedFix`: what worked.
- `preferredHarnessSurface`: template, repair packet, safe probe, rollback, worker role, or validator.
- `confidence`: low, medium, high.
- `lastSeenAt`: timestamp.

Validation rules:
- Must be compact enough for the configured context-pack cap.
- Must be excluded or marked stale when contract/template versions no longer match.
- Must not override validators or compact templates.

## Safe GM Probe

Harness-owned read-only context surface for the GM.

Fields:
- `probeId`: stable name.
- `purpose`: what question it answers.
- `input`: allowed parameters or context.
- `outputShape`: compact schema or markdown shape.
- `authority`: source state/files and hashes.
- `limitations`: what it does not prove or mutate.

Validation rules:
- Read-only by default.
- Any write-like operation must route through existing apply, validation, or rollback gate.
- Must avoid implementation source paths as the ordinary route.

## Worker Delegation Record

Represents one hidden/background worker task.

Fields:
- `taskId`: stable worker task id.
- `workerId` and `role`: selected worker profile and role.
- `taskType`: proposal-only narrative/content/analysis/repair type.
- `contextRefs`: read-only context references with hashes.
- `allowedSurfaces`: files, schemas, or proposal fields allowed for this task.
- `proposalId`: resulting proposal id if any.
- `decision`: accepted, rejected, timed out, malformed, skipped.
- `validationResult`: validation/apply outcome when relevant.

Validation rules:
- Workers do not directly mutate canonical game_session files.
- Proposal content refs must stay under the proposal directory.
- Apply decisions must be auditable in the trajectory.

## Live-Test Rubric Finding

Structured note for harness quality.

Fields:
- `findingId`: stable id.
- `recordId`: related trajectory record.
- `category`: success, containment, friction, delegation, experience-memory, missing-tool, follow-up.
- `severity`: info, low, medium, high.
- `observation`: compact note.
- `recommendedHarnessAction`: template, validator, normalizer, rollback, safe probe, worker packet, docs, or no-change rationale.
- `githubRef`: issue or comment link if created.

Validation rules:
- Repeated high-friction findings should create or update tracked issues.
- Notes should focus on harness/tooling improvements, not only prompt wording.
