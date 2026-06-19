# Tasks: GM Worker Proposal Inbox Diagnostics

**Source GitHub issue**: #1145 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1145

**Spec**: `specs/1145-gm-worker-proposal-inbox/spec.md`

**Plan**: `specs/1145-gm-worker-proposal-inbox/plan.md`

## Phase 1: Context

- [x] T001 Create issue #1145 and branch `1145-gm-worker-proposal-inbox`.
- [x] T002 Inspect existing proposal store, audit log, GM bridge diagnostics, and console options diagnostics.

## Phase 2: Test-First Coverage

- [x] T003 Add failing tests for proposal inbox listing with readable proposals.
- [x] T004 Add failing tests for malformed proposal files producing unreadable entries.
- [x] T005 Add failing tests for audit/apply-state joining.
- [x] T006 Add failing tests for diagnostics rendering/serialization.

## Phase 3: Implementation

- [x] T007 Implement read-only proposal inbox models/service.
- [x] T008 Join proposal entries with related audit events.
- [x] T009 Surface proposal inbox summary/details in GM worker diagnostics.
- [x] T010 Keep proposal-only entries review-only and avoid new apply paths.

## Phase 4: Docs and Verification

- [x] T011 Update GM worker docs/examples for proposal inbox review flow.
- [x] T012 Run focused verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|GmBridgeDiagnostics"`.
- [x] T013 Run full verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore`.
- [x] T014 Run `git diff --check`, inspect status, and reconcile this task list.
