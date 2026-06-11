# Tasks: Daren Scene 05 Full Literary Page

**Input**: `specs/973-daren-keykeeper-gallery/spec.md`, `plan.md`, issue #973 body, parent #955 comments, existing Daren specs #956-#961, completed scenes #969-#972.
**Source Issues**: [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## TDD / Implementation

- [X] T001 Confirm baseline status on branch `work/973-daren-keykeeper-gallery` and record focused Daren test count.
- [X] T002 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `guard_interrogation` scene.
- [X] T003 Run focused Daren tests and record RED evidence showing the new #973 guard fails for the expected reason.
- [X] T004 Replace only the `guard_interrogation` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a substantial Russian dark-fantasy keykeeper/social-pressure page.
- [X] T005 Run focused Daren tests and record GREEN evidence.
- [X] T006 Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.
- [X] T007 Run client/test-project build and `git diff --check origin/main...HEAD`.
- [X] T008 Run added-line static scan excluding specs/tests/docs and record `NO_MATCHES` or findings.

## Scope / Spec Kit Reconciliation

- [X] T009 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T010 Verify `specs/973-daren-keykeeper-gallery/` references #973/#955 and that tasks/checklists match the final diff.
- [X] T011 Verify #974-#983 and parent #955 remain out of scope.

## Hermes-Owned Lifecycle

- [X] T012 Independent review of #973 implementation and literary quality bar.
- [X] T013 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T014 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T015 Comment closure evidence on #973, close as completed, confirm issue state closed.
- [ ] T016 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/973-daren-keykeeper-gallery`; `git status --short --branch` showed only untracked `specs/973-daren-keykeeper-gallery/`. Baseline focused Daren run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 45 passed / 0 failed / 0 skipped / 45 total.
- T002: Added `DarenGuardInterrogation_ReadsAsKeykeeperLiteraryPageWithoutMechanicDrift` with grouped motif checks and mechanic invariants for `guard_interrogation`.
- T003 RED: Focused Daren run after adding the guard failed as expected: 45 passed / 1 failed / 0 skipped / 46 total. Failing assertion: `Daren guard_interrogation narrative should be a substantial keykeeper social-pressure page, not a compact synopsis.`
- T004: Replaced only the shared `guard_interrogation` beat prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; action/routing/config/score/result text code paths were not edited.
- T005 GREEN: Focused Daren run after the rewrite passed: 46 passed / 0 failed / 0 skipped / 46 total.
- T006: Affected C# slice passed: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 315 passed / 0 failed / 0 skipped / 315 total.
- T007: Client build passed with 0 warnings / 0 errors; test-project build passed with 0 warnings / 0 errors; `git diff --check origin/main...HEAD` exited 0 with no output.
- T008: Added-line production scan over `BookOfEternityClient/Services/QteSceneService.Daren.cs` for forbidden implementation terms returned `NO_MATCHES`.
- T009: Local diff review found production changes only in the `guard_interrogation` narrative block plus focused tests/spec evidence; no frontend files changed and no action/config/routing/score/reward/runtime lines matched the scope-drift scan.
- T010: Feature artifacts reference #973 and parent #955; checklist and tasks were updated to match recorded evidence while Hermes-owned lifecycle tasks remain unchecked.
- T011: Read-only `gh issue view 955 --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --json number,state,url,title` returned `state: OPEN`; no #974-#983 files or issue lifecycle actions were touched.
- T012: Independent Codex review run `E:/Games/codex-runs/20260611-172142-boe-973-daren-keykeeper-gallery-review` exited 0 and returned `APPROVED_WITH_NOTES`: no Critical/Important/Minor issues; reviewer accepted literary quality, social interaction, Daren/Лукьян treatment, mechanic invariants, and Spec Kit scope. Detached review checkout could not rerun Spec Kit prerequisites, so Hermes reran them in the implementation worktree and confirmed `FEATURE_DIR` resolves to `specs/973-daren-keykeeper-gallery`.
- T013: PR [#1015](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1015) was created against `main` with local verification evidence, `Closes #973`, and non-closing parent/sibling references for #955 and #974-#983.
