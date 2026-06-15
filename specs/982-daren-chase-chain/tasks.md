# Tasks: Daren Scene 14 Full Literary Page

**Input**: `specs/982-daren-chase-chain/spec.md`, `plan.md`, issue #982 body, parent #955 context, previous scene #981, and existing Daren route/test patterns.
**Tracked issue and related context**: [#982](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/982), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), previous scene [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981), next scene [#983](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/983)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/982-daren-chase-chain` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/982-daren-chase-chain/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `chase_chain` scene and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #982 guard fails for the expected reason.

## Phase 3: User Story 1 - Courtyard Chain Literary Page (Priority: P1)

**Goal**: Replace `chase_chain` with a substantial Russian dark-fantasy courtyard chase-sequence page while preserving existing mechanics.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `chase_chain` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T014 Verify `specs/982-daren-chase-chain/` references #982/#955 and that tasks/checklists match the final diff.
- [X] T015 Verify #983, result/aftermath issues #988-#1014, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/982-daren-chase-chain`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #982 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [X] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #982, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/982-daren-chase-chain` started from `origin/main` at `057b484`; source issue #982 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` for #982.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 81 passed / 0 failed / 0 skipped / 81 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 350 passed / 0 failed / 0 skipped / 350 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-982-daren-chase-chain\\specs\\982-daren-chase-chain` with `contracts/` and `tasks.md`.
- T005: Added `DarenChaseChain_ReadsAsCourtyardChainPageWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`. The guard pins shared `beat.PlayerText == chapter.Narrative`, route id/title, `chase_chain_action`, label, `PromptChain`, `Characteristics.Speed`, difficulty `4`, `null` config, routing to `hideout_return`, score deltas from `DarenScoreDeltas(pursuit: 4, evidence: -2)`, page length, sentence count, Daren active POV, grouped motifs, and forbidden player-facing terms.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => expected failure: 81 passed / 1 failed / 0 skipped / 82 total. Failing guard reported old `chase_chain` synopsis at 253 chars, 2 sentences, 1 Daren mention, plus missing grouped motif coverage.
- T007: Replaced only `chase_chain` / "Цепочка дворов" prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. Runtime-shaped text count: 3600 chars, 32 sentences, 11 Daren mentions.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 82 passed / 0 failed / 0 skipped / 82 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 351 passed / 0 failed / 0 skipped / 351 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded, 0 warnings, 0 errors. Test build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded, 0 warnings, 0 errors.
- T011 whitespace check: `git diff --check origin/main...HEAD` exited 0 with no whitespace errors in both Codex and Hermes fresh verification.
- T012 added-line production/test static scan for hardcoded secrets, shell execution, eval/exec, unsafe deserialization, and SQL string formatting => `NO_MATCHES`; Hermes refined production-prose technical/meta scan also returned `NO_MATCHES`.
- T013 diff inspection: production code change is limited to shared `chase_chain` prose; tests add the focused guard and allow `chase_chain` to use the existing full-page 3600-char cap. No route/action/check/config/routing/score/reward/profile/New Game/endpoint/runtime/frontend drift found.
- T014 Spec Kit reconciliation: `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and `tasks.md` reference #982/#955 and match the final scoped diff.
- T015 Out-of-scope verification: #983, result/aftermath issues #988-#1014, parent #955 lifecycle, frontend files, runtime state, reward behavior, and mechanics are untouched.
- T016: Focused local implementation commit `fe76652a96cd60d8c128e078a04db920cbffe419` (`Rewrite Daren chase chain scene [skip ci]`) was reconciled by Hermes: worktree clean/ahead 1, no PR existed, no run artifacts in repo status.
- T017 Hermes fresh gates before independent review: Spec Kit prerequisite resolved `specs/982-daren-chase-chain`; focused Daren `82/82` passed; affected Daren/QTE/docs/browser slice `351/351` passed; client build `0 warnings / 0 errors`; test build `0 warnings / 0 errors`; `git diff --check origin/main...HEAD` clean; static/prose scans `NO_MATCHES`.
- T017 independent review: detached Codex review run `E:/Games/codex-runs/20260616-030507-review-boe-982-daren-chase-chain`, worktree `E:/Games/worktrees/boe-982-daren-chase-chain-review-20260616-030507`, verdict `APPROVED`; Critical/Important/Minor findings: none. Review verified expected file scope, beat order, `chase_chain` route/action contract, prose metrics, no technical/meta leakage, and a focused guard passed (`1/1`).
- T018 PR evidence: PR #1051 was created for `work/982-daren-chase-chain`, read back as `OPEN` / `CLEAN`, with closing references limited to #982. PR body states GitHub Actions are not used/required and lists local gates plus approved independent review; parent #955 and sibling issues are mentioned only as non-closing references.
- T019 merge evidence: PR #1051 was squash-merged as `512434ec9c11206765281dcac4cec4321868ca91`, and GitHub auto-closed #982 as `CLOSED` / `COMPLETED`.
- T019 post-merge CRLF follow-up: primary Windows `main` verification initially failed `DarenChapters_HaveLiterarySceneProseForEveryBeat` with `Actual: 3618` over the `3600` cap. Follow-up branch `fix/982-daren-chase-chain-crlf` shortened only two `chase_chain` prose phrases, leaving mechanics unchanged; focused Daren `82/82`, affected Daren/QTE/docs/browser slice `351/351`, `git diff --check`, static scan, and refined prose/meta scan all passed after the trim.
- T019 fix re-review: detached Codex re-review run `E:/Games/codex-runs/20260616-032342-rereview-boe-982-daren-chase-chain-crlf`, worktree `E:/Games/worktrees/boe-982-daren-chase-chain-crlf-rereview-20260616-032342`, verdict `APPROVED`; Critical/Important/Minor findings: none. Review measured CRLF runtime length `3576/3600`, confirmed mechanics unchanged, and reran focused/affected gates (`82/82`, `351/351`).
