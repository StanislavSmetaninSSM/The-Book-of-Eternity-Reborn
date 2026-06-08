# Tasks: Saref Main Story E2E Audit

**Input**: Design documents from `/specs/692-saref-main-story-e2e-audit/`

**Prerequisites**: `plan.md`, `spec.md`, `checklists/requirements.md`, `contracts/saref-main-story-audit.md`, source GitHub issue #692

**Tests**: Behavior changes and audit guards must be test-first. Each story starts with RED tests/source guards before runtime/docs/example edits.

**Organization**: Tasks are grouped by user story so each story can be implemented and verified independently. Source issue: #692 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/692

## Phase 1: Setup and Baseline

**Purpose**: Confirm the closure unit, dependencies, and starting verification evidence.

- [ ] T001 Confirm source issue #692 is open, branch is `codex/692-saref-e2e-audit`, and worktree status is clean apart from intentional Spec Kit artifacts.
- [ ] T002 Confirm Agent Console prerequisite issues #749, #750, #751, #752, and #753 are closed before returning to Saref work.
- [ ] T003 Read `AGENTS.md`, `.specify/memory/constitution.md`, `spec.md`, `plan.md`, `contracts/saref-main-story-audit.md`, and issue #692 acceptance criteria.
- [ ] T004 Read the existing Saref runtime/docs/test sources listed in `plan.md` and note existing coverage before adding tests.
- [ ] T005 Record focused baseline evidence in the implementation log or final PR body. Fresh baseline from this worktree: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SarefMainStory|FullyQualifiedName~CanonicalStateNormalizerTests.SarefMainStory|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` => `166 passed, 0 failed, 0 skipped`.

---

## Phase 2: Foundational Audit Contract

**Purpose**: Establish the audit boundary and stage map before implementing user stories.

- [ ] T006 Create or update a human-readable Saref stage matrix in an appropriate tracked doc or test fixture note. It must cover `unknown`, `shadow`, `name_revealed`, `wings_revealed`, `infiltration_active`, `confrontation_available`, `completed`, `oathbound_to_saref`, defeat outcomes, and oath-break routes.
- [ ] T007 Identify the minimal deterministic fixture strategy: extend existing builders/tests if available; otherwise add focused fixture helpers in `BookOfEternityClient.Tests` without changing production APIs for test-only convenience.
- [ ] T008 Identify all GM-facing docs/examples/manifests that must change if the audit clarifies authoring expectations: `OtherGuides/Afterlife_Contract_Matrix.md`, Saref guides, `Examples/E_CLI_Afterlife_Turns.txt`, and `Examples/example_validation_manifest.json`.
- [ ] T009 Define follow-up issue policy in the PR body: large new mechanics, missing true keyboard E2E harness, or separate browser parity gaps get their own GitHub issue instead of broadening #692.

**Checkpoint**: The stage matrix and fixture strategy are clear enough for TDD implementation.

---

## Phase 3: User Story 1 - Stage matrix and deterministic state fixtures (Priority: P1)

**Goal**: Prove valid and invalid Saref stages are auditable through deterministic validation/normalizer coverage.

**Independent Test**: Focused Saref validation/normalizer/docs filter passes with non-zero counts and includes new stage/negative coverage.

### Tests for User Story 1 (RED first)

- [ ] T010 [US1] Add a failing test or source guard in `BookOfEternityClient.Tests` that expects the stage matrix/audit artifact to name every #692 stage and its required player/GM/validation surfaces. Run the focused filter and confirm it fails for the missing matrix or missing stage names.
- [ ] T011 [US1] Add failing validation tests for valid stage fixtures and negative variants: early reveal, advantage without quest-4 Memory proof, Wings request without route proof, final before `confrontation_available`, deal without oath/post-story agenda, broken oath without proof, and defeat without required audit. Confirm failures are due to missing coverage or invalid behavior, not typos.

### Implementation for User Story 1

- [ ] T012 [US1] Implement the stage matrix and fixture helper updates needed for T010/T011 without adding unrelated mechanics.
- [ ] T013 [US1] If a validation gap is small and directly contradicts #692 acceptance, fix the root cause with the new RED test as the regression guard. If the gap is broad, create a follow-up issue and record it in the matrix.
- [ ] T014 [US1] Run the focused Saref filter and record pass counts for US1 evidence.

**Checkpoint**: Valid/invalid Saref stages are documented and mechanically covered or explicitly tracked as follow-ups.

---

## Phase 4: User Story 2 - Anti-spoiler commands and Memory layer (Priority: P1)

**Goal**: Verify `/сареф`, `/сареф найти_крылья`, and `/воспоминание` protect hidden-story progression and use Memory-scene proof correctly.

**Independent Test**: Focused Explorer command/Memory validation tests pass and demonstrate no spoiler leakage or illegal quest-4 authority.

### Tests for User Story 2 (RED first)

- [ ] T015 [US2] Add failing command-result tests for `/сареф` in `unknown`, `shadow`, `name_revealed`, and pre-`wings_revealed` states. Assertions must check player-facing non-spoiler copy and absence of raw stage names, JSON paths, and hidden Wings/final details.
- [ ] T016 [US2] Add failing command-result or service tests for `/сареф найти_крылья`: Chaos Sea block, Shining Abode without route block, valid route creates `pending_saref_wings_infiltration.json`, and active pending prevents duplicate creation.
- [ ] T017 [US2] Add failing validation/normalizer tests proving quest 4 requires `memorySceneProof`, uses `memoryScene.layer="Воспоминание"`, and does not rely on Memory Gates, `pendingMemoryLegacy`, or physical mortal-item transfer.

### Implementation for User Story 2

- [ ] T018 [US2] Fix only direct anti-spoiler, realm guard, duplicate pending, or Memory-proof root causes exposed by T015-T017.
- [ ] T019 [US2] Update GM-facing docs/examples/source guards if Memory-scene authoring expectations are clarified.
- [ ] T020 [US2] Run the focused command/validation/docs filters and record pass counts for US2 evidence.

**Checkpoint**: Player command and quest-4 Memory behavior is spoiler-safe and verified or linked to follow-ups.

---

## Phase 5: User Story 3 - Wings, final, deal, defeat, and oath-break lifecycle (Priority: P2)

**Goal**: Verify advanced branch lifecycle behavior without inconsistent pending cleanup, terminal state, or missing proof.

**Independent Test**: Lifecycle validation/normalizer tests pass for Wings closure modes, final routes, deal/post-story, defeat mitigation, and oath-break routes.

### Tests for User Story 3 (RED first)

- [ ] T021 [US3] Add failing lifecycle tests for Wings request creation and matching accepted closure cleanup across `reveal_wings`, `refuse_wings`, and `block_wings`; include risky/desperate route disadvantage requirements and incompatible pending blocking.
- [ ] T022 [US3] Add failing validation/normalizer tests for final branches: `combat`, `political`, `oath_law`, `metaphysical`, `hybrid`, and `deal`; assert required rewards/effects or explicit follow-up issue references.
- [ ] T023 [US3] Add failing validation/normalizer tests for deal/post-story agenda assignments, defeat outcomes and mitigation, and oath-break routes/consequences.

### Implementation for User Story 3

- [ ] T024 [US3] Fix direct lifecycle root causes exposed by T021-T023 when they are bounded #692 audit gaps.
- [ ] T025 [US3] Create separate GitHub follow-up issues for any final/deal/defeat/oath-break mechanics that require broad new design or contract expansion.
- [ ] T026 [US3] Run the focused Saref lifecycle filters and record pass counts for US3 evidence.

**Checkpoint**: Late-branch Saref lifecycle behavior is covered by tests/fixtures/examples or explicitly tracked as follow-up work.

---

## Phase 6: User Story 4 - GM-facing docs/examples and closure report (Priority: P3)

**Goal**: Make the audit usable for a GM and close #692 only with verified evidence and tracked residual risks.

**Independent Test**: Documentation coverage tests pass and final issue/PR evidence lists follow-up issues.

### Tests for User Story 4 (RED first)

- [ ] T027 [US4] Add or update documentation coverage/source-guard tests requiring worked examples for quest-4 Memory scene, Wings search/closure, final confrontation, deal/post-story, defeat, and oath break. Confirm the guard fails before docs/examples are updated.
- [ ] T028 [US4] Add or update example validation manifest coverage for any new/changed `Examples/E_CLI_Afterlife_Turns.txt` fragments. Confirm malformed or missing fragments fail validation.

### Implementation for User Story 4

- [ ] T029 [US4] Update `OtherGuides/`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, and docs/source guards to satisfy T027/T028.
- [ ] T030 [US4] Ensure follow-up issues created during T013/T025 are linked in the audit doc, PR body, and final issue comment.
- [ ] T031 [US4] Run documentation coverage and example validation tests and record pass counts for US4 evidence.

**Checkpoint**: GM-facing documentation and examples are synchronized with any audited behavior changes.

---

## Phase 7: Final Verification, Review, PR, Merge, Closure

**Purpose**: Complete the autonomous closure unit with local gates and independent review.

- [ ] T032 Run the full focused gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SarefMainStory|FullyQualifiedName~CanonicalStateNormalizerTests.SarefMainStory|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
- [ ] T033 Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal`.
- [ ] T034 Run `git diff --check origin/main...HEAD`.
- [ ] T035 Run the added-line static security scan excluding `specs/**` and docs plan recipes; record `NO_MATCHES` or inspect any benign docs placeholders.
- [ ] T036 Check for accidental run artifacts (`final.md`, `events.jsonl`, `stderr.log`, `exit-code.txt`, `prompt.md`, `run-codex.sh`) in the repository diff.
- [ ] T037 Reconcile `spec.md`, `plan.md`, `tasks.md`, `checklists/requirements.md`, and `contracts/saref-main-story-audit.md` against implemented diff and verification evidence.
- [ ] T038 Obtain independent code/spec review; fix Critical/Important findings before PR/merge.
- [ ] T039 Create PR for `codex/692-saref-e2e-audit` with local verification evidence and safe closing reference `Closes #692.`.
- [ ] T040 Squash-merge after local gates and independent review, delete branch, fetch/fast-forward `main`, verify PR `MERGED` and issue #692 `CLOSED`/`COMPLETED`.
- [ ] T041 Add final issue comment or PR body evidence listing verified paths, commands/counts, review result, docs impact, Spec Kit reconciliation, GitHub Actions status, and next target.

## Dependencies & Execution Order

- Phase 1 must complete before edits beyond Spec Kit artifacts.
- Phase 2 blocks all user stories because stage boundaries define the audit.
- User Stories 1 and 2 are P1 and should complete before late-branch lifecycle work.
- User Story 3 depends on stage and anti-spoiler foundations.
- User Story 4 depends on whichever runtime/docs expectations changed in prior stories.
- Phase 7 runs only after all implemented stories have fresh local evidence.

## Parallel Opportunities

- Reading existing docs/tests can happen in parallel with source inspection.
- Documentation/source-guard updates can be split from runtime validation changes only after the stage matrix is stable.
- Independent review must happen after the implementation diff is coherent; do not run code quality review before spec compliance.
