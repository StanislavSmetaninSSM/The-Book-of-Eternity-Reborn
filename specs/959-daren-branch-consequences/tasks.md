# Tasks: Daren Branch Consequences

**Input**: `specs/959-daren-branch-consequences/spec.md`, `plan.md`, `contracts/daren-branch-consequences.md`, `checklists/requirements.md`
**Source Issues**: [#959](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), prerequisite [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), prerequisite [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), base [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Phase 0: Hermes Preflight and Spec Kit Setup

- [x] T001 Select #959 as the next logical closure unit after #956/#957/#958 because the narrative spine, shared prose, and dialogue/cast work are merged, #959 is the next child under parent #955, no open PR was found, no repo pause/complete sentinel was present, and no correlated live Codex process was found in preflight.
- [x] T002 Create isolated worktree `E:/Games/worktrees/boe-959-daren-branch-consequences` on branch `work/959-daren-branch-consequences` from `origin/main`.
- [x] T003 Mark #959 `status: in-progress` and remove `status: triaged` on GitHub.
- [x] T004 Record focused baseline before implementation. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 299/299 on 2026-06-11 before #959 code changes.
- [x] T005 Verify Spec Kit prerequisite helper discovers `specs/959-daren-branch-consequences/` and record `specify version`/integration evidence. Evidence: `specify version` reported CLI Version 0.9.3; `specify integration list` reported Codex CLI installed as default; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-959-daren-branch-consequences\\specs\\959-daren-branch-consequences` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.

## Phase 1: RED Tests

- [x] T006 Add a failing branch-consequence coverage test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that asserts key Daren QTE actions have distinct success/partial/fail consequence prose beyond generic pass/fail wording. Evidence: added `DarenKeyQteResults_HaveDistinctBranchConsequenceSignals`.
- [x] T007 Add a failing carry-forward test that asserts at least several earlier decisions or QTE results are referenced later in route prose/result text by NPC, clue, route, ward, witness, evidence, or pursuit pressure. Evidence: added `DarenLaterScenes_EchoSeveralEarlierChoicesAndResults`.
- [x] T008 Add a failing #958 dialogue/planning consequence test that asserts at least one already-landed Daren dialogue/social-choice moment affects later route consequence prose through shared route data. Evidence: added `DarenDialoguePlanningChoices_ReachLaterConsequenceProse`.
- [x] T009 Add a failing playable-bad-outcome test that asserts poor outcomes in non-terminal scenes continue route play where the existing route allows and describe specific risk, detour, suspicion, noise, lost time, or pursuit pressure. Evidence: added `DarenNonTerminalPoorOutcomes_KeepPlayMovingWithSpecificPressure`.
- [x] T010 Add or update boundary tests so branch consequences remain in existing QTE route/action/result/score/spine data and no new consequence runtime/state/endpoint/frontend-only fork/check type is introduced. Evidence: added `DarenBranchConsequences_StayInSharedRouteAndSpineContract`.
- [x] T011 Keep or update route/spine invariants so the original #957 heist beats remain present in original relative order, #958 dialogue beats remain shared route content, route id/reward semantics stay unchanged, and #960/#961 remain follow-ups. Evidence: existing Daren route/spine/reward invariant tests retained and passed with the new #959 guards.
- [x] T012 Run the focused Daren test filter and record RED evidence showing the new tests fail because branch/carry-forward consequence content is missing, not because of typo or harness errors. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed 5/35 before production changes; failures were the new #959 guards for missing branch signal, carry-forward echo, playable pressure, dialogue consequence, and spine source/contract evidence.

## Phase 2: GREEN Shared Route Consequences

- [x] T013 Update `BookOfEternityClient/Services/QteSceneService.Daren.cs` so selected stealth/security/dialogue/pursuit actions have distinct strong/partial/poor consequence prose.
- [x] T014 Deepen at least one #958 dialogue/planning/social-choice outcome so a later route scene references that decision or result.
- [x] T015 Add several carry-forward echoes from earlier QTE results or choices into later route prose/result text while keeping copy concise for console/browser surfaces.
- [x] T016 Use existing `ScoreDeltas`, routing, result text, and supported QTE config fields for consequences; do not add new branch-state machinery.
- [x] T017 Keep bad outcomes playable in non-terminal scenes where the current route continues, using increased pressure/detour/reduced-control text rather than generic failure.
- [x] T018 Update `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` only as needed for #959 source/consequence/carry-forward truth while preserving #956/#957/#958 invariants and #960/#961 handoff links.
- [x] T019 Rerun focused Daren tests and record GREEN evidence. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed 35/35 after shared-route and spine updates.

## Phase 3: Verification and Reconciliation

- [x] T020 Run affected QTE/docs/browser contract slice: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests`. Evidence: passed 304/304 with `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`.
- [x] T021 Run client and test-project builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`. Evidence: both builds succeeded with 0 warnings and 0 errors.
- [x] T022 Run Spec Kit prerequisite helper and verify `FEATURE_DIR` points to `specs/959-daren-branch-consequences`. Evidence: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-959-daren-branch-consequences\\specs\\959-daren-branch-consequences` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- [x] T023 Run `git diff --check origin/main...HEAD` and an added-line static scan. Exclude specs/tests/docs from the security scan only when matches are clearly plan/test placeholder text rather than production code. Evidence: `git diff --check origin/main...HEAD` produced no output; added-line scan for hardcoded secrets and dangerous shell/eval/exec/pickle/SQL patterns returned `No added-line static scan matches.`
- [x] T024 Update this `tasks.md` with RED/GREEN and verification evidence for completed implementation tasks. Evidence: T006-T024 marked complete with command evidence; T025-T027 remain open for Hermes-owned review, PR, merge, issue closure, and cleanup.

## Phase 4: Hermes-Owned Review, PR, Merge, Closure

- [x] T025 Independent review validates #959 acceptance, #955/#956/#957/#958/#919 boundaries, shared route consequences, no new branch-state/consequence runtime, no accidental reward/campaign changes, and no default UI technical wording. Evidence: independent Codex review run `E:/Games/codex-runs/20260611-201431-boe-959-daren-branch-consequences-finalreview` returned `APPROVED`, blocking findings none; reviewer inspected `git diff origin/main...HEAD`, `QteSceneService.Daren.cs`, `DarenQteShowcaseTests.cs`, `DarenQteNarrativeSpine.json`, and Spec Kit artifacts, then ran read-only diff/static/scope checks.
- [ ] T026 Create PR with local-gated verification evidence and safe closing wording for #959.
- [ ] T027 Squash-merge to `main`, verify PR merged and #959 closed/completed, post evidence comment, remove/restore temporary labels as appropriate, and clean up worktree/branches.

## Notes for Codex

- Follow TDD strictly: RED tests first, verify failure, then implementation, then GREEN verification.
- Mark T006-T024 complete only after diff and command evidence exist.
- Leave T025-T027 open; Hermes owns independent review, PR, merge, issue closure, and cleanup.
- Do not broaden into #960 ending/reward presentation or #961 broad content-quality gates.
- Do not add a new campaign-state branch/consequence engine, QTE check type, endpoint, state file, or React-only story fork.
- If implementation touches React/frontend files, run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact counts.
- If requirements change, update `spec.md`, `plan.md`, and `contracts/daren-branch-consequences.md` before final response.
