# Tasks: Daren Narrative Spine and Scene Map

**Input**: `specs/956-daren-narrative-spine/spec.md`, `plan.md`, `contracts/daren-narrative-spine.md`
**Source Issues**: [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), related [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Phase 0: Hermes Preflight

- [x] T001 Record focused baseline for existing Daren/QTE route tests before implementation. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 287/287 on 2026-06-11 before implementation.
- [x] T002 Verify Spec Kit prerequisite helper discovers `specs/956-daren-narrative-spine/`. Evidence: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-956-daren-narrative-spine\\specs\\956-daren-narrative-spine` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.

## Phase 1: RED Tests

- [x] T003 Add a failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that loads `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` and asserts it exists with route id `daren_qte_showcase` and source issues #956/#955/#919. Evidence: `DarenNarrativeSpine_ExistsAndDeclaresRouteIssuesAndPlaytime` added; RED run below failed on the missing artifact path.
- [x] T004 Add a failing test that compares scene-map beat ids and QTE types to `QteSceneService.GetDarenShowcaseRoute()`. Evidence: `DarenNarrativeSpine_BeatsMatchSharedRouteOrderAndQteTypes` added; RED run below failed before the artifact existed.
- [x] T005 Add a failing test that rejects scene-map beats missing phase, dramatic purpose, player goal, scene framing, branch points, consequence hooks, carry-forward notes, or positive pacing. Evidence: `DarenNarrativeSpine_BeatsDeclareNarrativeStructureAndConsequences` added; RED run below failed before the artifact existed.
- [x] T006 Add a failing test that verifies the map covers preparation, approach, infiltration, reconnaissance, security, complication, theft, alarm, chase, hideout, and epilogue stages plus the required future NPC/cast slots. Evidence: `DarenNarrativeSpine_CoversArcCastHandoffAndSharedRuntimeBoundary` added; RED run below failed before the artifact existed.
- [x] T007 Run the focused Daren test filter and record expected RED evidence. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenNarrativeSpine" --logger "console;verbosity=minimal"` failed 4/4 on 2026-06-11 because `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` was missing.

## Phase 2: GREEN Artifact

- [x] T008 Create `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` with all current Daren route beats in order. Evidence: JSON artifact added with the 12 current beat ids from `QteSceneService.GetDarenShowcaseRoute()`.
- [x] T009 Fill every beat with a concise narrative role: phase, dramatic purpose, player goal, QTE mechanic, scene framing, branch points, consequence hooks, carry-forward, future issue links, and pacing. Evidence: focused GREEN run below passed the structural field guard for every beat.
- [x] T010 Include target playtime 20-30 minutes, arc-stage declarations, cast slots, and handoff notes for #957-#961. Evidence: focused GREEN run below passed target playtime, arc coverage, cast-slot, and handoff assertions.
- [x] T011 Keep this slice as a shared planning/authoring artifact only: no new runtime, no reward/profile changes, no browser-only or console-only route fork. Evidence: implementation touched only `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`, and this task log; no service, reward profile, console runtime, or React/frontend file was changed.
- [x] T012 Rerun focused Daren tests and record GREEN evidence. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenNarrativeSpine" --logger "console;verbosity=minimal"` passed 4/4 on 2026-06-11 after adding the artifact.

## Phase 3: Verification and Reconciliation

- [x] T013 Run affected QTE/docs/browser contract slice: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests`. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 291/291 on 2026-06-11.
- [x] T014 Run client and test-project builds. Evidence: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings/0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings/0 errors.
- [x] T015 Run Spec Kit prerequisite helper and verify `FEATURE_DIR` points to `specs/956-daren-narrative-spine`. Evidence: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-956-daren-narrative-spine\\specs\\956-daren-narrative-spine` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- [x] T016 Run `git diff --check origin/main...HEAD` and added-line static scan. Evidence: `git diff --check origin/main...HEAD` passed after mechanical Spec Kit trailing-whitespace cleanup; added-line scan for secrets/shell/eval/path traversal/security concerns outside specs/tests/docs returned no hits.
- [x] T017 Update this `tasks.md` with RED/GREEN and verification evidence for completed tasks. Evidence: this task log records T003-T016 evidence; T018-T020 remain open for Hermes-owned review, PR, merge, issue closure, and cleanup.

## Phase 4: Hermes-Owned Review, PR, Merge, Closure

- [x] T018 Independent review validates #956 acceptance, #955/#919 boundaries, scene-map drift guards, and no accidental runtime/reward contract change. Evidence: independent Codex review run `E:/Games/codex-runs/20260611-164831-boe-956-daren-narrative-spine-finalreview` returned `APPROVED`, blocking findings none; reviewer inspected `git diff origin/main...HEAD`, `DarenQteNarrativeSpine.json`, route source, tests, and Spec Kit artifacts, and reran focused DarenNarrativeSpine 4/4 plus DarenQteShowcaseTests 22/22.
- [x] T019 Create PR with local-gated verification evidence and safe closing wording for #956. Evidence: PR #964 created at https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/964 with `Closes #956`, local verification evidence, independent review evidence, and follow-up issue references worded as non-lifecycle references.
- [ ] T020 Squash-merge to `main`, verify PR merged and #956 closed/completed, post evidence comment, and clean up worktree/branches.

## Notes for Codex

- Mark T003-T017 complete only after diff and command evidence exist.
- Leave T018-T020 open; Hermes owns independent review, PR, merge, issue closure, and cleanup.
- If implementation touches React/frontend files, run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact counts.
- If requirements change, update `spec.md`, `plan.md`, and `contracts/daren-narrative-spine.md` before final response.
