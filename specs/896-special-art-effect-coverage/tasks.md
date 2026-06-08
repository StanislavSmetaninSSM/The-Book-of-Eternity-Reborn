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

- [ ] Add a focused failing test/source guard in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` or the nearest existing docs/example guard file.
- [ ] The RED guard must prove examples require at least one player-owned learned Predvechnye Guardian special art whose unique combat effect changes the tactical/narrative result beyond the base operation.
- [ ] The RED guard must prove examples require at least one non-player Guardian/opposition special art with `specialArtAudit.effectNote` referencing the unique combat effect.
- [ ] The RED guard must require at least two #894 Guardian art names from the Saref/Predvechnye set, preferably different base operations.
- [ ] Run the focused new test before prose/example edits and record exact failure count/reason.

## T003 — Worked examples and GM-facing docs

- [ ] Update `Examples/E_CLI_Afterlife_Turns.txt` with a player-owned learned special-art conflict example using a #894 Guardian art and an explicit unique effect delta.
- [ ] Update `Examples/E_CLI_Afterlife_Turns.txt` with a non-player Guardian/opposition special-art conflict example whose `specialArtAudit.effectNote` names the unique combat effect.
- [ ] Include at least two #894 Guardian arts in the examples; prefer different base operations such as one `binding`/`break_binding` and one `guard`/`maneuver`/`pressure` art.
- [ ] Ensure examples use existing #897 `specialArts[].combatEffect` structure and #898 legal axes/payoffs without inventing new runtime fields.
- [ ] Update `OtherGuides/Afterlife_Combat_Terminology_Glossary.md` and/or `OtherGuides/Afterlife_Contract_Matrix.md` where current docs define special-art audit/example requirements.
- [ ] Update `Examples/example_validation_manifest.json` only if the new/changed example section needs manifest coverage.

## T004 — GREEN verification and Spec Kit reconciliation

- [ ] Run the new focused coverage test and record exact counts.
- [ ] Run `AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests|SystemGuardianLibraryServiceTests` and record exact counts.
- [ ] Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` and record warnings/errors.
- [ ] Run `git diff --check origin/main...HEAD` and record result.
- [ ] Run the added-line static scan excluding `specs/**`; inspect/report any matches.
- [ ] Update this tasks file with RED/GREEN/final verification evidence before PR if commands/counts change.
- [ ] Check that no run artifacts, `bin/`, `obj/`, `node_modules/`, `.hermes/`, `.review-*`, or test output artifacts are staged.

## T005 — Independent review / PR / merge / closure owned by Hermes

- [ ] Independent review approves final diff or all Critical/Important findings are fixed and re-reviewed.
- [ ] Create PR with local verification evidence. PR body should close #896 only.
- [ ] Squash-merge after local gates and review approval.
- [ ] Verify #896 is `CLOSED` / `COMPLETED` only after merge to `main`.
- [ ] Post final Russian closure report with verification evidence, review summary, docs impact, Spec Kit reconciliation, and next target.

## Verification Evidence

- Baseline focused docs/Guardian gate before implementation: 116 passed, 0 failed, 0 skipped.
