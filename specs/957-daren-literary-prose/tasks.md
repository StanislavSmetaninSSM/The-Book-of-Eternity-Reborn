# Tasks: Daren Literary Scene Prose

**Input**: `specs/957-daren-literary-prose/spec.md`, `plan.md`, `contracts/daren-literary-prose.md`, `checklists/requirements.md`  
**Source Issues**: [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), base [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Phase 0: Hermes Preflight and Spec Kit Setup

- [x] T001 Select #957 as the next logical closure unit after #956 because #956 defined the Daren scene map and #957 is the first child that consumes it. Evidence: #957 is open/triaged, #956 is closed/merged, no open PR or active Codex process was found in preflight.
- [x] T002 Create isolated worktree `E:/Games/worktrees/boe-957-daren-literary-prose` on branch `work/957-daren-literary-prose` from `origin/main`.
- [x] T003 Mark #957 `status: in-progress` and remove `status: triaged` on GitHub.
- [x] T004 Record focused baseline before implementation. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 291/291 on 2026-06-11 before #957 code changes.
- [x] T005 Verify Spec Kit prerequisite helper discovers `specs/957-daren-literary-prose/` after these artifacts are created. Evidence: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-957-daren-literary-prose\\specs\\957-daren-literary-prose` and `AVAILABLE_DOCS=["contracts/","tasks.md"]` on 2026-06-11.

## Phase 1: RED Tests

- [ ] T006 Add a failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that enumerates `QteSceneService.GetDarenShowcaseRoute().Offer.Chapters` and rejects any Daren chapter narrative that is missing, one-sentence/too terse, too long for console, or bare mechanical copy.
- [ ] T007 Add a failing test in `DarenQteShowcaseTests.cs` that checks every Daren action `SuccessText`, `PartialText`, and `FailText` is player-facing transition prose, not empty/terse/debug-like wording, and stays within console-friendly bounds.
- [ ] T008 Add or extend a failing route-copy boundary test that rejects default UI technical terms (`GM`, `DTO`, `API`, `debug`, `Spec Kit`, `manual-grade`, `endpoint`, `client-owned`) in Daren offer, intro, chapter, and action result copy.
- [ ] T009 Keep/extend #956 spine alignment coverage so route beat ids and QTE types still match `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` after prose changes.
- [ ] T010 Run the focused Daren test filter and record RED evidence showing the new tests fail against the existing terse route copy, not because of typos or harness errors.

## Phase 2: GREEN Shared Route Prose

- [ ] T011 Update `BookOfEternityClient/Services/QteSceneService.Daren.cs` so `OfferText` and `IntroNarrative` are player-facing and in-world while preserving the separate-showcase boundary without GM/API/debug wording.
- [ ] T012 Update every `DarenShowcaseBeat.PlayerText` to concise book-like scene prose that includes location/context, stakes, and immediate QTE goal.
- [ ] T013 Update every Daren action `SuccessText`, `PartialText`, and `FailText` to short transition prose that carries tension toward the next beat or final resolution.
- [ ] T014 Preserve route mechanics exactly: beat ids/order, action ids, QTE check types/config, routing, score deltas, ending tiers, reward/profile writes, and New Game grants.
- [ ] T015 Update `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` only as needed for #957 source/handoff evidence while preserving #956 invariants and future #958-#961 links.
- [ ] T016 Rerun focused Daren tests and record GREEN evidence.

## Phase 3: Verification and Reconciliation

- [ ] T017 Run affected QTE/docs/browser contract slice: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests`.
- [ ] T018 Run client and test-project builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- [ ] T019 Run Spec Kit prerequisite helper and verify `FEATURE_DIR` points to `specs/957-daren-literary-prose`.
- [ ] T020 Run `git diff --check origin/main...HEAD` and an added-line static scan. Exclude specs/tests/docs from the security scan only when matches are clearly plan/test placeholder text rather than production code.
- [ ] T021 Update this `tasks.md` with RED/GREEN and verification evidence for completed implementation tasks.

## Phase 4: Hermes-Owned Review, PR, Merge, Closure

- [ ] T022 Independent review validates #957 acceptance, #955/#956/#919 boundaries, shared route prose, no accidental QTE mechanics/reward changes, and no default UI technical wording.
- [ ] T023 Create PR with local-gated verification evidence and safe closing wording for #957.
- [ ] T024 Squash-merge to `main`, verify PR merged and #957 closed/completed, post evidence comment, and clean up worktree/branches.

## Notes for Codex

- Follow TDD strictly: RED tests first, verify failure, then implementation, then GREEN verification.
- Mark T006-T021 complete only after diff and command evidence exist.
- Leave T022-T024 open; Hermes owns independent review, PR, merge, issue closure, and cleanup.
- Do not broaden into #958 dialogue/NPC variants, #959 branch-specific consequence variants, #960 endings/rewards, or #961 broad quality gates.
- If implementation touches React/frontend files, run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact counts.
- If requirements change, update `spec.md`, `plan.md`, and `contracts/daren-literary-prose.md` before final response.
