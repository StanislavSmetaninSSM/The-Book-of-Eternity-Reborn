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

## Phase 5: Reopened Follow-up - Score-Based Ending and Readable Final Screen

**Goal**: Remove the hidden hard-failure rule reported from live console play and make the final Daren screen readable.

**Independent Test**: A run with a failed QTE step but high final score resolves to the score-derived reward tier and records a permanent reward; the console completion response is structured instead of a dense single paragraph.

### Tests

- [x] T017 Add failing C# regression coverage for high-score failed-step completion in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
- [x] T018 Add failing C# coverage for structured Daren completion response readability in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`

### Implementation

- [x] T019 Remove the unsafe-route hard failure from Daren ending resolution while preserving score penalties and threshold tiers
- [x] T020 Rewrite shared Daren completion response construction into a concise structured player-facing result

### Verification

- [x] T021 Run focused reopened-issue tests for Daren score-tier and console response readability
- [x] T022 Run full Daren focused test suite
- [x] T023 Run frontend player-facing tests/build if shared response changes affect browser DTO/rendering
- [ ] T024 Review diff, update #1080 with evidence, and merge the branch

## Dependencies & Execution Order

- Phase 1 and Phase 2 are prerequisites.
- T006-T008 must fail before T009-T012 implementation.
- T013-T016 run after implementation.

## Implementation Strategy

Deliver one narrow increment: shared C# summary first, browser projection second, frontend rendering last. Do not change Daren mechanics or persisted profile schema.
