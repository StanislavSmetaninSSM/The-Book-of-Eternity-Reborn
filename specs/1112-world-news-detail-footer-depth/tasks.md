# Tasks: World News Detail Footer Depth

**Input**: `specs/1112-world-news-detail-footer-depth/spec.md`, `specs/1112-world-news-detail-footer-depth/plan.md`, issue [#1112](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1112)

## Phase 1: Setup

- [x] T001 Create GitHub issue #1112 and branch `fix/1112-world-news-detail-footer-depth`
- [x] T002 Create Spec Kit artifacts and update active pointers in `.specify/feature.json` and `AGENTS.md`
- [x] T003 Confirm no GM prompt/schema/doc changes are required because this is client-owned rendering and local test data

## Phase 2: Detail Depth

- [x] T004 Add RED detail-depth test for Valmont-style event in `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
  - RED evidence: `WorldNewsValmontEventDetail` failed before the fix because the detail lacked `Печать` / useful seeded fields.
- [x] T005 Implement detail rendering/data seeding changes in `BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs` only if RED exposes missing rendering
  - Removed technical `Метка` rows from event/flag/progression detail panels and added readable labels for Valmont-style fields.
- [x] T006 Enrich ignored local `BookOfEternityClient/game_session/game_state/world/world_events.json` for manual inspection
  - Added seal details, leads, stakes, related people, factions, and consequences to the local test world events.
- [x] T007 Run focused detail tests and record evidence
  - GREEN evidence: `dotnet test ... --filter "WorldNewsValmontEventDetail|WorldNews_ConsoleSelectionRendersSelectedDetail"` passed: 2/2.

## Phase 3: Console Footer

- [x] T008 Add RED console test proving interactive detail does not render the footer text in `BookOfEternityClient.Tests/ExplorerModeCommandTests.RivalAndWorld.cs`
  - RED evidence: console selection test failed while the shared detail still emitted `Вернуться к сводке можно командой`.
- [x] T009 Suppress confusing interactive footer in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs` or shared DTO if appropriate
  - Suppressed the footer in the shared world news detail DTO; back navigation remains available through `world-news-back`.
- [x] T010 Run focused console tests and record evidence
  - GREEN evidence: `dotnet test ... --filter "WorldNewsValmontEventDetail|WorldNews_ConsoleSelectionRendersSelectedDetail"` passed: 2/2.

## Phase 4: Verification and Merge

- [x] T011 Run focused and broader verification listed in `plan.md`
  - `dotnet test ... --filter "WorldNews"` passed: 10/10.
  - `dotnet test ... --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests"` passed: 667/667.
- [x] T012 Run build and `git diff --check`
  - `dotnet build BookOfEternityClient\BookOfEternityClient.csproj ...` passed with 0 warnings / 0 errors.
  - `git diff --check` passed.
- [ ] T013 Review diff, commit, push, PR, merge if clean, and update #1112 labels

## Phase 5: Follow-up Polish After User Review

- [x] T014 Reopen #1112 after user reported the detail screen still had ugly data, English labels, and a confusing bottom command/action hint
- [x] T015 Add RED tests for localized rich fields and direct console detail without an actions table
  - RED evidence: `WorldNewsValmontEventDetail|WorldNews_DirectDetailSuppressesCommandActionTable` failed on missing `Возможность` and on rendered `Доступные действия`.
- [x] T016 Suppress console world-news action tables and localize additional known world-news fields
  - Console `/новости_мира` now renders shared blocks without generic actions; browser/shared DTO actions remain intact.
  - Nested world-news objects/arrays render as readable lines instead of a semicolon blob.
- [x] T017 Enrich ignored local `game_session` event data with `opportunity`, `openQuestions`, and `playerKnowledge`
- [x] T018 Run focused and broader verification for the polish branch
  - RED evidence before production changes: focused filter failed on missing `Возможность` and rendered `Доступные действия`.
  - GREEN evidence: `dotnet test ... --filter "WorldNewsValmontEventDetail|WorldNews_DirectDetailSuppressesCommandActionTable"` passed: 2/2.
  - `dotnet test ... --filter "WorldNews"` passed: 11/11.
  - `dotnet test ... --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests"` passed: 668/668.
  - `dotnet build BookOfEternityClient\BookOfEternityClient.csproj ...` passed with 0 warnings / 0 errors.
  - `git diff --check` passed.
- [ ] T019 Commit, push, PR, merge if clean, and update #1112 labels again
