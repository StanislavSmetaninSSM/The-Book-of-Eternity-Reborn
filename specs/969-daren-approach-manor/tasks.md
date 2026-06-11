# Tasks: Daren Scene 01 Full Literary Page

**Input**: `specs/969-daren-approach-manor/spec.md`, `plan.md`, issue #969 body, parent #955 comments, existing Daren specs #956-#961.
**Source Issues**: [#969](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/969), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## TDD / Implementation

- [x] T001 Confirm baseline status on branch `work/969-daren-approach-manor` and record focused Daren test count.
  - Evidence: initial local status confirmed `## work/969-daren-approach-manor...origin/main` with intentional untracked `specs/969-daren-approach-manor/`; Hermes preflight focused Daren baseline was 41 passed / 0 failed / 0 skipped.
- [x] T002 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `approach_manor` scene.
  - Evidence: added `DarenApproachManor_ReadsAsFullLiteraryPage`, including shared beat/chapter parity, unchanged first action metadata, length/sentence/protagonist checks, scene motif checks, and forbidden technical-term guard.
- [x] T003 Run focused Daren tests and record RED evidence showing the new #969 guard fails for the expected reason.
  - Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed as expected: 41 passed / 1 failed / 0 skipped / 42 total. Failure: `DarenApproachManor_ReadsAsFullLiteraryPage` reported `Daren approach_manor narrative should be a substantial literary page, not a compact synopsis.`
- [x] T004 Replace only the `approach_manor` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a substantial Russian dark-fantasy page.
  - Evidence: replaced only the `approach_manor` `DarenShowcaseBeat` player text with multi-paragraph Russian prose; no action/check/routing/scoring/reward/runtime/frontend data was edited.
- [x] T005 Run focused Daren tests and record GREEN evidence.
  - Evidence: focused Daren command passed: 42 passed / 0 failed / 0 skipped / 42 total.
- [x] T006 Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.
  - Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed: 311 passed / 0 failed / 0 skipped / 311 total.
- [x] T007 Run client/test-project build and `git diff --check origin/main...HEAD`.
  - Evidence: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings / 0 errors. `git diff --check origin/main...HEAD` passed on the committed diff with no output.
- [x] T008 Run added-line static scan excluding specs/tests/docs and record `NO_MATCHES` or findings.
  - Evidence: production added-line scan excluding specs/tests/docs returned `NO_MATCHES` for forbidden technical/player-facing terms (`GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`).

## Scope / Spec Kit Reconciliation

- [x] T009 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
  - Evidence: diff inspection showed production edit limited to the `approach_manor` narrative string in `BuildDarenBeats`; no frontend files, route ids, beat order, action id/check type, routing, score deltas, reward/profile/New Game grant, endpoint, or runtime-state files changed.
- [x] T010 Verify `specs/969-daren-approach-manor/` references #969/#955 and that tasks/checklists match the final diff.
  - Evidence: `spec.md`, `plan.md`, `tasks.md`, contract, and checklist reference #969/#955 and describe the same one-scene shared route-data rewrite; prerequisite helper returned `FEATURE_DIR` as `specs/969-daren-approach-manor` with `contracts/` and `tasks.md`; `tasks.md` now records implementation and verification evidence.
- [x] T011 Verify #970-#983 and parent #955 remain out of scope.
  - Evidence: no issue lifecycle actions were taken; this implementation changes only scene 01 and leaves #970-#983 plus parent #955 for Hermes/follow-up handling.

## Hermes-Owned Lifecycle

- [x] T012 Independent review of #969 implementation and literary quality bar.
  - Evidence: independent review returned `APPROVED_WITH_NOTES` with no Critical or Important findings; minor typography/checklist notes were addressed, followed by focused Daren tests `42 passed / 0 failed / 0 skipped`, affected slice `311 passed / 0 failed / 0 skipped`, `git diff --check`, and production added-line static scan `NO_MATCHES`.
- [ ] T013 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T014 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T015 Comment closure evidence on #969, close as completed, confirm issue state closed.
- [ ] T016 Clean up worktree/branches and report next target.
