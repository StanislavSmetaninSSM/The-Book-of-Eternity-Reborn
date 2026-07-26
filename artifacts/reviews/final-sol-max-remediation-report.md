# Final Sol/Max Review Remediation

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500

Exact comparison base: `9a1490146b7cecad6101af1f166bde614050a6a3`

Review source: `artifacts/reviews/final-sol-max-findings.md`

## Finding Matrix

### Critical 1: `/incarnate` mutated an unbound session

Remediation:

- The incarnation flow binds one immutable `SessionOperationContext` before
  its first canonical read or staging mutation.
- Incarnation snapshot capture and restore run as generation-bound canonical
  transactions.
- The bound operation is carried through turn processing, and typed
  `SessionReplaced` aborts the old flow.

Evidence:

- Deterministic incarnation/load barriers and lifecycle/source guards pass in
  the `284/284` lifecycle run.

### Important 1: failed worker output remained applyable

Remediation:

- Timeout, cancellation, host failure, nonzero exit, missing exit code, and
  unconfirmed process-tree termination are diagnostic-only.
- `GmWorkerBridgePool` does not import a workspace proposal after a failed
  execution.
- `GmWorkerValidationRepairDelegator` requires confirmed zero exit, stopped
  process state, and confirmed process-tree termination before apply.

Evidence:

- Valid-proposal-before-timeout and valid-proposal-before-nonzero RED
  regressions were observed before the production change.
- The complete bridge/delegator regression run passes `69/69`.

### Important 2: Load retained old runtime identity

Remediation:

- Runtime rebinding after session replacement derives and sets the replacement
  session and turn.
- Response, image, pending-memory, transition, warning, and Explorer transient
  state is reset.
- A replacement without an active game returns to the menu.

Evidence:

- Replacement-with-active-game and replacement-without-active-game tests pass
  in the `284/284` lifecycle/source-guard run.

### Important 3: rollback cleanup could poison later writers

Remediation:

- Successful rollback durably persists `rolledBack=true` before fallible
  transaction-root and active-journal cleanup.
- Recovery treats a retained rolled-back journal as retryable cleanup evidence,
  not an unresolved mutation.

Evidence:

- The root-delete-success/journal-delete-failure fault-injection regression
  passes in the `74/74` filesystem/save-load/session-lease run.

### Important 4: Mortal bootstrap inferred setting-specific mechanics

Remediation:

- Fresh Mortal bootstrap no longer creates skills, items, actors, teachers,
  merchants, capabilities, money, experience, or carrying values from prose or
  genre keywords.
- Only explicit GM-authored `structuredGmAuthority` may require those
  mechanics.
- Contract tests use explicit complete setting-neutral actors rather than
  treating production bootstrap output as fixture authority.

Evidence:

- Setting-neutral bootstrap/source guards pass `53/53`.
- Actor/materialization and NPC command contracts pass `298/298`.
- Normalizer and daemon context-pack regressions pass `316/316`.

### Important 5: worker runtime could alias canonical state

Remediation:

- Configured and derived worker runtime roots are rejected when equal to or
  nested under canonical `game_session`.
- Physical containment checks reject reparse-point aliases into canonical
  state.

Evidence:

- Direct containment and junction-alias tests pass in the `69/69`
  bridge/delegator run.

### Important 6: terminal verify/read TOCTOU

Remediation:

- Complete/error terminal bytes are captured together under one canonical
  lease.
- Final resolution consumes only that immutable snapshot and never rereads a
  ready file after releasing the lease.
- Late-response resolution uses the same snapshot protocol.

Evidence:

- The deterministic replacement-after-snapshot regression and source guard pass
  `2/2`.
- The complete lifecycle/source-guard run passes `284/284`.

## Harness Fixture Remediation

Production `MortalBootstrapStateBuilder` is intentionally actor-free and
setting-neutral. Tests that require an actor now materialize an explicit,
complete `MortalActorTestFixtures` actor. This prevents a test helper from
normalizing the forbidden production behavior back into the codebase.

## GM Contract Synchronization

Updated:

- Mortal GM context-pack guidance for explicit GM-authored active/passive
  skills and prohibition of prose/keyword inference.
- Worker bridge guide and formal contract.
- Validation-repair worked example.
- CLI launcher guidance.
- Validation manifest.
- Shared afterlife contract matrix.
- Documentation and source-guard tests.

No afterlife pending/control file, `actionType`, response field, receipt,
report, scheduler contour, normalizer side effect, or GM-authored gameplay
schema changed. The afterlife matrix was updated because the shared worker
harness applies to afterlife repair execution as well.

## Fresh Verification

- Mandatory afterlife documentation:
  `117/117` passed.
- Documentation and source guards:
  `278/278` passed.
- Broad actor, afterlife, worker, save/load, state-distribution, and lifecycle
  regression filter:
  `1069/1069` passed.
- Normalizer and GM context-pack regression filter:
  `316/316` passed.
- Complete C# project:
  `6415/6415` passed.
- Release build:
  succeeded with `0` warnings and `0` errors.
- PowerShell parser:
  both changed scripts passed.
- `git diff --check`:
  passed; only expected line-ending conversion notices were emitted.

## Spec Kit Analysis

The final read-only consistency pass found no constitution conflict, unresolved
placeholder, uncovered core requirement, or blocking spec/plan/task
contradiction. The artifacts contain `52` functional requirements, `15` success
criteria, and `139` tasks. The remaining historical integration checkboxes form
one inherited review gate and are closed only after the fresh independent
exact-base review below.

## Independent Re-review

Pending fresh independent `gpt-5.6-sol` / `max` exact-base review of the complete
branch diff. Every confirmed Critical or Important finding must be remediated
with focused RED/GREEN evidence before integration.
