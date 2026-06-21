# Tasks: Browser Console Command Parity Audit

**Input**: Design documents from `specs/1119-browser-console-command-parity-audit/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: A focused source guard is required before the audit document is completed.

**Organization**: Tasks are grouped by user story to enable independent implementation and verification.

## Phase 1: Setup

- [x] T001 Confirm source GitHub issues #1119/#1118, `git status --short`, and active worktree branch.
- [x] T002 Read `AGENTS.md`, constitution, source issue acceptance criteria, and relevant existing audits.
- [x] T003 Create Spec Kit artifacts and update active feature pointer.

---

## Phase 2: Foundational

- [x] T004 Extract browser command coverage metadata from `BrowserCommandCoverageService` / fixture.
- [x] T005 Add a failing audit source guard in `BookOfEternityClient.Tests/` that checks command IDs are present in the audit.
- [x] T006 Confirm current state of follow-up issues #1120-#1126 for ordering/status.

---

## Phase 3: User Story 1 - Every Command Is Classified (Priority: P1)

**Goal**: Every command ID from browser coverage appears in the audit.

**Independent Test**: The source guard fails before the document exists and passes after the audit includes all command IDs.

- [x] T007 [US1] Generate command inventory rows from browser coverage metadata.
- [x] T008 [US1] Create `docs/audits/browser-console-command-parity-audit.md` with a row for each command ID.
- [x] T009 [US1] Run the source guard and focused BrowserCommandCoverage tests.

---

## Phase 4: User Story 2 - Gaps Have Severity And Owners (Priority: P2)

**Goal**: Every gap is actionable or explicitly out of scope.

**Independent Test**: Manual audit review confirms no non-adequate row lacks priority and follow-up/no-fix reason.

- [x] T010 [US2] Classify raw JSON dependency, drill-down status, missing browser details, and priority for each row.
- [x] T011 [US2] Link follow-up issues or explicit no-fix reasons for every non-adequate group.

---

## Phase 5: User Story 3 - Execution Order Is Clear (Priority: P3)

**Goal**: The audit states current status/order for #1120-#1126.

**Independent Test**: Summary names #1121 through #1126 and tells future agents what to do first.

- [x] T012 [US3] Add #1120-#1126 order/status summary to the audit.
- [x] T013 [US3] Reconcile `tasks.md` checkboxes with implemented evidence.

---

## Final Phase: Verification

- [x] T014 Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter BrowserCommandCoverage --logger "console;verbosity=minimal"`.
- [x] T015 Run `git diff --check`.
- [x] T016 Review diff against #1119 acceptance criteria before PR/merge.

## Dependencies & Execution Order

- Phase 1 before all other phases.
- Phase 2 before audit completion.
- US1 before US2 and US3.
- US2 and US3 can proceed after US1 inventory exists.

## Implementation Strategy

1. Establish the source guard first.
2. Fill the audit inventory.
3. Add gap classification and follow-up links.
4. Verify and merge before moving to the next browser task.
