# Tasks: Daren Scene 02 Full Literary Page

**Input**: `specs/970-daren-mira-whisper/spec.md`, `plan.md`, issue #970 body, parent #955 comments, existing Daren specs #956-#961, completed scene #969.
**Source Issues**: [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## TDD / Implementation

- [X] T001 Confirm baseline status on branch `work/970-daren-mira-whisper` and record focused Daren test count.
- [X] T002 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `informant_parley` scene.
- [X] T003 Run focused Daren tests and record RED evidence showing the new #970 guard fails for the expected reason.
- [X] T004 Replace only the `informant_parley` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a substantial Russian dark-fantasy Mira/Daren page.
- [X] T005 Run focused Daren tests and record GREEN evidence.
- [X] T006 Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.
- [X] T007 Run client/test-project build and `git diff --check origin/main...HEAD`.
- [X] T008 Run added-line static scan excluding specs/tests/docs and record `NO_MATCHES` or findings.

## Scope / Spec Kit Reconciliation

- [X] T009 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types, choice ids/outcomes, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T010 Verify `specs/970-daren-mira-whisper/` references #970/#955 and that tasks/checklists match the final diff.
- [X] T011 Verify #971-#983 and parent #955 remain out of scope.

## Hermes-Owned Lifecycle

- [X] T012 Independent review of #970 implementation and literary quality bar.
  - Evidence: independent Codex review returned `APPROVED_WITH_NOTES` with no Critical or Important findings. Minor guard-strength notes were addressed by adding grouped motif assertions for `informant_parley` and adding `QTE` to the forbidden default player-facing technical terms; focused Daren tests still passed `43 passed / 0 failed / 0 skipped`, the affected slice passed `312 passed / 0 failed / 0 skipped`, `git diff --check` passed, and the production added-line static scan returned `NO_MATCHES`.
- [ ] T013 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T014 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T015 Comment closure evidence on #970, close as completed, confirm issue state closed.
- [ ] T016 Clean up worktree/branches and report next target.
