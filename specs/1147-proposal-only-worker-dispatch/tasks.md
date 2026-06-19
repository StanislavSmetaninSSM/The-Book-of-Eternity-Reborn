# Tasks: Proposal-Only GM Worker Dispatch

**Source GitHub issue**: #1147 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1147

**Spec**: `specs/1147-proposal-only-worker-dispatch/spec.md`

**Plan**: `specs/1147-proposal-only-worker-dispatch/plan.md`

## Phase 1: Context

- [x] T001 Create issue #1147 and branch `1147-proposal-only-worker-dispatch`.
- [x] T002 Inspect existing worker task builders, bridge request models, and dispatch patterns.

## Phase 2: Test-First Coverage

- [x] T003 Add failing tests for successful fake narrative-draft dispatch.
- [x] T004 Add failing tests for successful fake analysis dispatch.
- [x] T005 Add failing tests for no-worker fallback.
- [x] T006 Add failing tests for proposal-only changed-file rejection/no canonical writes.
- [x] T007 Add failing bridge command/source contract tests.

## Phase 3: Implementation

- [x] T008 Implement proposal-only dispatch request/result models and service.
- [x] T009 Use existing routing, task builders, bridge pool, and proposal inbox store.
- [x] T010 Add bridge command support for proposal-only dispatch.
- [x] T011 Ensure file-changing proposal-only output is rejected/reported safely.

## Phase 4: Docs and Verification

- [x] T012 Update GM worker docs/examples for proposal-only dispatch.
- [x] T013 Run focused verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|GmBridgeDiagnostics"`.
- [x] T014 Run full verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore`.
- [x] T015 Run `git diff --check`, inspect status, and reconcile this task list.
