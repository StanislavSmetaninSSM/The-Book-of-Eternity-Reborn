# Tasks: Daren Endings and Reward Presentation

**Input**: `specs/960-daren-endings-rewards/spec.md`, `plan.md`, `contracts/daren-endings-rewards.md`, `checklists/requirements.md`
**Source Issues**: [#960](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/960), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisites [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), [#959](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959), base [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Phase 0: Hermes Preflight and Spec Kit Setup

- [x] T001 Select #960 as the next logical closure unit after #956/#957/#958/#959 because the Daren narrative spine, shared prose, dialogue/cast, and in-route branch consequence work are merged; #960 precedes #961 content-quality gates and parent #955 closure; no open PR was found; no repo pause/complete sentinel was present; and no correlated live Codex process was found in preflight.
- [x] T002 Create isolated worktree `E:/Games/worktrees/boe-960-daren-endings-rewards` on branch `work/960-daren-endings-rewards` from `origin/main`.
- [x] T003 Mark #960 `status: in-progress` and remove `status: triaged` on GitHub.
- [x] T004 Record focused baseline before implementation. Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 304/304 on 2026-06-11 before #960 code changes.
- [x] T005 Verify Spec Kit prerequisite helper discovers `specs/960-daren-endings-rewards/` and record `specify version`/integration evidence after artifact creation. Evidence: `specify version` reported CLI Version 0.9.3; `specify integration list` reported Codex CLI installed as default; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-960-daren-endings-rewards\\specs\\960-daren-endings-rewards` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.

## Phase 1: RED Tests

- [ ] T006 Add a failing ending-epilogue coverage test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that asserts `no_reward_failure`, `shadow_on_the_run`, `broken_trail`, `clean_heist`, and `perfect_shadow` each have non-empty distinct epilogue prose.
- [ ] T007 Add a failing tier-consequence test that asserts ending epilogues/reward copy contain tier-appropriate consequence language for suspicion/evidence, ward pressure, witnesses, pursuit control, route cleanliness, hideout safety, or unsafe failure.
- [ ] T008 Add a failing reward-presentation test that asserts reward-granting endings explain the permanent Daren achievement and future New Game Ink Feather start bonus in-world, not only as a `+N` mechanical receipt.
- [ ] T009 Add or update failing browser/console shared-data tests so `DarenShowcaseEnding` and `DarenShowcaseEndingDto` carry the same epilogue/reward fields used by console completion.
- [ ] T010 Add or update boundary tests so reward thresholds, tier ids, bonus values, profile path, no-downgrade behavior, and New Game one-time grant semantics remain unchanged.
- [ ] T011 Add or update source guards so #960 does not introduce a new reward profile file, ending-state runtime, campaign-state side effect, QTE check type, or frontend-only ending mapping.
- [ ] T012 Run the focused Daren test filter and record RED evidence showing the new tests fail because ending epilogue/reward presentation is missing, not because of typo or harness errors.

## Phase 2: GREEN Shared Ending and Reward Presentation

- [ ] T013 Update `BookOfEternityClient/Services/DarenQteRewardProfileService.cs` with shared ending epilogue/reward-presentation data for failure and all reward tiers while preserving existing thresholds, ids, bonuses, profile writes, and New Game grant semantics.
- [ ] T014 Update `BookOfEternityClient/Services/QteSceneService.Daren.cs` so Daren completion summaries, `DarenShowcaseEnding`, feedback, and console completion rendering include the shared epilogue/reward explanation.
- [ ] T015 Update `BookOfEternityClient/WebUi/QteWebInteractionService.cs` only if needed so browser Daren ending DTO exposes the same shared epilogue/reward fields.
- [ ] T016 Update `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` only as needed for #960 source/ending/reward handoff truth while preserving #956/#957/#958/#959 invariants and #961 future links.
- [ ] T017 Keep #959 branch consequence result text intact; #960 ending copy should complement route-specific choices without adding a branch-memory runtime.
- [ ] T018 Rerun focused Daren tests and record GREEN evidence.

## Phase 3: Verification and Reconciliation

- [ ] T019 Run affected QTE/docs/browser contract slice: `DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests`.
- [ ] T020 Run client and test-project builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- [ ] T021 Run Spec Kit prerequisite helper and verify `FEATURE_DIR` points to `specs/960-daren-endings-rewards`.
- [ ] T022 Run `git diff --check origin/main...HEAD` and an added-line static scan. Exclude specs/tests/docs from the security scan only when matches are clearly plan/test placeholder text rather than production code.
- [ ] T023 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/frontend files change or a browser display bug is found; otherwise record why frontend verify was not required.
- [ ] T024 Update this `tasks.md` with RED/GREEN and verification evidence for completed implementation tasks.

## Phase 4: Hermes-Owned Review, PR, Merge, Closure

- [ ] T025 Independent review validates #960 acceptance, #955/#956/#957/#958/#959/#919 boundaries, shared ending data, unchanged reward mechanics, browser/console parity, and no accidental #961 broad content-quality work.
- [ ] T026 Create PR with local-gated verification evidence and safe closing wording for #960.
- [ ] T027 Squash-merge to `main`, verify PR merged and #960 closed/completed, post evidence comment, remove/restore temporary labels as appropriate, and clean up worktree/branches.

## Notes for Codex

- Follow TDD strictly: RED tests first, verify failure, then implementation, then GREEN verification.
- Mark T006-T024 complete only after diff and command evidence exist.
- Leave T025-T027 open; Hermes owns independent review, PR, merge, issue closure, and cleanup.
- Do not broaden into #961 broad content-quality gates or parent #955 closure.
- Do not add a new reward profile file, ending-state runtime, campaign-state side effect, frontend-only ending mapping, or QTE check type.
- If implementation touches React/frontend files, run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact counts.
- If requirements change, update `spec.md`, `plan.md`, and `contracts/daren-endings-rewards.md` before final response.
