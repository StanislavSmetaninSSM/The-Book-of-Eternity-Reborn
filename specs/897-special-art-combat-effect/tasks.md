# Tasks: Structured special-art combatEffect (#897)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/897

## T001 — Spec Kit preflight and branch discovery

- [x] Create `specs/897-special-art-combat-effect/` for GitHub issue #897.
- [x] Link #897 in `spec.md`, `plan.md`, `tasks.md`, and `contracts/special-art-combat-effect.md`.
- [x] Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from branch `codex/897-special-art-combat-effect` and confirm `FEATURE_DIR` points to `specs/897-special-art-combat-effect`.

## T002 — Baseline and pattern inspection

- [x] Run baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeSpiritualConflict|FullyQualifiedName~AfterlifeEntityProfiles|FullyQualifiedName~ExplorerAfterlife|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 485 passed, 0 failed, 0 skipped.
- [x] Inspect existing afterlife entity-profile validation, spiritual-conflict special-art audit validation, `/spiritual_arts` and `/afterlife_profiles` output builders, docs coverage tests, and worked examples before editing.
- [x] Identify exact focused test classes and filters for RED/GREEN validation, rendering, and docs/source guards.

## T003 — Validation contract TDD

- [x] RED: add tests proving a current teachable special art with complete `combatEffect` is accepted and missing `combatEffect` is rejected/flagged where the current contract applies.
- [x] RED: add tests rejecting empty/generic `combatEffect.summary`, missing required `trigger`/`mechanicalAxis`/`allowedPayoff`/`limit`/`auditRequirement`, unsupported axes, and passive/unlimited/bypass wording where validation can reasonably catch it.
- [x] RED: add or adjust compatibility test proving legacy profiles with only `effectSummary` remain loadable/readable according to the documented compatibility rule.
- [x] GREEN: implement minimal validation in the existing afterlife validation layer without altering Mortal item `combatEffect` semantics.
- [x] GREEN: rerun focused validation tests and record exact counts in this file.

## T004 — Player-facing display TDD

- [x] RED: add tests for `/spiritual_arts`, `/afterlife_profiles`, or the shared command-result path that currently renders visible `specialArts[]`, asserting player-facing combat-effect summary/trigger/payoff/limit text appears.
- [x] RED: add tests proving raw JSON, debug/API terms, hidden/private/GM-only fields, and unrevealed Saref/Wings spoilers do not leak through default output.
- [x] GREEN: implement minimal rendering using existing console/browser shared output patterns; keep React presentation-only unless existing C# DTO metadata must be exposed.
- [x] GREEN: rerun focused output tests and record exact counts in this file.

## T005 — GM prompts/docs/examples synchronization

- [x] RED: add/update documentation coverage or source-guard tests requiring `specialArts[].combatEffect`, legal axes, base-operation preservation, and `specialArtAudit.effectNote` guidance.
- [x] Update `CLI_Agent_Daemon_Specification.md`, `OtherGuides/Afterlife_Contract_Matrix.md`, `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, and `BookOfEternityClient/game_master_daemon.ps1` if it duplicates the active GM afterlife spiritual-conflict prompt.
- [x] Update `Examples/E_CLI_Afterlife_Turns.txt` with one player-owned learned special art and one non-player Guardian/opposition special art using `combatEffect`, legal payoff axes, multiplied ОД where required, and concrete `specialArtAudit.effectNote`.
- [x] Update `Examples/example_validation_manifest.json` if the example-validation manifest needs new/changed entries.
- [x] GREEN: run `AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests` and record exact counts in this file.

## T006 — Spec Kit evidence reconciliation

- [x] Update `contracts/special-art-combat-effect.md` if implementation chooses final field names or nested payoff structure different from the initial contract. No update required; implementation kept the planned field names and compatibility rule.
- [x] Update `spec.md` and `plan.md` if accepted implementation reality changes requirements, compatibility behavior, or verification commands. No update required; no requirement/compatibility drift found.
- [x] Check off completed tasks only after diff and verification evidence exists.

## T007 — Final verification before PR

- [x] Run focused special-art `combatEffect` validation/output tests and record exact counts.
- [x] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` and record exact counts.
- [x] Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` and record warnings/errors.
- [x] Run `git diff --check origin/main...HEAD` and record result.
- [x] Run added-line static scan excluding `specs/**` and inspect/report any matches.
- [x] Ensure no run artifacts, `bin/`, `obj/`, `node_modules/`, `.hermes/` scratch, or test output artifacts are staged.

## T008 — Review / PR / closure owned by Hermes

- [ ] Independent review approves the final diff or all Critical/Important findings are fixed and re-reviewed.
- [ ] Create PR with local verification evidence and `GitHub Actions: not used/not required`.
- [ ] Squash-merge after local gates and review approval.
- [ ] Verify #897 is `CLOSED` / `COMPLETED` only after merge to `main`.
- [ ] Post final Russian closure report.

## Verification Evidence

- Spec Kit prerequisite check: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` -> `FEATURE_DIR=E:\\Games\\worktrees\\boe-897-special-art-combat-effect\\specs\\897-special-art-combat-effect`, `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- Baseline before implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeSpiritualConflict|FullyQualifiedName~AfterlifeEntityProfiles|FullyQualifiedName~ExplorerAfterlife|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 485 passed, 0 failed, 0 skipped.
- RED focused suite after tests/docs guards were added and before implementation/docs fixes: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~CurrentTeachableSpecialArtRequiresCombatEffect|FullyQualifiedName~CurrentTeachableSpecialArtAcceptsMeaningfulCombatEffect|FullyQualifiedName~CurrentSpecialArtRejectsInvalidCombatEffect|FullyQualifiedName~LegacyPersistedSpecialArtWithoutCombatEffect|FullyQualifiedName~TryProcessCommand_AfterlifeProfiles_RendersFullEntityProfile|FullyQualifiedName~TryProcessCommand_SpiritualArts_ShowsLearnedSpecialArtBaseAction|FullyQualifiedName~ExecuteAsync_AfterlifeSpecialArtSurfaces_ShowCombatEffectWithoutRawContractLeak|FullyQualifiedName~AfterlifeSpecialArtCombatEffectContractIsDocumentedForGm" --logger "console;verbosity=minimal"` -> 11 total, 1 passed, 10 failed, 0 skipped. Failures covered missing current-contract validation, missing player-facing combat-effect text, missing shared/browser DTO text, and missing GM docs/source-guard coverage.
- GREEN focused suite after implementation/docs/examples: same focused filter -> 11 passed, 0 failed, 0 skipped.
- Required docs/examples suite: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 101 passed, 0 failed, 0 skipped.
- Broader afterlife regression filter: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeSpiritualConflict|FullyQualifiedName~AfterlifeEntityProfiles|FullyQualifiedName~ExplorerAfterlife|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 486 passed, 0 failed, 0 skipped.
- Build: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` -> succeeded, 0 warnings, 0 errors.
- Diff hygiene: `git diff --check origin/main...HEAD` -> no output / passed.
- Added-line static scan excluding `specs/**` for hardcoded secrets, shell execution, standalone eval/exec, unsafe pickle, and SQL string-formatting -> no matches.
