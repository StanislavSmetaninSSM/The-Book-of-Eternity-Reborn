# Tasks: Daren NPC Dialogue Cast

**Input**: `specs/958-daren-dialogue-cast/spec.md`, `plan.md`, `contracts/daren-dialogue-cast.md`, `checklists/requirements.md`
**Source Issues**: [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), prerequisite [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), base [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Phase 0: Hermes Preflight and Spec Kit Setup

- [x] T001 Select #958 as the next logical closure unit after #956/#957 because the narrative spine and shared prose are merged, #958 is the next child under parent #955, no open PR was found, and no correlated live Codex process was found in preflight.
- [x] T002 Create isolated worktree `E:/Games/worktrees/boe-958-daren-dialogue-cast` on branch `work/958-daren-dialogue-cast` from `origin/main`.
- [x] T003 Mark #958 `status: in-progress` and remove `status: triaged` on GitHub.
- [x] T004 Record focused baseline before implementation. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 294/294 on 2026-06-11 before #958 code changes.
- [x] T005 Verify Spec Kit prerequisite helper discovers `specs/958-daren-dialogue-cast/`. Evidence: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-958-daren-dialogue-cast\\specs\\958-daren-dialogue-cast` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`; `specify version` reported 0.9.3 and Codex integration installed.

## Phase 1: RED Tests

- [ ] T006 Add a failing cast-coverage test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that asserts the route/spine exposes four named/personified figures for the required cast slots: contact/informant, estate staff/guard, magical-security authority or house representative, and pursuit figure.
- [ ] T007 Add a failing dialogue/social-choice coverage test that asserts the Daren route contains at least three people-driven dialogue/social-choice moments implemented as existing QTE route chapters/actions.
- [ ] T008 Add a failing choice-option test that verifies interactive dialogue choices expose player-facing answer labels/descriptions/hints through supported existing QTE check config, preferring `PrecisionChoice` when player selection is expected.
- [ ] T009 Add a failing response-variant test that asserts dialogue/social-choice success/partial/fail result texts are distinct, non-empty, concise, and read as NPC/social reactions.
- [ ] T010 Add a failing consequence test that asserts at least one dialogue/social-choice outcome affects existing score/risk metrics and later route copy/result text references an earlier NPC/social consequence.
- [ ] T011 Keep or update route/spine boundary tests so the original #957 heist beats remain present in original relative order, route id/reward semantics stay unchanged, and no new dialogue runtime/state/endpoint/frontend-only fork/check type is introduced.
- [ ] T012 Run the focused Daren test filter and record RED evidence showing the new tests fail because cast/dialogue/choice content is missing, not because of typo or harness errors.

## Phase 2: GREEN Shared Route Dialogue

- [ ] T013 Update `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a small Daren NPC cast and shared route text that makes the contact/informant, estate staff/guard, magical-security authority/house representative, and pursuit figure visible to players.
- [ ] T014 Add at least three dialogue/social-choice moments inside the existing QTE route flow using existing check types and route/action/config data. If inserting new chapters, keep original heist beats ordered as a subsequence.
- [ ] T015 Author success/partial/fail result text for dialogue/social-choice actions so NPC responses differ by grade and remain player-facing/console-friendly.
- [ ] T016 Add score/risk deltas for at least one dialogue/social-choice action using existing metrics; do not create new score/state machinery.
- [ ] T017 Add modest later prose/result references to earlier NPC/social consequences without broadening into #959 branch-consequence expansion or #960 endings.
- [ ] T018 Update `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` only as needed for #958 source/cast/dialogue truth while preserving #956/#957 invariants and #959-#961 handoff links.
- [ ] T019 Rerun focused Daren tests and record GREEN evidence.

## Phase 3: Verification and Reconciliation

- [ ] T020 Run affected QTE/docs/browser contract slice: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests`.
- [ ] T021 Run client and test-project builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- [ ] T022 Run Spec Kit prerequisite helper and verify `FEATURE_DIR` points to `specs/958-daren-dialogue-cast`.
- [ ] T023 Run `git diff --check origin/main...HEAD` and an added-line static scan. Exclude specs/tests/docs from the security scan only when matches are clearly plan/test placeholder text rather than production code.
- [ ] T024 Update this `tasks.md` with RED/GREEN and verification evidence for completed implementation tasks.

## Phase 4: Hermes-Owned Review, PR, Merge, Closure

- [ ] T025 Independent review validates #958 acceptance, #955/#956/#957/#919 boundaries, shared route dialogue/cast, no new dialogue runtime, no accidental reward/campaign changes, and no default UI technical wording.
- [ ] T026 Create PR with local-gated verification evidence and safe closing wording for #958.
- [ ] T027 Squash-merge to `main`, verify PR merged and #958 closed/completed, post evidence comment, remove/restore temporary labels as appropriate, and clean up worktree/branches.

## Notes for Codex

- Follow TDD strictly: RED tests first, verify failure, then implementation, then GREEN verification.
- Mark T006-T024 complete only after diff and command evidence exist.
- Leave T025-T027 open; Hermes owns independent review, PR, merge, issue closure, and cleanup.
- Do not broaden into #959 branch-specific consequences, #960 endings/rewards, or #961 broad content quality gates.
- If implementation touches React/frontend files, run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact counts.
- If requirements change, update `spec.md`, `plan.md`, and `contracts/daren-dialogue-cast.md` before final response.
