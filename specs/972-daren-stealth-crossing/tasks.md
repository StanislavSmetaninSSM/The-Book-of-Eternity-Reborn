# Tasks: Daren Scene 04 Full Literary Page

**Input**: `specs/972-daren-stealth-crossing/spec.md`, `plan.md`, issue #972 body, parent #955 comments, existing Daren specs #956-#961, completed scenes #969-#971.
**Source Issues**: [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## TDD / Implementation

- [X] T001 Confirm baseline status on branch `work/972-daren-stealth-crossing` and record focused Daren test count.
- [X] T002 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `stealth_crossing` scene.
- [X] T003 Run focused Daren tests and record RED evidence showing the new #972 guard fails for the expected reason.
- [X] T004 Replace only the `stealth_crossing` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a substantial Russian dark-fantasy gallery stealth page.
- [X] T005 Run focused Daren tests and record GREEN evidence.
- [X] T006 Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.
- [X] T007 Run client/test-project build and `git diff --check origin/main...HEAD`.
- [X] T008 Run added-line static scan excluding specs/tests/docs and record `NO_MATCHES` or findings.

## Scope / Spec Kit Reconciliation

- [X] T009 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T010 Verify `specs/972-daren-stealth-crossing/` references #972/#955 and that tasks/checklists match the final diff.
- [X] T011 Verify #973-#983 and parent #955 remain out of scope.

## Hermes-Owned Lifecycle

- [X] T012 Independent review of #972 implementation and literary quality bar.
- [X] T013 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T014 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T015 Comment closure evidence on #972, close as completed, confirm issue state closed.
- [ ] T016 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Baseline focused Daren filter before edits: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` -> 44 passed, 0 failed, 0 skipped, 44 total.
- T002: Added `DarenStealthCrossing_ReadsAsGalleryStealthPageWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; it asserts shared title/narrative parity, `StealthNoise` mechanics/config/routing/score invariants, grouped motif coverage, substantial length, Daren POV, and no player-facing technical terms.
- T003 RED: Focused Daren filter after adding the guard and before rewriting prose -> 44 passed, 1 failed, 0 skipped, 45 total. Expected failure: `Daren stealth_crossing narrative should be a substantial gallery stealth page, not a compact synopsis.`
- T004: Replaced only the shared C# `stealth_crossing`/`Галерея без звука` narrative page in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; the action and route data were not intentionally changed.
- T005 GREEN: Focused Daren filter after the rewrite -> 45 passed, 0 failed, 0 skipped, 45 total.
- T006: Affected Daren/QTE/docs/browser C# slice -> 314 passed, 0 failed, 0 skipped, 314 total.
- T007: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` -> succeeded with 0 warnings, 0 errors. `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` -> succeeded with 0 warnings, 0 errors. Post-commit `git diff --check origin/main...HEAD` -> passed with no findings.
- T008: Added-line static scan for production `BookOfEternityClient` diff against forbidden player-facing implementation terms -> `NO_MATCHES`.
- T009: Current diff touches `BookOfEternityClient/Services/QteSceneService.Daren.cs` narrative text and `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` guard/helper only; no frontend/browser files, endpoints, runtime state, reward/profile/New Game grant files, or route mechanic values changed.
- T010: `spec.md`, `plan.md`, `tasks.md`, and `contracts/daren-scene-page.md` reference #972/#955. `requirements.md` is 12/13 complete, leaving only Hermes-owned independent review before PR/merge unchecked.
- T011: No lifecycle actions were taken; #973-#983 are not edited or implemented by this diff, and parent #955 remains out of scope.
- T012: Independent Codex review in detached worktree `E:/Games/worktrees/boe-972-daren-stealth-crossing-review` returned `APPROVED` with no Critical, Important, or Minor findings.
- T013: PR #987 was created with local verification evidence, `Closes #972`, and explicit scope boundary that #955 remains open while #973-#983 remain separate tasks.
