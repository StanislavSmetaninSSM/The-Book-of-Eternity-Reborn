---
description: "Task list for QTE layout-independent key input"
---

# Tasks: QTE Layout-Independent Key Input

**Input**: Design documents from `/specs/920-qte-layout-keys/`

**Prerequisites**: `plan.md`, `spec.md`, `contracts/qte-layout-input.md`, source issue #920.

**Tests**: Behavior changes require test-first tasks. Write the listed failing tests before production implementation and verify RED before GREEN.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm scope, branch, and baseline before implementation.

- [ ] T001 Confirm source GitHub issue #920, current branch `work/920-qte-layout-keys`, `git status --short`, and feature path `specs/920-qte-layout-keys/`.
- [ ] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `spec.md`, `plan.md`, `contracts/qte-layout-input.md`, issue #920 body, and nearby QTE code/tests.
- [ ] T003 Run or record a focused baseline for `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|BrowserApiContractTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests"` and `npm run verify --prefix BookOfEternityClient.WebFrontend`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define the QTE-only normalization/display contract before UI or docs work.

- [ ] T004 [P] Add a C# RED test in `BookOfEternityClient.Tests/QteSceneServiceTests.cs` proving the RU fallback mappings `й->q`, `ц->w`, `у->e`, `ф->a`, `ы->s`, `в->d` and uppercase variants normalize for QTE matching.
- [ ] T005 [P] Add a frontend RED test for a QTE key-normalization helper proving `KeyboardEvent.code` values `KeyQ`, `KeyW`, `KeyE`, `KeyA`, `KeyS`, `KeyD`, and `Space` win over produced characters.
- [ ] T006 [P] Add a frontend RED test proving fallback Cyrillic `key` values normalize when `KeyboardEvent.code` is absent or unsupported.
- [ ] T007 Define/implement the scoped C# QTE key token/display helper only after T004 is RED.
- [ ] T008 Define/implement the scoped frontend QTE key token/display helper only after T005/T006 are RED.

**Checkpoint**: Console/shared and browser helpers exist and are tested without changing ordinary text input paths.

---

## Phase 3: User Story 1 - Console QTE accepts the intended physical key (Priority: P1)

**Goal**: Console/fallback QTE input handles common RU/EN key pairs deterministically.

**Independent Test**: `QteSceneServiceTests` proves fallback character mappings and display labels.

### Tests for User Story 1

- [ ] T009 [US1] Add/extend C# tests for QTE key display labels such as `Q / Й`, `W / Ц`, `E / У`, `A / Ф`, `S / Ы`, `D / В`, and `Space`.
- [ ] T010 [US1] Run the focused `QteSceneServiceTests` and verify the new tests fail for the expected missing helper/display behavior.

### Implementation for User Story 1

- [ ] T011 [US1] Wire the C# QTE key helper into relevant console QTE prompt/comparison surfaces without changing ordinary command/text input behavior.
- [ ] T012 [US1] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests"` and verify the new tests pass.

**Checkpoint**: Console QTE normalization and labels are covered and green.

---

## Phase 4: User Story 2 - Browser QTE prefers physical KeyboardEvent.code (Priority: P1)

**Goal**: Browser QTE input is physical-key based where possible and has RU/EN fallback.

**Independent Test**: Frontend tests prove physical code wins and character fallback works.

### Tests for User Story 2

- [ ] T013 [US2] Add a focused frontend test file for QTE key normalization/keyboard handling under `BookOfEternityClient.WebFrontend/test/`.
- [ ] T014 [US2] Run the focused frontend test command or `npm run verify --prefix BookOfEternityClient.WebFrontend` and verify the new tests fail before implementation.

### Implementation for User Story 2

- [ ] T015 [US2] Wire the frontend QTE key helper into `QteScenePanel` or the nearest QTE-only event handling surface while leaving command/composer text paths untouched.
- [ ] T016 [US2] Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and verify the new tests pass.

**Checkpoint**: Browser QTE key handling is layout-independent and scoped to QTE.

---

## Phase 5: User Story 3 - Prompts and docs explain physical keys (Priority: P2)

**Goal**: Player and GM-facing text describes physical QTE keys and RU/EN support without telling players to switch layouts.

**Independent Test**: Docs/source guards prove relevant QTE guidance and examples mention client-owned layout support.

### Tests for User Story 3

- [ ] T017 [US3] Add/update C# documentation/source-guard tests in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`, `ExampleDocumentationValidationTests.cs`, or nearby QTE docs tests proving the physical-key/RU-EN guidance exists.
- [ ] T018 [US3] Run the focused docs/QTE test filter and verify the new documentation test fails before doc updates.

### Implementation for User Story 3

- [ ] T019 [US3] Update `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and any active QTE task guide entrypoint with concise physical-key/RU-EN guidance.
- [ ] T020 [US3] Update player-facing console/browser copy only if the current implementation exposes prompt/key labels on those surfaces.
- [ ] T021 [US3] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|QteSceneServiceTests|BrowserApiContractTests"` and verify the docs/player-copy tests pass.

**Checkpoint**: QTE authoring docs/examples and player-facing copy are synchronized.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verify compatibility, reconcile Spec Kit artifacts, and prepare PR closure.

- [ ] T022 Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|BrowserApiContractTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests"`.
- [ ] T023 Run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- [ ] T024 Run `git diff --check origin/main...HEAD`.
- [ ] T025 Run added-line static security scan over `origin/main...HEAD`, excluding docs/spec text false positives where appropriate.
- [ ] T026 Reconcile `spec.md`, `plan.md`, `tasks.md`, and `contracts/qte-layout-input.md` with the final diff; mark tasks complete only with implementation and verification evidence.
- [ ] T027 Perform independent review before PR/merge; fix critical/important findings or document technical pushback.
- [ ] T028 Create/update PR for #920, squash-merge after local gates, fetch main, verify PR merged and issue #920 closed.

## Dependencies & Execution Order

- Phase 1 precedes all edits.
- Phase 2 blocks user stories because helpers/tests define scoped behavior.
- User Stories 1 and 2 can be implemented in either order after Phase 2, but both must pass before docs/player-copy closure.
- User Story 3 depends on final helper/display wording.
- Phase 6 runs after all selected user-story tasks are complete.

## Parallel Opportunities

- T004, T005, and T006 can be written in parallel if agents work in separate files.
- Docs tests and frontend tests can be reviewed independently after helper behavior is stable.
