# Tasks: Daren Scene 07 Full Literary Page

**Input**: `specs/975-daren-rune-memory/spec.md`, `plan.md`, issue #975 body, parent #955 comments, existing Daren specs #956-#961, completed scenes #969-#974.
**Source Issues**: [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## TDD / Implementation

- [X] T001 Confirm baseline status on branch `work/975-daren-rune-memory` and record focused Daren test count.
- [X] T002 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `rune_memory` scene.
- [X] T003 Run focused Daren tests and record RED evidence showing the new #975 guard fails for the expected reason.
- [X] T004 Replace only the `rune_memory` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a substantial Russian dark-fantasy magical rune-memory page.
- [X] T005 Run focused Daren tests and record GREEN evidence.
- [X] T006 Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.
- [X] T007 Run client/test-project build and `git diff --check origin/main...HEAD`.
- [X] T008 Run added-line static scan excluding specs/tests/docs and record `NO_MATCHES` or findings.

## Scope / Spec Kit Reconciliation

- [X] T009 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T010 Verify `specs/975-daren-rune-memory/` references #975/#955 and that tasks/checklists match the final diff.
- [X] T011 Verify #976-#983 and parent #955 remain out of scope.

## Hermes-Owned Lifecycle

- [X] T012 Independent review of #975 implementation and literary quality bar.
- [X] T013 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T014 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T015 Comment closure evidence on #975, close as completed, confirm issue state closed.
- [ ] T016 Clean up worktree/branches and report next target.

## Evidence Log

- T001 launch preflight: Branch `work/975-daren-rune-memory` created from `origin/main` at `45b21db` after #974 landed. Initial `git status --short --branch` showed only new untracked `specs/975-daren-rune-memory/` artifacts before baseline/implementation. Spec Kit prerequisites resolved `FEATURE_DIR` to `specs/975-daren-rune-memory` with `contracts/` and `tasks.md`. Baseline focused Daren run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 47 passed / 0 failed / 0 skipped / 47 total. Baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 316 passed / 0 failed / 0 skipped / 316 total.
- T002-T003 RED: Added `DarenRuneMemory_ReadsAsRuneWardMemoryPageWithoutMechanicDrift` before changing production prose. Focused run failed for the expected reason: `Daren rune_memory narrative should be a substantial rune-ward memory page, not a compact synopsis.` Result: 47 passed / 1 failed / 0 skipped / 48 total.
- T004-T005 GREEN: Replaced only `rune_memory` shared route prose in `QteSceneService.Daren.cs`; reran focused Daren tests. Result: 48 passed / 0 failed / 0 skipped / 48 total.
- T006 affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 317 passed / 0 failed / 0 skipped / 317 total.
- T007 builds/checks: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded, 0 warnings / 0 errors. `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded, 0 warnings / 0 errors. `git diff --check origin/main...HEAD` => exit 0.
- T008 added production-line scan: scanned added lines in `BookOfEternityClient/Services/QteSceneService.Daren.cs` for `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`; result `NO_MATCHES`.
- T009-T011 scope reconciliation: implementation diff changes only shared Daren C# route prose and focused Daren tests, plus this #975 Spec Kit directory. The #975 test pins `rune_memory_action`, `PatternMemory`, `Perception`, difficulty, config, routing to `ward_steward_parley`, and score deltas. No frontend/browser/runtime/reward/profile/New Game/endpoints files changed; #976-#983 and parent #955 remain out of scope.
- T012 independent review: Codex review run `E:/Games/codex-runs/20260612-061446-boe-975-daren-rune-memory-review` returned `APPROVED`; blocking findings: none. Reviewer verified diff check, production added-line static scan => `NO_MATCHES`, clean review worktree, implementation worktree at `4d1806f72f3718df9926846c18359f3c66cbe4fd`, prose quality, mechanics preservation, focused guard quality, and Spec Kit lifecycle consistency.
- T013: PR [#1017](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1017) created with local verification evidence, independent review verdict, `Closes #975`, and explicit note that parent #955 remains open.
