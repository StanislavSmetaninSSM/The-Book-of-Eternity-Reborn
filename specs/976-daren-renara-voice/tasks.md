# Tasks: Daren Scene 08 Full Literary Page

**Input**: `specs/976-daren-renara-voice/spec.md`, `plan.md`, issue #976 body, parent #955 context, existing Daren specs #956-#961, completed scenes #969-#975.
**Source Issues**: [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## TDD / Implementation

- [X] T001 Confirm baseline status on branch `work/976-daren-renara-voice` and record focused Daren test count.
- [X] T002 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `ward_steward_parley` scene.
- [X] T003 Run focused Daren tests and record RED evidence showing the new #976 guard fails for the expected reason.
- [X] T004 Replace only the `ward_steward_parley` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a substantial Russian dark-fantasy Renara/ward dialogue page.
- [X] T005 Run focused Daren tests and record GREEN evidence.
- [X] T006 Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.
- [X] T007 Run client/test-project build and `git diff --check origin/main...HEAD`.
- [X] T008 Run added-line static scan excluding specs/tests/docs and record `NO_MATCHES` or findings.

## Scope / Spec Kit Reconciliation

- [X] T009 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T010 Verify `specs/976-daren-renara-voice/` references #976/#955 and that tasks/checklists match the final diff.
- [X] T011 Verify #977-#983 and parent #955 remain out of scope.

## Hermes-Owned Lifecycle

- [X] T012 Independent review of #976 implementation and literary quality bar.
- [X] T013 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T014 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T015 Comment closure evidence on #976, close as completed, confirm issue state closed.
- [ ] T016 Clean up worktree/branches and report next target.

## Evidence Log

- T001 launch preflight: Branch `work/976-daren-renara-voice` created from `origin/main` at `03806e9` after #975 landed. Initial `git status --short --branch` showed only new untracked `specs/976-daren-renara-voice/` artifacts before implementation. Spec Kit prerequisites resolved `FEATURE_DIR` to `specs/976-daren-renara-voice` with `contracts/` and `tasks.md`. Baseline focused Daren run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 48 passed / 0 failed / 0 skipped / 48 total. Baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 317 passed / 0 failed / 0 skipped / 317 total.
- T002/T003 RED: Added `DarenWardStewardParley_ReadsAsRenaraWardDialoguePageWithoutMechanicDrift` before production prose changes. Focused Daren command failed for the expected reason: 48 passed / 1 failed / 0 skipped / 49 total, with the new guard reporting that `ward_steward_parley` was still a compact synopsis rather than a substantial Renara ward-dialogue page.
- T004/T005 GREEN: Replaced only the shared `ward_steward_parley` authored prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; route/action/check/routing/scoring code remained unchanged. Focused Daren command passed: 49 passed / 0 failed / 0 skipped / 49 total.
- T006 affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 318 passed / 0 failed / 0 skipped / 318 total.
- T007 builds/checks: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => 0 warnings / 0 errors; working-tree `git diff --check` => no whitespace errors.
- T008 static scan: added-line production C# forbidden default-player technical-term scan for `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE` returned `NO_MATCHES`.
- T010 Spec Kit reconciliation: `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` all continue to reference #976 and parent #955; checklist completion now matches the local implementation evidence while the independent review gate remains unchecked.
- T009/T011 scope reconciliation: inspected diff and verified only `QteSceneService.Daren.cs`, `DarenQteShowcaseTests.cs`, and `specs/976-daren-renara-voice/` artifacts changed; no frontend files, endpoints, runtime state, rewards, route ids, beat ids/order, action ids/labels, check type/config, routing targets, or score helpers were modified. #977-#983 and parent #955 remain out of scope.
- T012 independent review: first read-only Codex review on commit `1ce6b04` returned `CHANGES_REQUIRED` because a detached CRLF checkout measured `ward_steward_parley` at 3621 chars against the 3600-char broad Daren cap, even though the literary quality/mechanics scope were otherwise approved. Fixed by shortening the prose without changing mechanics, then reran focused Daren (49/49), affected slice (318/318), `git diff --check`, and production added-line static scan (`NO_MATCHES`). Re-review on commit `9e7f6b4` returned `APPROVED_WITH_NOTES`: no blocking findings; the reviewer confirmed focused Daren 49/49, affected slice 318/318, diff-check/static scan passed, literary quality met the #976 bar, and mechanics/frontend/reward scope stayed unchanged. Non-blocking note: the scene is close to the 3600 CRLF cap, so future prose tweaks must rerun the focused Daren guard.
- T013 PR evidence: opened PR #1018 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1018`) with local-gated verification, independent-review evidence, `Closes #976`, and explicit boundary that parent #955 plus #977-#983/result-follow-ups remain out of scope.
