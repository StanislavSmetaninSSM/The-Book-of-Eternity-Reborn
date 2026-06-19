# Feature Specification: Proposal-Only GM Worker Dispatch

**Feature Branch**: `1147-proposal-only-worker-dispatch`

**Created**: 2026-06-20

**Status**: Draft

**Source GitHub issue**: #1147 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1147

## Scope

The worker bridge can run validation repair and the main GM can inspect proposal inbox entries. This feature adds the first GM-facing dispatch path for proposal-only worker tasks: narrative drafting and analysis.

The feature remains read-only/proposal-only. It must not add a player-facing command, browser UI, or manual apply flow.

## Requirements

- **FR-001**: The system MUST support dispatching `NarrativeDraft` and `Analysis` worker tasks through a GM-facing service/bridge command.
- **FR-002**: Dispatch MUST select only enabled matching worker profiles.
- **FR-003**: Dispatch MUST build scoped task packets with sanitized read-only context file references.
- **FR-004**: Dispatch MUST launch workers hidden/background through `GmWorkerBridgePool`.
- **FR-005**: Returned proposals MUST be stored in the proposal inbox.
- **FR-006**: Proposal-only dispatch MUST NOT allow canonical writes or apply proposal-only drafts.
- **FR-007**: A proposal-only worker that returns changed files MUST be rejected or reported as failed without changing canonical files.
- **FR-008**: No-worker, invalid-request, worker-failure, and malformed-proposal cases MUST return compact diagnostics instead of crashing.
- **FR-009**: Tests MUST cover successful fake narrative dispatch, successful fake analysis dispatch, no-worker fallback, and changed-file rejection.

## Out of Scope

- Manual approval/apply UI.
- Browser UI.
- Validation-repair changes.
- Remote/cloud workers.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|GmBridgeDiagnostics"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```
