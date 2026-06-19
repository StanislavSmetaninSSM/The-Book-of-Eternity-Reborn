# Tasks: Live GM Worker Validation Repair

**Source GitHub issue**: #1143 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1143

**Spec**: `specs/1143-live-worker-validation-repair/spec.md`

**Plan**: `specs/1143-live-worker-validation-repair/plan.md`

## Phase 1: Context and Baseline

- [x] T001 Create source issue #1143 and branch `1143-live-worker-validation-repair`.
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, and prior worker spec `specs/1113-gm-worker-bridges/`.
- [x] T003 Inspect current validation-repair integration and worker services.
- [x] T004 Run focused existing worker/validation tests as baseline if needed.

## Phase 2: Test-First Coverage

- [x] T005 Add a failing test proving no-worker validation repair keeps legacy behavior unchanged.
- [x] T006 Add a failing test proving an enabled fake validation-repair worker is launched from live validation failure handling.
- [x] T007 Add a failing test proving failed/timed-out/malformed worker output falls back to legacy repair behavior.
- [x] T008 Add a failing test proving accepted worker proposals are routed through `GmWorkerApplyGate` and rejected proposals are not applied.

## Phase 3: Runtime Implementation

- [x] T009 Add a small validation-repair worker delegator or equivalent focused method near `GameEngine.ValidationAndRepair.cs`.
- [x] T010 Select matching enabled `ValidationRepair` worker profiles and skip cleanly when none exist.
- [x] T011 Run `GmWorkerBridgePool.RunTaskAsync` with hidden/background launch behavior already covered by the pool.
- [x] T012 Read and validate worker proposals through existing store/validator services.
- [x] T013 Route proposals through `GmWorkerApplyGate`.
- [x] T014 Preserve legacy repair request fallback on skip, timeout, failure, malformed output, and rejection.
- [x] T015 Record audit/status outcomes for skipped, dispatched, succeeded, failed, timed out, and rejected paths.

## Phase 4: Docs and Verification

- [x] T016 Update `OtherGuides/GM_Worker_Bridges.md` and the validation repair example to clarify that live dispatch currently covers validation repair only.
- [x] T017 Run focused verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|ValidationRepair"`.
- [x] T018 Run full verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore`.
- [x] T019 Run `git diff --check`, inspect status, and reconcile this task list.
