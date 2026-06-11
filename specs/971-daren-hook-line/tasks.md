# Tasks: Daren Scene 03 Full Literary Page

**Input**: `specs/971-daren-hook-line/spec.md`, `plan.md`, issue #971 body, parent #955 comments, existing Daren specs #956-#961, completed scenes #969-#970.
**Source Issues**: [#971](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/971), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## TDD / Implementation

- [X] T001 Confirm baseline status on branch `work/971-daren-hook-line` and record focused Daren test count.
- [X] T002 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `gadget_infiltration` scene.
- [X] T003 Run focused Daren tests and record RED evidence showing the new #971 guard fails for the expected reason.
- [X] T004 Replace only the `gadget_infiltration` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a substantial Russian dark-fantasy hook-and-line infiltration page.
- [X] T005 Run focused Daren tests and record GREEN evidence.
- [X] T006 Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.
- [X] T007 Run client/test-project build and `git diff --check origin/main...HEAD`.
- [X] T008 Run added-line static scan excluding specs/tests/docs and record `NO_MATCHES` or findings.

## Scope / Spec Kit Reconciliation

- [X] T009 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T010 Verify `specs/971-daren-hook-line/` references #971/#955 and that tasks/checklists match the final diff.
- [X] T011 Verify #972-#983 and parent #955 remain out of scope.

## Hermes-Owned Lifecycle

- [X] T012 Independent review of #971 implementation and literary quality bar.
- [X] T013 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T014 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T015 Comment closure evidence on #971, close as completed, confirm issue state closed.
- [ ] T016 Clean up worktree/branches and report next target.

## Codex Evidence

- T001: Initial `git status --short --branch` showed `## work/971-daren-hook-line...origin/main` and only `?? specs/971-daren-hook-line/`; Hermes baseline focused Daren run passed 43/43.
- T003 RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed 1 / passed 43 / skipped 0 / total 44 on `DarenGadgetInfiltration_ReadsAsHookAndLineLiteraryPageWithoutMechanicDrift`, with the expected synopsis-length assertion.
- T005 GREEN: same focused command passed 44 / failed 0 / skipped 0 / total 44.
- T006: affected slice command passed 313 / failed 0 / skipped 0 / total 313.
- T007: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` both completed with 0 warnings and 0 errors; post-commit `git diff --check origin/main...HEAD` exited 0 with no output.
- T008: Added-line static scan of `BookOfEternityClient/Services/QteSceneService.Daren.cs` for `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, and `QTE` returned `NO_MATCHES`.
- T009/T011: Diff is limited to `BookOfEternityClient/Services/QteSceneService.Daren.cs`, `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, and `specs/971-daren-hook-line/`; no frontend/runtime/endpoint/reward/profile/New Game files or #972-#983 scenes were edited.
- T012: Independent Codex review in detached worktree `E:/Games/worktrees/boe-971-daren-hook-line-review` returned `APPROVED_WITH_NOTES` with no Critical or Important findings. The only Minor wording note about `Source Issues` in `spec.md` was fixed before PR.
- T013: PR #986 was created with local verification evidence, `Closes #971`, and explicit scope boundary that #955 remains open while #972-#983 remain separate tasks.
