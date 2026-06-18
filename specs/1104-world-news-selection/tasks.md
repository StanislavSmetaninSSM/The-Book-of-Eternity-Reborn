# Tasks: World News Selectable Details

**Input**: `specs/1104-world-news-selection/spec.md`, `specs/1104-world-news-selection/plan.md`, issue [#1104](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1104)

**Prerequisites**: plan.md, spec.md

**Tests**: Behavior change requires test-first coverage.

## Phase 1: Setup

- [x] T001 Confirm source GitHub issue #1104, `git status --short`, and branch `fix/1104-world-news-selection`
- [x] T002 Read `AGENTS.md`, constitution, nearby world-news builder/tests, and current command rendering flow
- [x] T003 Define focused verification commands in `specs/1104-world-news-selection/plan.md`

---

## Phase 2: Foundational

- [x] T004 Confirm no GM-facing state schema/prompt/example changes are required because this is client-owned rendering of existing mortal-world state
- [x] T005 Confirm existing detail commands `/новости_мира событие|флаг|прогресс <selector>` remain canonical detail authority
- [x] T006 Update `AGENTS.md` active Spec Kit plan reference to `specs/1104-world-news-selection/plan.md`

---

## Phase 3: User Story 1 - Compact Summary First (Priority: P1)

**Goal**: `/новости_мира` overview shows a compact summary and selectable actions, not raw JSON or all details at once.

**Independent Test**: Execute overview against rich world-news data and inspect command-result blocks/actions.

### Tests

- [x] T007 [US1] Add RED browser command-service test in `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` asserting `/новости_мира` overview has no `UiRawJsonBlock`, hides full detail section tables, and keeps event/flag/progression actions
- [x] T008 [US1] Add RED console command test in `BookOfEternityClient.Tests/ExplorerModeCommandTests.RivalAndWorld.cs` asserting console overview output omits raw JSON keys/full detail labels while exposing selectable detail commands/actions

### Implementation

- [x] T009 [US1] Update `BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs` so `BuildOverview` renders only summary/empty/warnings and actions, not full event/flag/progression tables or raw state blocks
- [x] T010 [US1] If console overview still does not offer action selection after rendering actions, update `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs` to use the existing action selector flow after rendering the shared command result
- [x] T011 [US1] Run focused RED/GREEN command and record evidence in this tasks file

**Evidence**:
- RED: `dotnet test ... --filter "WorldNewsOverview|WorldNews_ConsoleExposes"` failed because overview still rendered 7 tables and raw `Полная запись` blocks.
- GREEN: same command passed with 2/2 tests after summary-first overview and console selector implementation.

---

## Phase 4: User Story 2 - Detail Drilldowns Stay Readable (Priority: P1)

**Goal**: Existing event/flag/progression detail commands continue to show one readable record at a time.

**Independent Test**: Existing detail tests for event, flag, and progression pass.

### Tests

- [x] T012 [US2] Ensure existing detail tests in `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` and `BookOfEternityClient.Tests/ExplorerModeCommandTests.RivalAndWorld.cs` still assert no raw JSON/debug leakage

### Implementation

- [x] T013 [US2] Preserve existing `BuildDetail` behavior in `BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs` and only adjust if RED tests reveal detail regressions
- [x] T014 [US2] Run focused detail tests and record evidence in this tasks file

**Evidence**:
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "WorldNews" --logger "console;verbosity=minimal"` passed with 7/7 tests.

---

## Phase 5: Verification and Merge

- [x] T015 Run focused verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"` (passed: 665/665)
- [x] T016 Run build: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` (passed: 0 warnings, 0 errors)
- [x] T017 Run `git diff --check` and added-line static scan (passed; only LF/CRLF warnings from Git)
- [x] T018 Review diff against #1104/spec/plan/tasks and fix any Critical/Important issues
- [ ] T019 Commit, push, create PR, merge if clean, and post closure evidence on #1104
