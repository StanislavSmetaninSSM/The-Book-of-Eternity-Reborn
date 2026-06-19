# Feature Specification: Live GM Worker Validation Repair

**Feature Branch**: `1143-live-worker-validation-repair`

**Created**: 2026-06-20

**Status**: Draft

**Source GitHub issue**: #1143 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1143

## Scope

PR #1142 introduced the GM worker bridge foundation: worker profiles, scoped task packets, proposal validation, apply gate, audit log, and fake-worker tests. This feature wires the first live runtime use case: validation-repair worker delegation from real validation failures.

This slice is intentionally narrow. It only covers validation repair. Narrative drafting, analysis tasks, proposal inbox UI, and worker profile editing remain follow-up work unless needed as support plumbing.

## User Story

When a GM turn fails validation and an enabled worker profile can handle validation repair, the runtime dispatches a hidden worker task, receives a proposal, validates it, gates it, and preserves the existing repair fallback if the worker cannot produce an acceptable result.

### Acceptance Scenarios

1. Given no configured validation-repair worker, when validation fails, then the existing legacy repair request behavior remains unchanged.
2. Given an enabled validation-repair worker profile, when validation fails, then the runtime creates a scoped worker task and runs the worker hidden/background through `GmWorkerBridgePool`.
3. Given a worker returns a valid proposal, when the proposal is read, then the runtime validates it and routes it through `GmWorkerApplyGate` before any state changes are accepted.
4. Given a worker times out, exits with failure, or returns malformed JSON, when validation repair continues, then the legacy repair request remains available and the turn does not crash.
5. Given a worker proposal attempts to change files outside the allowed scope, when the apply gate evaluates it, then the proposal is rejected and no forbidden file changes are applied.
6. Given worker dispatch runs, succeeds, fails, or is skipped, then audit/status output records what happened for GM diagnostics.

## Requirements

- **FR-001**: The live validation-repair path MUST preserve current behavior when no matching worker profile is enabled.
- **FR-002**: The runtime MUST choose only enabled worker profiles that allow `ValidationRepair`.
- **FR-003**: Worker launch MUST use the existing hidden/background `GmWorkerBridgePool` launch path.
- **FR-004**: Worker task packets MUST include validation issues, scoped context, allowed files, and expected proposal shape.
- **FR-005**: Worker proposals MUST be parsed and validated before any apply decision.
- **FR-006**: Accepted worker repair proposals MUST pass through `GmWorkerApplyGate`; workers MUST NOT directly mutate canonical state as the supported contract.
- **FR-007**: Worker timeout, non-zero exit, missing proposal, and malformed proposal MUST fall back to existing legacy repair handling.
- **FR-008**: Audit events or status history MUST distinguish skipped, dispatched, succeeded, rejected, timed out, and failed worker outcomes.
- **FR-009**: Tests MUST cover success and fallback paths using deterministic fake workers.
- **FR-010**: Documentation MUST clarify that this slice enables live validation repair only and keeps broader worker task classes as follow-up work.

## Out of Scope

- Proposal inbox/review UI.
- Runtime narrative-draft dispatch.
- Browser client changes.
- Remote/cloud worker orchestration.
- Automatic acceptance of arbitrary worker-authored narrative or analysis.
- User-facing profile editor.

## Verification

Run at minimum:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|ValidationRepair"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```
