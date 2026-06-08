# Tasks: Afterlife combatConditions layer (#898)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/898

## T001 — Spec Kit preflight and branch discovery

- [x] Create `specs/898-combat-conditions/` for GitHub issue #898.
- [x] Link #898 in `spec.md`, `plan.md`, and `tasks.md`.
- [x] Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from branch `codex/898-combat-conditions` and confirm `FEATURE_DIR` points to `specs/898-combat-conditions`.

## T002 — Baseline and pattern inspection

- [x] Inspect existing afterlife spiritual-conflict validation, entity profile validation, command result builders, docs coverage tests, and examples before editing.
- [x] Run a focused baseline command for afterlife validation/docs surfaces and record exact counts or the exact baseline blocker.
- [x] Identify the smallest existing test classes where combat-condition RED tests belong.

## T003 — Validation contract TDD

- [x] RED: add tests showing `combatConditions[]` is optional for backward compatibility, but present active entries require `conditionId`, `displayName`, `kind`, `source`, `targetSide`, `mechanicalAxis`, `duration`, `summary`, and `counterplay`.
- [x] RED: add tests rejecting unsupported `kind`, unsupported `mechanicalAxis`, missing/empty `counterplay`, stale active expired/consumed entries, illegal operation-to-payoff mappings, and roll/advantage/disadvantage sources that reference missing active conditions.
- [x] GREEN: implement minimal validation in the existing afterlife validation layer.
- [x] GREEN: rerun focused validation tests and record exact counts.

## T004 — Player-facing display TDD

- [x] RED: add tests for visible active condition output including name, kind, source, target, affected operations, duration/remaining uses, counterplay, and summary.
- [x] RED: add tests proving hidden/GM-only/non-player-visible conditions do not leak to ordinary player-facing output.
- [x] GREEN: implement minimal console/shared output rendering using existing afterlife command-result patterns.
- [x] GREEN: rerun focused UI/output tests and record exact counts.

## T005 — GM prompt/docs/examples synchronization

- [x] RED: add or update documentation coverage/source guard tests requiring `combatConditions`, five kinds (`mark`, `ward`, `burden`, `opening`, `vow`), lifecycle/audit rules, legal mechanical axes, and no generic passive stat stacking.
- [x] Update `CLI_Agent_Daemon_Specification.md`, `OtherGuides/Afterlife_Contract_Matrix.md`, `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, and any active daemon/task guide duplicates with GM instructions.
- [x] Update `Examples/E_CLI_Afterlife_Turns.txt` with worked examples for mark, ward, burden, opening, vow, consumption/expiration/counterplay, and at least two Guardian/Saref-linked non-spoiler examples.
- [x] Update `Examples/example_validation_manifest.json` if needed so example validation covers the new worked examples.
- [x] GREEN: run `AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests` and record exact counts.

## T006 — Spec Kit evidence reconciliation

- [x] Update `contracts/combat-conditions.md` if implementation chooses final field names that differ from the initial model.
- [x] Update `spec.md`/`plan.md` only if accepted implementation reality changes requirements or scope.
- [x] Check off completed tasks only after diff and verification evidence exists.

## T007 — Final verification before PR

- [x] Run focused combat-condition validation/UI tests and record exact counts.
- [x] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` and record exact counts.
- [x] Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` and record warnings/errors.
- [x] Run `git diff --check origin/main...HEAD` and record result.
- [x] Run added-line static scan excluding `specs/**` and inspect/report any matches.
- [x] Ensure no run artifacts, `bin/`, `obj/`, `node_modules/`, or scratch files are staged.

## Verification Evidence

- Spec Kit preflight: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` -> `FEATURE_DIR=E:\Games\worktrees\boe-898-combat-conditions\specs\898-combat-conditions`, `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- Baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~AfterlifeSpiritualConflict|FullyQualifiedName~AfterlifeEntityProfiles|FullyQualifiedName~ExplorerAfterlife" --logger "console;verbosity=minimal"` -> 469 passed, 0 failed, 0 skipped.
- RED #1: new validation/UI/docs filter before implementation -> 7 failed, 0 passed, 0 skipped. Expected failures were missing `combatConditions` docs, missing visible UI block, and missing `afterlife_combat_condition_*` validation issues.
- RED #2: canonical alias and consumed-condition roll-source guard before alias fix -> 2 failed, 0 passed, 0 skipped. Expected failures were draft-only field requirements and consumed condition still accepted as a roll source.
- Review RED #1: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ValidateGameStateAsync_RollModeNonCombatConditionSourceWithSourceId_RemainsValid|FullyQualifiedName~ValidateGameStateAsync_MalformedActiveCombatCondition_IsRejected|FullyQualifiedName~ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes" --logger "console;verbosity=minimal"` -> 7 failed, 0 passed, 0 skipped. Expected failures were unwanted combat-condition roll-source validation for `sourceType=guard_tempo_window`, missing active-condition shape errors, and browser DTO leakage of `concealed` combatConditions.
- Re-review RED #2: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes|FullyQualifiedName~TryProcessCommand_SpiritualCombatLog_ShowsExchangeAndRecentConflictAudit" --logger "console;verbosity=minimal"` -> 2 failed, 0 passed, 0 skipped. Expected failures were hidden/gm_only/concealed/spoiler combat-condition ids and summaries leaking through `diceAudit.rollMode.*.advantageSources[]` / `disadvantageSources[]` in browser raw DTO JSON and console dice source text.
- Review focused GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ValidateGameStateAsync_RollModeNonCombatConditionSourceWithSourceId_RemainsValid|FullyQualifiedName~ValidateGameStateAsync_MalformedActiveCombatCondition_IsRejected|FullyQualifiedName~ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes" --logger "console;verbosity=minimal"` -> 7 passed, 0 failed, 0 skipped.
- Re-review focused GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes|FullyQualifiedName~TryProcessCommand_SpiritualCombatLog_ShowsExchangeAndRecentConflictAudit" --logger "console;verbosity=minimal"` -> 2 passed, 0 failed, 0 skipped.
- Re-review RED #3: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes|FullyQualifiedName~TryProcessCommand_SpiritualCombatLog_ShowsExchangeAndRecentConflictAudit" --logger "console;verbosity=minimal"` -> 2 failed, 0 passed, 0 skipped. Expected failures were hidden active combat-condition legacy string sources (`hidden_condition_marker`, hidden summary text, hidden audit text, concealed/spoiler ids) surviving into ordinary browser raw JSON and console dice source text.
- Re-review legacy-string focused GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes|FullyQualifiedName~TryProcessCommand_SpiritualCombatLog_ShowsExchangeAndRecentConflictAudit" --logger "console;verbosity=minimal"` -> 2 passed, 0 failed, 0 skipped.
- Re-review RED #4: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_SpiritualAction_SanitizesActiveCombatConditionRawJson" --logger "console;verbosity=minimal"` -> 1 failed, 0 passed, 0 skipped. Expected failure was hidden active combat-condition marker text leaking through `/spiritual_action` player-facing browser raw JSON.
- Re-review lifecycle focused GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_SpiritualAction_SanitizesActiveCombatConditionRawJson" --logger "console;verbosity=minimal"` -> 1 passed, 0 failed, 0 skipped.
- Re-review raw-output scan: inspected player-facing `AfterlifeSpiritualConflictState.StatePath` raw/audit call sites in `ExplorerLifecycleLocalTurnCommandResultBuilder`, `ExplorerAfterlifeCombatCommandResultBuilder`, `ExplorerMode.Afterlife.SpiritualConflict`, and `ExplorerMode.Afterlife.StatusAudit`; `/spiritual_action` was the only unsanitized active-conflict raw JSON path. `/spiritual_conflict`, `/spiritual_conflict_log`, and afterlife status audit already route through `AfterlifeCombatConditionPlayerAuditSanitizer`.
- Re-review docs/example sync fix: independent re-review found the worked `Examples/E_CLI_Afterlife_Turns.txt` `combatConditions[]` entries omitted `payoff` even though runtime validation requires `payoff.sourceType` and `payoff.effect`; the example now includes explicit `payoff` objects for the mark, ward, burden, opening, and vow entries.
- Focused GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ValidateGameStateAsync_AbsentCombatConditions|FullyQualifiedName~ValidateGameStateAsync_ValidActiveCombatCondition|FullyQualifiedName~ValidateGameStateAsync_ValidCombatConditionCanonicalAliases|FullyQualifiedName~ValidateGameStateAsync_ActiveCombatCondition|FullyQualifiedName~ValidateGameStateAsync_CombatCondition|FullyQualifiedName~ValidateGameStateAsync_RollModeCombatCondition|FullyQualifiedName~ValidateGameStateAsync_RollModeNonCombatConditionSourceWithSourceId|FullyQualifiedName~ValidateGameStateAsync_MalformedActiveCombatCondition|FullyQualifiedName~ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes|FullyQualifiedName~AfterlifeCombatConditionsContractIsDocumentedForGm|FullyQualifiedName~JsonExamples_AreParseableOrExplicitlyExempted|FullyQualifiedName~GameResponseShapedExamples_DoNotUseUnknownTopLevelFields|FullyQualifiedName~AfterlifeWorkedExamplesHaveRuntimeScenarioOrExplicitCoverageExemption" --logger "console;verbosity=minimal"` -> 20 passed, 0 failed, 0 skipped.
- Re-review focused GREEN with lifecycle blocker: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ValidateGameStateAsync_AbsentCombatConditions|FullyQualifiedName~ValidateGameStateAsync_ValidActiveCombatCondition|FullyQualifiedName~ValidateGameStateAsync_ValidCombatConditionCanonicalAliases|FullyQualifiedName~ValidateGameStateAsync_ActiveCombatCondition|FullyQualifiedName~ValidateGameStateAsync_CombatCondition|FullyQualifiedName~ValidateGameStateAsync_RollModeCombatCondition|FullyQualifiedName~ValidateGameStateAsync_RollModeNonCombatConditionSourceWithSourceId|FullyQualifiedName~ValidateGameStateAsync_MalformedActiveCombatCondition|FullyQualifiedName~ExecuteAsync_SpiritualConflict_RendersVisibleCombatConditionsAndSuppressesHiddenOnes|FullyQualifiedName~ExecuteAsync_SpiritualAction_SanitizesActiveCombatConditionRawJson|FullyQualifiedName~AfterlifeCombatConditionsContractIsDocumentedForGm|FullyQualifiedName~JsonExamples_AreParseableOrExplicitlyExempted|FullyQualifiedName~GameResponseShapedExamples_DoNotUseUnknownTopLevelFields|FullyQualifiedName~AfterlifeWorkedExamplesHaveRuntimeScenarioOrExplicitCoverageExemption" --logger "console;verbosity=minimal"` -> 21 passed, 0 failed, 0 skipped.
- Required docs GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 100 passed, 0 failed, 0 skipped.
- Build: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` -> succeeded, 0 warnings, 0 errors.
- Post-review whitespace: `git diff --check origin/main...HEAD` -> exit 0, no output and no whitespace errors.
- Added-line static scan excluding `specs/**` against `origin/main...HEAD`: broad keyword pass produced inspected false positives from domain terms such as `hiddenConditionTokens` and visibility `"secret"`; stricter credential-shaped literal / shell execution / eval-exec / pickle / SQL formatting scan returned `No matches`. No actionable hardcoded secret, shell execution, eval/exec, unsafe pickle, or SQL string-formatting match.

## T008 — Review / PR / closure owned by Hermes

- [ ] Independent review approves the final diff or all Critical/Important findings are fixed and re-reviewed.
- [ ] Create PR with local verification evidence and `GitHub Actions: not used/not required`.
- [ ] Squash-merge after local gates and review approval.
- [ ] Verify #898 is `CLOSED` / `COMPLETED` only after merge to `main`.
- [ ] Post final Russian closure report.
