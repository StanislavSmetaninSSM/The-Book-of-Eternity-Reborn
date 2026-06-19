# Tasks: Explicit GM Worker Bridges

**Input**: Design documents from `specs/1113-gm-worker-bridges/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/gm-worker-bridge-contract.md`, `quickstart.md`

**Tests**: Behavior changes require test-first implementation. Write focused failing tests before runtime code.

**Organization**: Tasks are grouped by independently testable user story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish working context and traceability.

- [X] T001 Confirm source issue #1141, branch `1113-gm-worker-bridges`, `git status --short`, and active Spec Kit path `specs/1113-gm-worker-bridges/`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `specs/1113-gm-worker-bridges/spec.md`, `specs/1113-gm-worker-bridges/plan.md`, and GitHub issue #1141.
- [X] T003 [P] Read existing GM bridge runtime in `BookOfEternityGMBridge/Program.cs` and current bridge settings in `BookOfEternityClient/Configuration/GameSettings.cs`.
- [ ] T004 [P] Read accepted-turn validation and repair-related tests in `BookOfEternityClient.Tests/AgentConsoleLiveSmokeTests.cs`, `BookOfEternityClient.Tests/GameEngineTurnLifecycleTests.cs`, and validation tests under `BookOfEternityClient.Tests/`.
- [ ] T005 Create an implementation note in the first commit message tying the work to #1141 and the MVP scope: validation repair plus proposal-only narrative drafting.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define shared contracts and safety gates before user stories.

**CRITICAL**: No user story runtime work starts until this phase is complete.

- [X] T006 [P] Add contract/source guard tests for worker profile schema references in `BookOfEternityClient.Tests/GmWorkerBridgeContractTests.cs`.
- [X] T007 [P] Add source guard tests that worker code cannot directly write canonical files outside the apply gate in `BookOfEternityClient.Tests/GmWorkerBridgeSourceGuardTests.cs`.
- [X] T008 [P] Add test fixture helpers for worker task/proposal files and hidden-launch profile fixtures in `BookOfEternityClient.Tests/GmWorkerBridgeTestFixtures.cs`.
- [X] T009 Define C# model records for `WorkerBridgeProfile`, `WorkerScopePolicy`, `WorkerTaskPacket`, `WorkerProposal`, `ApplyGateDecision`, `WorkerBridgeStatus`, and `WorkerAuditEvent` in `BookOfEternityClient/Services/GmWorkers/GmWorkerModels.cs`.
- [X] T010 Implement JSON serialization helpers for worker contracts in `BookOfEternityClient/Services/GmWorkers/GmWorkerJson.cs`.
- [X] T011 Add validation helpers for worker ids, paths, task types, and proposal statuses in `BookOfEternityClient/Services/GmWorkers/GmWorkerContractValidator.cs`.
- [X] T012 Run focused model/contract tests with `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerBridgeContractTests|GmWorkerBridgeSourceGuardTests"`.

**Checkpoint**: Worker contract models and safety source guards exist.

---

## Phase 3: User Story 1 - Delegate Validation Repair Safely (Priority: P1) MVP

**Goal**: Validation errors can be delegated to a worker, returned as a proposal, and applied only through scope and validation gates.

**Independent Test**: A failing validation fixture delegates to a fake worker proposal and ends with accepted/rejected apply decisions according to scope and validation.

### Tests for User Story 1

- [X] T013 [P] [US1] Write failing scope-acceptance test in `BookOfEternityClient.Tests/GmWorkerApplyGateTests.cs` for a proposal changing only allowed files.
- [X] T014 [P] [US1] Write failing scope-rejection test in `BookOfEternityClient.Tests/GmWorkerApplyGateTests.cs` for a proposal changing a forbidden path.
- [X] T015 [P] [US1] Write failing validation-rejection test in `BookOfEternityClient.Tests/GmWorkerApplyGateTests.cs` for an allowed proposal that still fails game validation.
- [X] T016 [P] [US1] Write failing validation-repair dispatch test in `BookOfEternityClient.Tests/GmWorkerValidationRepairTests.cs` that packages validation issues into a `WorkerTaskPacket`.

### Implementation for User Story 1

- [X] T017 [US1] Implement `GmWorkerTaskPacketBuilder` in `BookOfEternityClient/Services/GmWorkers/GmWorkerTaskPacketBuilder.cs`.
- [X] T018 [US1] Implement `GmWorkerProposalStore` in `BookOfEternityClient/Services/GmWorkers/GmWorkerProposalStore.cs`.
- [X] T019 [US1] Implement `GmWorkerApplyGate` in `BookOfEternityClient/Services/GmWorkers/GmWorkerApplyGate.cs`.
- [X] T020 [US1] Integrate validation repair packet creation with validation failure handling in `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`.
- [X] T021 [US1] Ensure accepted worker repairs run existing validation before applying canonical changes in `BookOfEternityClient/Services/GmWorkers/GmWorkerApplyGate.cs`.
- [X] T022 [US1] Add audit events for dispatch, proposal receipt, proposal acceptance, proposal rejection, and validation failure in `BookOfEternityClient/Services/GmWorkers/GmWorkerAuditLog.cs`.
- [X] T023 [US1] Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerApplyGateTests|GmWorkerValidationRepairTests"`.

**Checkpoint**: Validation repair MVP is independently testable without launching real external CLIs.

---

## Phase 4: User Story 2 - Configure and Observe Worker Bridges (Priority: P2)

**Goal**: Users can configure worker profiles, launch enabled workers, and inspect lifecycle diagnostics.

**Independent Test**: Settings with enabled/disabled profiles produce correct worker status without dispatching tasks to disabled or failed workers.

### Tests for User Story 2

- [X] T024 [P] [US2] Write failing settings roundtrip test for worker profiles in `BookOfEternityClient.Tests/StateManagerTests.cs`.
- [X] T025 [P] [US2] Write failing worker lifecycle test in `BookOfEternityClient.Tests/GmWorkerBridgeLifecycleTests.cs` for disabled, starting, ready, failed, busy, and timed-out states.
- [X] T026 [P] [US2] Write failing bridge diagnostics contract test in `BookOfEternityClient.Tests/GmBridgeDiagnosticsContractTests.cs` for worker status visibility and hidden/background launch settings.

### Implementation for User Story 2

- [X] T027 [US2] Add worker profile settings to `BookOfEternityClient/Configuration/GameSettings.cs`.
- [X] T028 [US2] Add worker profile load/save behavior in `BookOfEternityClient/Core/StateManager.cs`.
- [X] T029 [US2] Implement worker lifecycle coordinator with hidden/background process launch defaults in `BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs`.
- [X] T030 [US2] Extend `BookOfEternityGMBridge/Program.cs` to expose worker-ready/worker-failed status without changing main bridge behavior.
- [ ] T031 [US2] Add console advanced diagnostics for worker profiles in `BookOfEternityClient/Core/GameEngine/GameEngine.OptionsAndSettings.cs`.
- [X] T032 [US2] Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "StateManagerTests|GmWorkerBridgeLifecycleTests|GmBridgeDiagnosticsContractTests"`.

**Checkpoint**: Worker profiles and lifecycle diagnostics work without requiring validation repair dispatch.

---

## Phase 5: User Story 3 - Route Proposal-Only Narrative and Analytical Tasks (Priority: P2)

**Goal**: Non-repair tasks can be delegated as proposal-only narrative drafts or analysis without applying file changes.

**Independent Test**: A narrative-draft task returns draft prose, records it in the proposal inbox, and rejects file changes.

### Tests for User Story 3

- [X] T033 [P] [US3] Write failing narrative-draft proposal-only task test in `BookOfEternityClient.Tests/GmWorkerProposalOnlyTests.cs` for `draftText` with no file changes.
- [X] T034 [P] [US3] Write failing proposal-only rejection test in `BookOfEternityClient.Tests/GmWorkerProposalOnlyTests.cs` for a worker response that includes file changes.
- [X] T035 [P] [US3] Write failing role-routing test in `BookOfEternityClient.Tests/GmWorkerTaskRoutingTests.cs` for no suitable worker available.

### Implementation for User Story 3

- [X] T036 [US3] Add proposal-only task types, including `narrative-draft`, to `BookOfEternityClient/Services/GmWorkers/GmWorkerModels.cs`.
- [ ] T037 [US3] Extend `GmWorkerTaskPacketBuilder` in `BookOfEternityClient/Services/GmWorkers/GmWorkerTaskPacketBuilder.cs` to build read-only narrative-draft and analysis packets.
- [X] T038 [US3] Extend `GmWorkerApplyGate` in `BookOfEternityClient/Services/GmWorkers/GmWorkerApplyGate.cs` to reject file changes for proposal-only policies while preserving findings.
- [X] T039 [US3] Add routing failure diagnostics in `BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs`.
- [X] T040 [US3] Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerProposalOnlyTests|GmWorkerTaskRoutingTests"`.

**Checkpoint**: Narrative and analytical worker delegation is safe and non-authoritative.

---

## Phase 6: GM Documentation, Examples, and E2E

**Purpose**: Keep GM-facing contracts synchronized with runtime behavior.

- [X] T041 [P] Update GM-facing guidance in `OtherGuides/` to explain main-GM authority, hidden/background worker delegation, proposal-only narrative drafts, analysis tasks, and validation repair.
- [X] T042 [P] Add `Examples/E_CLI_GM_Worker_Validation_Repair.txt` and `Examples/E_CLI_GM_Worker_Narrative_Draft.txt`, then register them in `Examples/example_validation_manifest.json`.
- [X] T043 [P] Update worker bridge launch guidance in `BookOfEternityClient/Launcher/CLI_Launch_Script.md` and `BookOfEternityClient/game_master_daemon.ps1`.
- [X] T044 Add documentation coverage/source guard tests in `BookOfEternityClient.Tests/` proving worker bridge docs/examples are referenced.
- [X] T045 Add live E2E coverage in `BookOfEternityClient.Tests/GmWorkerLiveSmokeTests.cs` for fake/local worker repair and fake/local narrative draft without external network dependency.
- [X] T046 Run documentation verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|SourceGuard|GmWorker"`.

---

## Phase 7: Polish & Final Verification

**Purpose**: Stabilize the feature before merge.

- [ ] T047 Verify quickstart scenario 1 with automated or scripted evidence: no worker profiles preserve existing single-GM behavior.
- [X] T048 Verify quickstart scenario 2 with automated or scripted evidence: worker repairs a validation failure and audit records acceptance.
- [X] T049 Verify quickstart scenario 3 with automated or scripted evidence: forbidden worker proposal is rejected.
- [X] T050 Verify quickstart scenario 4 with automated or scripted evidence: worker drafts narration, main GM can inspect it, and no canonical files change.
- [X] T051 Verify quickstart scenario 6 with automated or scripted evidence: worker processes use hidden/background launch settings and diagnostics remain available in the main GM/daemon flow.
- [X] T052 Run focused C# verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "WorkerBridge|GmBridge|ValidationRepair|ProposalOnly|AgentConsoleLiveSmokeTests"`.
- [X] T053 Run full C# verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore`.
- [X] T054 Run `git diff --check` and inspect `git status --short`.
- [X] T055 Reconcile `specs/1113-gm-worker-bridges/tasks.md` with completed implementation and verification evidence before final report.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on setup and blocks all user stories.
- **US1 Validation Repair (Phase 3)**: Depends on foundational contracts.
- **US2 Worker Profiles and Lifecycle (Phase 4)**: Depends on foundational contracts; can partly proceed in parallel with US1 after T009-T011.
- **US3 Proposal-Only Narrative and Analysis Tasks (Phase 5)**: Depends on US1 apply gate and US2 routing concepts.
- **Docs/E2E (Phase 6)**: Depends on implemented behavior for accurate examples.
- **Polish (Phase 7)**: Depends on selected user stories and docs.

### MVP Scope

Complete Phases 1-5 plus the required documentation/example tasks from Phase 6. This ships a general worker mechanism proven by validation repair and proposal-only narrative drafting, with safe apply gate and audit trail.

### Parallel Opportunities

- T003-T004 can run in parallel.
- T006-T008 can run in parallel.
- T013-T016 can run in parallel after foundational models are sketched.
- T024-T026 can run in parallel.
- T033-T035 can run in parallel after proposal-only scope is defined.
- T041-T043 can run in parallel once runtime behavior is stable.

## Implementation Strategy

1. Build contract models and source guards first.
2. Ship validation repair through the apply gate.
3. Add worker lifecycle/profile diagnostics.
4. Ship proposal-only narrative drafting in the same MVP wave.
5. Update GM docs/examples and run live E2E.
