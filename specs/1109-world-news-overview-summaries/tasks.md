# Tasks: World News Overview Summaries

**Input**: `specs/1109-world-news-overview-summaries/spec.md`, `specs/1109-world-news-overview-summaries/plan.md`, issue [#1109](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1109)

## Phase 1: Setup

- [x] T001 Create GitHub issue #1109 and branch `fix/1109-world-news-overview-summaries`
- [x] T002 Create Spec Kit artifacts and update active pointers in `.specify/feature.json` and `AGENTS.md`
- [x] T003 Confirm no GM prompt/schema/doc changes are required because this is client-owned rendering over existing fields

## Phase 2: User Story 1 - Useful Overview

**Goal**: `/новости_мира` overview shows selectable entries with title and short context.

**Independent Test**: Overview command output includes seeded event/flag/progression titles and summaries while hiding raw/technical fields.

- [x] T004 [US1] Add RED overview DTO test in `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- [x] T005 [US1] Add RED console overview/selection label test in `BookOfEternityClient.Tests/ExplorerModeCommandTests.RivalAndWorld.cs`
- [x] T006 [US1] Implement overview summary rows/action labels in `BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs`
- [x] T007 [US1] Run focused overview tests and record evidence

**Evidence**:
- RED: `dotnet test ... --filter "WorldNewsOverview|WorldNews_ConsoleExposesSharedEventFlagAndProgressionDrilldowns"` failed because overview output did not contain `Беспорядки у Северных ворот`.
- GREEN: `dotnet test ... --filter "WorldNewsOverview|WorldNewsEventDetail|WorldNews_ConsoleExposesSharedEventFlagAndProgressionDrilldowns"` passed 3/3 after overview summary rendering and full nested detail rendering.

## Phase 3: User Story 2 - Full Detail

**Goal**: Selected detail remains maximally detailed and filters technical fields.

**Independent Test**: Detail commands include extra scalar/array/nested values and exclude ids/raw/debug/path/url fields.

- [x] T008 [US2] Strengthen detail tests in `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- [x] T009 [US2] Adjust detail rendering only if tests expose missing meaningful fields
- [x] T010 [US2] Run focused detail tests and record evidence

**Evidence**:
- RED: `dotnet test ... --filter "WorldNewsEventDetail"` failed because `witnessProfile` was rendered as an incomplete object.
- GREEN: focused world-news suite passed 7/7 after nested object rendering was expanded.

## Phase 4: Verification and Merge

- [x] T011 Run focused verification listed in `plan.md`
- [x] T012 Run build and `git diff --check`
- [ ] T013 Review diff, commit, push, PR, merge if clean, and update #1109 labels

**Evidence**:
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true -p:UseSharedCompilation=false --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"` passed 666/666.
- `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- `git diff --check` passed.
