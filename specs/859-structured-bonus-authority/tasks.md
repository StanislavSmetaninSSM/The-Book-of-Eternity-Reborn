# Tasks: Structured Authority for Mechanical Inventory Bonus Summaries

**Source Issue**: [#859](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/859)
**Spec**: `specs/859-structured-bonus-authority/spec.md`
**Plan**: `specs/859-structured-bonus-authority/plan.md`

## Phase 1: Exploration and Baseline

- [X] Inspect existing inventory item validation under `BookOfEternityClient/Services/Validation/` and choose the smallest partial/helper location for mechanical bonus authority checks.
- [X] Inspect `BookOfEternityClient/Services/CharacteristicsService.cs` so validation stays aligned with structured mechanics authority and does not parse free-text summaries.
- [X] Inspect inventory detail rendering under `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs` to confirm player-visible mechanical summaries remain inspectable or explicitly unresolved.
- [X] Inspect GM-facing item bonus/effect docs in `Rules/Block_10.txt` and relevant `Examples/` before changing prompts/examples.
- [X] Record focused baseline counts from the pre-implementation command in `plan.md`.

## Phase 2: Regression Tests (TDD RED)

- [X] Add validation regression coverage for a stat-like bonus summary (for example `Сила +2` or `Скрытность +1`) with empty `structuredBonuses`; verify current validation accepts it and the new test fails.
- [X] Add validation regression coverage for a skill-like bonus summary (for example `Аркановедение +1`) with no structured authority; verify RED.
- [X] Add validation regression coverage for a reputation bonus summary (for example `Репутация среди аристократов +3`) with no structured authority; verify RED.
- [X] Add validation regression coverage for a healing consumable/effect summary (for example `Восстанавливает 15% здоровья`) with no canonical consumable/combat/effect authority; verify RED.
- [X] Add passing coverage for a mechanical-looking summary backed by `structuredBonuses` or other existing canonical authority.
- [X] Add passing coverage for explicit narrative-only text and explicit unresolved/unidentified mechanics reason.
- [X] Add review regression coverage proving unrelated `structuredBonuses`, empty `customProperties`, and near-numeric mismatches such as `Сила +1` vs `Сила +10` do not authorize a mechanical-looking `bonuses` summary; verify RED.
- [X] Add review regression coverage proving inventory detail rendering surfaces `mechanicalSummaryAuthority="Unresolved"` with a player-facing reason; verify RED.
- [X] Add documentation guard coverage requiring GM docs/examples to describe matching structured authority; verify RED.

## Phase 3: Minimal Implementation (GREEN)

- [X] Implement conservative mechanical-looking summary detection for numeric modifiers, percentages, healing, damage, reputation, duration, condition, and activated-action wording in inventory `bonuses` and comparable effect summary fields.
- [X] Implement authority checks for matching, meaningful `structuredBonuses`, `combatEffect`, and existing canonical consumable/effect fields used by the repository.
- [X] Implement explicit narrative-only / flavor-only classification support using a GM-authorable field documented in Phase 4.
- [X] Implement explicit unresolved/unidentified/sealed mechanical reason support so validation can accept known unknowns without implying mechanics are applied.
- [X] Emit validation issue code/message(s) that identify the item and unresolved summary while keeping player-facing surfaces free of raw debug/API phrasing.
- [X] Keep `CharacteristicsService` and inventory rendering behavior scoped: no free-text mechanics parsing and no unrelated UI redesign.
- [X] Render unresolved and narrative-only `bonuses`/`effects` in console inventory details as non-applied player-facing text instead of ordinary bonus/effect lines.

## Phase 4: Docs / Contract Reconciliation

- [X] Update GM-facing inventory item bonus/effect rules to require structured authority for mechanical summaries and document narrative-only/unresolved exceptions.
- [X] Update at least one worked example or manifest/source guard proving a GM can author a mechanical item with `bonuses` plus structured authority and a narrative-only or unresolved exception.
- [X] If no suitable example surface exists, create a tracked follow-up issue and cite it in PR/issue notes instead of leaving the contract undocumented.

## Phase 5: Verification and Review Prep

- [X] Run focused new tests and confirm non-zero pass counts.
- [X] Run focused validation tests with `-p:IsTestProject=true`.
- [X] Run `CharacteristicsServiceTests` and neighboring inventory/ExplorerMode command tests with `-p:IsTestProject=true`.
- [X] Run docs/contract tests if documentation/prompts/examples changed.
- [X] Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- [X] Run `git diff --check origin/main...HEAD`.
- [X] Run added-line static security scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting.
- [X] Reconcile this Spec Kit feature; mark tasks complete only after implementation and verification evidence exist.

## Verification Evidence

- Baseline before implementation:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Validation|FullyQualifiedName~CharacteristicsServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 1223/1223, 0 failed, 0 skipped.
- TDD RED:
  - Initial focused run exposed a duplicate `isConsumption` key in the healing fixture; the fixture was corrected before production code changes.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~MechanicalBonusAuthorityValidationTests" --logger "console;verbosity=minimal"`: expected RED after fixture correction, failed 4, passed 3, skipped 0, total 7. Failures were missing `inventory_mechanical_summary_missing_structured_authority` reports for mechanical `bonuses`/`effects`.
  - Review follow-up RED for matching authority loopholes with the same `MechanicalBonusAuthorityValidationTests` command: failed 2, passed 8, skipped 0, total 10. Failures were missing `inventory_mechanical_summary_missing_structured_authority` reports when `bonuses=["Скрытность +1"]` had unrelated `structuredBonuses` or `customProperties: [{}]`.
  - Re-review follow-up RED for near-numeric summary text mismatch with the same `MechanicalBonusAuthorityValidationTests` command: failed 1, passed 10, skipped 0, total 11. Failure was missing `inventory_mechanical_summary_missing_structured_authority` when `bonuses=["Сила +1"]` had `structuredBonuses.description="Сила +10"` and `value=10`.
  - Review follow-up RED for console rendering: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~TryProcessCommand_InventoryDetail_UnresolvedMechanicalSummaryShowsReason" --logger "console;verbosity=minimal"` failed 1, passed 0, skipped 0, total 1 because the rendered detail did not contain `Механика не раскрыта`.
  - Review follow-up RED for docs: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~PromptDocumentationCoverageTests.InventoryMechanicalBonusAuthorityContract_IsDocumentedForGm" --logger "console;verbosity=minimal"` failed 1, passed 0, skipped 0, total 1 because `matching structured authority` was not documented.
- Focused GREEN:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~MechanicalBonusAuthorityValidationTests" --logger "console;verbosity=minimal"`: passed 11/11, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~TryProcessCommand_InventoryDetail_UnresolvedMechanicalSummaryShowsReason" --logger "console;verbosity=minimal"`: passed 1/1, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~PromptDocumentationCoverageTests.InventoryMechanicalBonusAuthorityContract_IsDocumentedForGm" --logger "console;verbosity=minimal"`: passed 1/1, 0 failed, 0 skipped.
- Final verification:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~MechanicalBonus|FullyQualifiedName~StructuredBonus|FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"`: passed 78/78, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"`: passed 944/944, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~CharacteristicsServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 292/292, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|Documentation|PromptDocumentationCoverageTests" --logger "console;verbosity=minimal"`: passed 112/112, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: succeeded with 0 warnings, 0 errors.
  - `git diff --check`: passed with no whitespace errors; Git printed CRLF normalization warnings only.
  - Added-line static scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting: no matches after inspecting and removing noisy evidence wording.
  - `check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`: passed and returned the active `specs/859-structured-bonus-authority` feature directory with `tasks.md`.
