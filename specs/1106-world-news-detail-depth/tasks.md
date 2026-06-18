# Tasks: World News Detail Depth

**Input**: `specs/1106-world-news-detail-depth/spec.md`, `specs/1106-world-news-detail-depth/plan.md`, issue [#1106](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1106)

## Phase 1: Setup

- [x] T001 Create GitHub issue #1106 and branch `fix/1106-world-news-detail-depth`
- [x] T002 Update active Spec Kit pointers in `.specify/feature.json` and `AGENTS.md`
- [x] T003 Confirm no GM prompt/schema/doc changes are required because this is client-owned rendering/navigation

## Phase 2: User Story 1 - Rich Detail Fields

**Goal**: Selected world-news detail views show meaningful extra GM-authored fields.

**Independent Test**: Detail command output includes seeded extra scalar, array, and nested object values without raw JSON.

- [x] T004 [US1] Add RED web command-service test in `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- [x] T005 [US1] Implement additional detail-field rendering in `BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs`
- [x] T006 [US1] Run focused world-news detail tests and record evidence

**Evidence**:
- RED: `dotnet test ... --filter "WorldNewsEventDetail|WorldNews_ConsoleSelectionCanReturn"` failed because event detail did not include `Настроение жителей`.
- GREEN: same command passed 2/2 after additional field rendering and console back navigation.
- Review hardening: technical `path`/`file`/`uri`/`url`-like keys are filtered from top-level and nested additional details, while player-facing extra fields remain visible.

## Phase 3: User Story 2 - Console Back Navigation

**Goal**: Console player can return from selected detail to the news selector.

**Independent Test**: Script console choices event -> back -> exit and assert selector appears twice.

- [x] T007 [US2] Add RED console selector/back test in `BookOfEternityClient.Tests/ExplorerModeCommandTests.RivalAndWorld.cs`
- [x] T008 [US2] Implement console overview/detail/back loop in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs`
- [x] T009 [US2] Run focused console world-news tests and record evidence

## Phase 4: Verification and Merge

- [x] T010 Run focused verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"` (passed: 666/666)
- [x] T011 Run build: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` (passed: 0 warnings, 0 errors)
- [x] T012 Run `git diff --check` and added-line raw/debug/path scan (diff-check passed; scan matches only test fixtures/assertions plus the new production skip-list guard)
- [ ] T013 Review diff, commit, push, PR, merge if clean, and update #1106 labels
