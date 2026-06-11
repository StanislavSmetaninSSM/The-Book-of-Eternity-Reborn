# Tasks: Daren Scene 06 Full Literary Page

**Input**: `specs/974-daren-lock-pick/spec.md`, `plan.md`, issue #974 body, parent #955 comments, existing Daren specs #956-#961, completed scenes #969-#973.
**Source Issues**: [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## TDD / Implementation

- [X] T001 Confirm baseline status on branch `work/974-daren-lock-pick` and record focused Daren test count.
- [X] T002 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `lock_pick` scene.
- [X] T003 Run focused Daren tests and record RED evidence showing the new #974 guard fails for the expected reason.
- [X] T004 Replace only the `lock_pick` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a substantial Russian dark-fantasy lock-picking page.
- [X] T005 Run focused Daren tests and record GREEN evidence.
- [X] T006 Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.
- [X] T007 Run client/test-project build and `git diff --check origin/main...HEAD`.
- [X] T008 Run added-line static scan excluding specs/tests/docs and record `NO_MATCHES` or findings.

## Scope / Spec Kit Reconciliation

- [X] T009 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T010 Verify `specs/974-daren-lock-pick/` references #974/#955 and that tasks/checklists match the final diff.
- [X] T011 Verify #975-#983 and parent #955 remain out of scope.

## Hermes-Owned Lifecycle

- [X] T012 Independent review of #974 implementation and literary quality bar.
- [X] T013 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T014 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T015 Comment closure evidence on #974, close as completed, confirm issue state closed.
- [ ] T016 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/974-daren-lock-pick`; `git status --short --branch` showed only untracked `specs/974-daren-lock-pick/`. Spec Kit prerequisites resolved `FEATURE_DIR` to `specs/974-daren-lock-pick` with `contracts/` and `tasks.md`. Baseline focused Daren run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 46 passed / 0 failed / 0 skipped / 46 total.
- T002/T003 RED: Added `DarenLockPick_ReadsAsCabinetLockLiteraryPageWithoutMechanicDrift` before production prose changes. `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed as expected: 46 passed / 1 failed / 0 skipped / 47 total. Failure was the new #974 guard: `Daren lock_pick narrative should be a substantial cabinet-lock burglary page, not a compact synopsis.`
- T004: Replaced only the `lock_pick` `PlayerText` in shared `BookOfEternityClient/Services/QteSceneService.Daren.cs`; action id, label, `LockPinSet` check/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, and frontend code were not changed.
- T005 GREEN: Focused Daren rerun after final prose cleanup: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 47 passed / 0 failed / 0 skipped / 47 total.
- T006: Affected C# slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 316 passed / 0 failed / 0 skipped / 316 total.
- T007: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors. `git diff --check origin/main...HEAD` => exit 0.
- T008: Production added-line static scan over `BookOfEternityClient/Services/QteSceneService.Daren.cs` for `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, and `QTE` => `NO_MATCHES`.
- T009: Diff inspection shows only `BookOfEternityClient/Services/QteSceneService.Daren.cs` prose for `lock_pick` and `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` guards changed; no QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, or frontend/browser files changed.
- T010/T011: `specs/974-daren-lock-pick/` continues to reference #974 and parent #955. Checklist updated for completed implementation/verification items; #975-#983 and parent #955 remain out of scope. Hermes-owned lifecycle tasks T014-T016 remain unchecked.
- T012: Independent Codex review run `E:/Games/codex-runs/20260612-053612-boe-974-daren-lock-pick-review` returned `APPROVED`; blocking findings: none. Reviewer verified `FullyQualifiedName~DarenQteShowcaseTests` => 47 passed / 0 failed / 0 skipped, Spec Kit prerequisites, `git diff --check origin/main...HEAD`, production added-line static scan => `NO_MATCHES`, and clean review/implementation worktrees.
- T013: PR [#1016](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1016) created with local verification evidence, independent review verdict, `Closes #974`, and explicit note that parent #955 remains open.
