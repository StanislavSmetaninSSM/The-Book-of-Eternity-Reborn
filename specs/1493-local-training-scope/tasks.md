# Tasks: Local Training And Trade Scope

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

**Input**: `specs/1493-local-training-scope/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/local-interaction-scope.md`

## Phase 1: Setup And Contract

- [x] T001 Confirm issue #1493, clean starting worktree, feature branch, and active Spec Kit directory.
- [x] T002 Read AGENTS, constitution, original training spec, training/trade services, console/browser command paths, and existing tests.
- [x] T003 Define realm-specific local authority and fail-closed behavior in `specs/1493-local-training-scope/contracts/local-interaction-scope.md`.

## Phase 2: Foundational RED Tests

- [x] T004 [P] Add RED Mortal local/remote teacher listing and unresolved-location tests in `BookOfEternityClient.Tests/TrainingServiceTests.cs`.
- [x] T005 [P] Add RED remote purchase no-mutation tests for Mortal and afterlife in `BookOfEternityClient.Tests/TrainingServiceTests.cs`.
- [x] T006 [P] Add RED Chaos Sea active-abode and Shining current-hall mentor tests in `BookOfEternityClient.Tests/TrainingServiceTests.cs`.
- [x] T007 [P] Add RED console/browser named-selector parity tests in `BookOfEternityClient.Tests/ConsoleTrainingCommandTests.cs` and `BookOfEternityClient.Tests/TrainingWebCommandServiceTests.cs`.
- [x] T008 [P] Add trade location regression tests in `BookOfEternityClient.Tests/ConsoleNpcTradeCommandTests.cs`, `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`, and `BookOfEternityClient.Tests/WebUi/BrowserTradeParityTests.cs`.

## Phase 3: User Story 1 - Mortal Local Training (P1)

- [x] T009 [US1] Implement shared Mortal location resolution and matching in `BookOfEternityClient/Services/LocalInteractionScopeService.cs`.
- [x] T010 [US1] Filter Mortal teachers before showcase/request processing in `BookOfEternityClient/Services/TrainingService.cs`.
- [x] T011 [US1] Recheck Mortal teacher locality before purchase and guarantee zero mutation on rejection in `BookOfEternityClient/Services/TrainingService.cs`.
- [x] T012 [US1] Reuse the shared matcher in `BookOfEternityClient/Services/NpcTradeService.cs` without changing existing trade semantics.
- [x] T013 [US1] Run and record focused Mortal RED/GREEN tests.

## Phase 4: User Story 2 - Chaos Sea Local Training (P1)

- [x] T014 [US2] Resolve active Guardian/current abode and explicit non-Guardian abode evidence in `BookOfEternityClient/Services/LocalInteractionScopeService.cs`.
- [x] T015 [US2] Filter Chaos Sea mentors and pending requests in `BookOfEternityClient/Services/TrainingService.cs`.
- [x] T016 [US2] Recheck Chaos mentor locality before purchase and guarantee zero mutation on rejection in `BookOfEternityClient/Services/TrainingService.cs`.
- [x] T017 [US2] Run and record focused Chaos Sea RED/GREEN tests.

## Phase 5: User Story 3 - Shining Abode Local Training (P1)

- [x] T018 [US3] Resolve valid `currentHallId` plus direct/faction/resident/leadership/political actor hall links in `BookOfEternityClient/Services/LocalInteractionScopeService.cs`.
- [x] T019 [US3] Filter Shining mentors from other halls/realms in `BookOfEternityClient/Services/TrainingService.cs` and filter Shining trade factions by current hall in console/browser command paths.
- [x] T020 [US3] Recheck Shining mentor locality before purchase and guarantee zero mutation on rejection in `BookOfEternityClient/Services/TrainingService.cs`.
- [x] T021 [US3] Run and record focused Shining Abode RED/GREEN tests.

## Phase 6: User Story 4 - Console, Browser, And Trade Parity (P2)

- [x] T022 [US4] Ensure useful localized empty/block states in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Training.cs` and `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.Training.cs` only if service results require presentation changes.
- [x] T023 [US4] Verify console and browser expose the same named local targets and hide IDs through existing structured command tests.
- [x] T024 [US4] Verify Mortal, Chaos Sea, and Shining trade regressions and adjust only shared matching code when tests prove a defect.

## Phase 7: GM Contract Synchronization

- [x] T025 [P] Update Mortal and afterlife locality guidance in `TaskGuides/CLI_Step_Main.txt` and `OtherGuides/Afterlife_Contract_Matrix.md`.
- [x] T026 [P] Update worked training examples in `Examples/E_CLI_Training_Showcases.txt`.
- [x] T027 [P] Synchronize launcher guidance in `BookOfEternityClient/Launcher/CLI_Launch_Script.md` and `BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1`.
- [x] T028 Add/update source-guard and documentation coverage tests in `BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs`, `AfterlifeDocumentationCoverageTests.cs`, or `ExampleDocumentationValidationTests.cs`.

## Phase 8: Verification And Integration

- [x] T029 Run focused Training/console/browser/trade tests from `specs/1493-local-training-scope/quickstart.md`.
- [x] T030 Run documentation/source-guard tests from `specs/1493-local-training-scope/quickstart.md`.
- [x] T031 Run `git diff --check`, inspect the complete diff, and request independent code review.
- [x] T032 Smoke-test `/обучение` and `/торговля` through fixture-backed Mortal, Chaos Sea, and Shining console/browser command handlers.
- [x] T033 Comment verification on #1493, merge/push, close the issue, and resume the Golden Path live test.

## Dependencies And Execution Order

- Phase 2 tests block all implementation.
- Mortal scope establishes the shared matcher before Chaos/Shining extensions.
- Console/browser parity follows service behavior and does not duplicate filtering.
- GM docs/examples and documentation guards must land in the same change.

## Independent Test Criteria

- **US1**: two Mortal teachers in different locations produce exactly one local target; a remote purchase changes no files.
- **US2**: two Chaos mentors in different abodes produce exactly the active/current mentor; remote action is rejected.
- **US3**: Shining scope accepts only mentors and visible trade factions resolved to `currentHallId`, excluding other halls and realms.
- **US4**: console/browser named selections match, and all three trade routes retain their local scope.
