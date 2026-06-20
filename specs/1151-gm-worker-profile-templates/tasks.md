# Tasks: GM Worker Profile Templates

**Input**: Design documents from `specs/1151-gm-worker-profile-templates/`

## Phase 1: Setup

- [x] T001 Create source GitHub issue #1151 and branch `1151-gm-worker-profile-templates`.
- [x] T002 Read settings, worker profile contracts, fixtures, and docs.
- [x] T003 Create Spec Kit artifacts for #1151.

## Phase 2: Tests First

- [x] T004 Add failing tests for default templates in `BookOfEternityClient.Tests/GmWorkerProfileTemplateTests.cs`.
- [x] T005 Add failing settings preservation/default tests.
- [x] T006 Run focused tests and confirm failures before implementation.

## Phase 3: Implementation

- [x] T007 Add `GmWorkerBridgeProfileTemplates` catalog with disabled runner-based profiles.
- [x] T008 Update `GameSettings.NormalizeWorkerProfiles` to supply templates only when no profiles are configured.
- [x] T009 Update `GmWorkerBridgeTestFixtures` to use template-derived runner commands.
- [x] T010 Update docs/examples/contracts to describe disabled templates.

## Phase 4: Verification and Merge

- [x] T011 Run focused verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerProfileTemplate|GmWorkerBridgeContract|GmWorkerBridgeDocumentation" -p:BaseOutputPath=TestResults/bin/1151-templates/`.
- [x] T012 Run full verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:BaseOutputPath=TestResults/bin/1151-full/`.
- [ ] T013 Inspect diff, exclude `BookOfEternityClient/client_profile/`, commit, open PR, and merge if green.
