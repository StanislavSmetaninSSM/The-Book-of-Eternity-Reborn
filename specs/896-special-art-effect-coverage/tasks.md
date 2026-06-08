# Tasks: Special-art combat-effect examples and regression coverage (#896)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/896
**Spec:** `specs/896-special-art-effect-coverage/spec.md`
**Plan:** `specs/896-special-art-effect-coverage/plan.md`

## T001 — Preflight / dependency and baseline

- [x] Confirm #898, #897, #895, and #894 are closed on `main` before starting #896.
- [x] Inspect #896 body/comments and confirm it is the next dependency after #894.
- [x] Create isolated ASCII worktree `E:/Games/worktrees/boe-896-special-art-coverage` on branch `codex/896-special-art-coverage` from `origin/main`.
- [x] Run baseline focused docs/Guardian gate before implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SystemGuardianLibraryServiceTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 116 passed, 0 failed, 0 skipped.
- [x] Run Spec Kit prerequisite discovery after artifacts exist: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` -> `FEATURE_DIR=E:\\Games\\worktrees\\boe-896-special-art-coverage\\specs\\896-special-art-effect-coverage`, `AVAILABLE_DOCS=["tasks.md"]`.

## T002 — RED coverage for special-art examples

- [x] Add a focused failing test/source guard in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` or the nearest existing docs/example guard file.
- [x] The RED guard must prove examples require at least one player-owned learned Predvechnye Guardian special art whose unique combat effect changes the tactical/narrative result beyond the base operation.
- [x] The RED guard must prove examples require at least one non-player Guardian/opposition special art with `specialArtAudit.effectNote` referencing the unique combat effect.
- [x] The RED guard must require at least two #894 Guardian art names from the Saref/Predvechnye set, preferably different base operations.
- [x] Run the focused new test before prose/example edits and record exact failure count/reason.

## T003 — Worked examples and GM-facing docs

- [x] Update `Examples/E_CLI_Afterlife_Turns.txt` with a player-owned learned special-art conflict example using a #894 Guardian art and an explicit unique effect delta.
- [x] Update `Examples/E_CLI_Afterlife_Turns.txt` with a non-player Guardian/opposition special-art conflict example whose `specialArtAudit.effectNote` names the unique combat effect.
- [x] Include at least two #894 Guardian arts in the examples; prefer different base operations such as one `binding`/`break_binding` and one `guard`/`maneuver`/`pressure` art.
- [x] Ensure examples use existing #897 `specialArts[].combatEffect` structure and #898 legal axes/payoffs without inventing new runtime fields.
- [x] Update `OtherGuides/Afterlife_Combat_Terminology_Glossary.md` and/or `OtherGuides/Afterlife_Contract_Matrix.md` where current docs define special-art audit/example requirements.
- [x] Update `Examples/example_validation_manifest.json` only if the new/changed example section needs manifest coverage.

## T004 — GREEN verification and Spec Kit reconciliation

- [x] Run the new focused coverage test and record exact counts.
- [x] Run `AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests|SystemGuardianLibraryServiceTests` and record exact counts.
- [x] Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` and record warnings/errors.
- [x] Run `git diff --check origin/main...HEAD` and record result.
- [x] Run the added-line static scan excluding `specs/**`; inspect/report any matches.
- [x] Update this tasks file with RED/GREEN/final verification evidence before PR if commands/counts change.
- [x] Check that no run artifacts, `bin/`, `obj/`, `node_modules/`, `.hermes/`, `.review-*`, or test output artifacts are staged.

## T005 — Independent review / PR / merge / closure owned by Hermes

- [ ] Independent review approves final diff or all Critical/Important findings are fixed and re-reviewed.
- [ ] Create PR with local verification evidence. PR body should close #896 only.
- [ ] Squash-merge after local gates and review approval.
- [ ] Verify #896 is `CLOSED` / `COMPLETED` only after merge to `main`.
- [ ] Post final Russian closure report with verification evidence, review summary, docs impact, Spec Kit reconciliation, and next target.

## Verification Evidence

- Baseline focused docs/Guardian gate before implementation: 116 passed, 0 failed, 0 skipped.
- RED focused guard before prose/example edits: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~PredvechnyeSpecialArtCombatEffectExamplesStayCombatActionable" --logger "console;verbosity=minimal"` -> failed as expected: 1 failed, 0 passed, 0 skipped; reason `Missing section start marker: #896 Predvechnye special-art combat-effect coverage start`.
- GREEN focused guard after examples/docs update: same command -> 1 passed, 0 failed, 0 skipped.
- GREEN focused docs/example/Guardian gate: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~SystemGuardianLibraryServiceTests" --logger "console;verbosity=minimal"` -> 117 passed, 0 failed, 0 skipped.
- Build gate: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` -> succeeded with 0 warnings, 0 errors.
- Diff hygiene note: first `git diff --check origin/main...HEAD` run failed on trailing whitespace from the active #896 Spec Kit planning files; metadata line trailing spaces were removed in the implementation commit and the gate must be rerun.
- Final sequential test rerun after commit: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~SystemGuardianLibraryServiceTests" --logger "console;verbosity=minimal"` -> 117 passed, 0 failed, 0 skipped. A prior parallel test/build attempt produced `CS2012` file-lock output because both .NET commands wrote shared `obj` files at the same time; rerunning sequentially resolved it.
- Final sequential build rerun after commit: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` -> succeeded with 0 warnings, 0 errors.
- Final diff hygiene gate after commit: `git diff --check origin/main...HEAD` -> exit 0, no output.
- Final added-line static scan excluding `specs/**`: `git diff origin/main...HEAD -- . ':(exclude)specs/**' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES` -> `NO_MATCHES`.
- Staging hygiene: `git status --short --branch` before commit showed only intended #896 docs/tests/spec files; no run artifacts were staged.
