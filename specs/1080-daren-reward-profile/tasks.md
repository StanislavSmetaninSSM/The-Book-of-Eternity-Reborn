# Tasks: Daren Reward Profile Presentation

**Input**: Design documents from `specs/1080-daren-reward-profile/`

**Prerequisites**: plan.md, spec.md

**Tests**: Required by project constitution. Write failing tests before production code.

## Phase 1: Setup

- [x] T001 Confirm source GitHub issue #1080, worktree branch, and clean baseline Daren tests
- [x] T002 Read `AGENTS.md`, constitution, source issue acceptance criteria, and nearby Daren reward/profile code
- [x] T003 Define focused verification commands in `specs/1080-daren-reward-profile/plan.md`

## Phase 2: Foundational

- [x] T004 Define canonical authority: C# Daren reward profile service owns player-facing reward/profile summaries
- [x] T005 Confirm GM-facing docs/examples are out of scope because Daren showcase reward profile is client-owned

## Phase 3: User Story 1 - Read the Permanent Daren Reward (Priority: P1)

**Goal**: Player understands saved Daren reward profile and its relationship to the current outcome.

**Independent Test**: Focused C# and frontend tests prove shared profile summary is present and readable for first reward, weaker replay, and browser best reward display.

### Tests

- [x] T006 [US1] Add failing C# tests for shared reward profile summary in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
- [x] T007 [US1] Add failing browser DTO contract fixture expectations in `BookOfEternityClient.Tests/BrowserApiContractTests.cs`
- [x] T008 [US1] Add failing frontend render tests in `BookOfEternityClient.WebFrontend/test/darenShowcase.test.tsx`

### Implementation

- [x] T009 [US1] Add shared player-facing summary derivation in `BookOfEternityClient/Services/DarenQteRewardProfileService.cs`
- [x] T010 [US1] Include profile summary in Daren completion and console rendering in `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- [x] T011 [US1] Project profile summary through browser DTOs in `BookOfEternityClient/WebUi/QteWebInteractionService.cs`
- [x] T012 [US1] Render shared profile summary in `BookOfEternityClient.WebFrontend/src/components/DarenShowcaseView.tsx`

## Phase 4: Verification & Closure

- [x] T013 Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Daren"`
- [x] T014 Run `npm run test:player-facing` from `BookOfEternityClient.WebFrontend/`
- [x] T015 Run `npm run build` from `BookOfEternityClient.WebFrontend/`
- [x] T016 Review diff for no mechanics drift and prepare GitHub issue #1080 closure evidence

## Dependencies & Execution Order

- Phase 1 and Phase 2 are prerequisites.
- T006-T008 must fail before T009-T012 implementation.
- T013-T016 run after implementation.

## Implementation Strategy

Deliver one narrow increment: shared C# summary first, browser projection second, frontend rendering last. Do not change Daren mechanics or persisted profile schema.
