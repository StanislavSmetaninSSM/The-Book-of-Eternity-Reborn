# Tasks: Quest Reward Detail Authority

**Source Issue**: [#860](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/860)
**Spec**: `specs/860-quest-reward-authority/spec.md`
**Plan**: `specs/860-quest-reward-authority/plan.md`

## Phase 1: Exploration and Baseline

- [X] Inspect `AGENTS.md` and `.specify/memory/constitution.md` for issue/spec/validation/docs guardrails.
- [X] Inspect issue #860 and parent #857 summary/detail authority requirements.
- [X] Create isolated worktree `E:/Games/worktrees/boe-860-quest-reward-authority` on `fix/860-quest-reward-authority` from `origin/main`.
- [X] Inspect current quest reward validation under `BookOfEternityClient/Services/Validation/ValidationService.QuestsAndSoulState.cs`.
- [X] Inspect current `/квесты` reward rendering under `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.QuestsAndRivals.cs`.
- [X] Record baseline focused tests in `plan.md`.

## Phase 2: Regression Tests (TDD RED)

- [X] Add validation regression coverage for a `questRewards[].itemsReceived[]` item id that resolves to current inventory/detail authority; verify the test is meaningful.
- [X] Add validation regression coverage for a missing/unresolved `itemsReceived[]` item id with no historical reason; verify RED with missing item authority issue.
- [X] Add validation regression coverage for a `skillsUnlocked[]` id that resolves to active/passive skill state or skill history; verify the test is meaningful.
- [X] Add validation regression coverage for a missing/unresolved `skillsUnlocked[]` id with no historical reason; verify RED with missing skill authority issue.
- [X] Add validation regression coverage for a `relationshipChanges[]` record that resolves to NPC/relationship state/history; verify the test is meaningful.
- [X] Add validation regression coverage for a missing/unresolved `relationshipChanges[]` record with no historical reason; verify RED with missing relationship authority issue.
- [X] Add validation regression coverage for explicit historical-only/unavailable item, skill, and relationship reward records with player-facing reasons; verify these do not report missing-authority issues.
- [X] Add command rendering regression coverage proving `/квесты` shows resolved labels or historical/unavailable reasons instead of raw orphan ids.
- [X] Add documentation/source-guard coverage requiring GM-facing quest reward authority guidance and a worked example.

## Phase 3: Minimal Implementation (GREEN)

- [X] Implement scoped quest reward authority collection for legacy strings and structured reward objects.
- [X] Implement item reward resolution against canonical inventory/detail state and explicit historical/unavailable records.
- [X] Implement skill reward resolution against active/passive skill state, skill history/detail state, and explicit historical/unavailable records.
- [X] Implement relationship reward resolution against NPC/actor relationship state/history and explicit historical/unavailable records.
- [X] Emit stable, surface-specific validation issue codes for unresolved item, skill, relationship rewards, and missing historical reasons.
- [X] Update `/квесты` rendering to use player labels and historical/unavailable reasons without exposing raw file/API/validator language.
- [X] Keep legacy bare strings accepted only when they resolve to detail authority.

## Phase 4: Docs / Contract Reconciliation

- [X] Update GM-facing quest history/reward docs to describe resolvable reward references and explicit historical/unavailable reward records.
- [X] Update at least one worked example for `questRewards` with item, skill, and relationship reward authority.
- [X] Update manifests/source-guard tests if examples or required docs strings change.
- [X] If a broader summary/detail gap is discovered outside #860, create a separate follow-up issue instead of expanding this PR. No separate #857 gap was discovered during this scoped change.

## Phase 5: Verification and Review Prep

- [X] Run focused quest/validation/ExplorerMode tests and record exact pass/fail/skip counts.
- [X] Run docs/contract tests and record exact pass/fail/skip counts.
- [X] Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and record warnings/errors.
- [X] Run `git diff --check origin/main...HEAD`.
- [X] Run added-line static security scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting.
- [X] Run Spec Kit prerequisite/consistency check or equivalent manual reconciliation that spec, plan, tasks, docs, tests, and diff align.
- [X] Obtain independent review before PR/merge; fix Critical/Important findings.
- [ ] Create PR, squash-merge after local verification, and verify issue #860 closes.

## Phase 6: Independent Review Reconciliation

- [X] Evaluate review finding that numeric historical/unavailable `reason` values were accepted as player-facing explanations.
- [X] Add regression coverage for numeric `reason` on a historical quest reward record.
- [X] Restrict reason-field reading to JSON strings so numeric/bool scalars cannot satisfy `quest_reward_history_reason_missing`.
- [X] Re-run focused quest reward authority gates after the review fix.
- [X] Re-run independent review or equivalent review reconciliation before PR/merge.

## Verification Evidence

- Baseline before implementation:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Quest|FullyQualifiedName~Validation|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 1596/1596, 0 failed, 0 skipped.
- TDD RED:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QuestRewardAuthority|HistoryRewardsRenderLabels|QuestRewardAuthorityContract" --logger "console;verbosity=minimal"`: failed 6, passed 4, skipped 0, total 10. Failures covered missing item/skill/relationship authority issue codes, missing historical reason issue, `/квесты` raw structured reward JSON/id leakage, and missing worked example docs file.
- Focused GREEN:
  - Same focused command after implementation: passed 10/10, 0 failed, 0 skipped.
- Final verification before commit:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Quest|FullyQualifiedName~Validation|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 1606/1606, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|Documentation|PromptDocumentationCoverageTests" --logger "console;verbosity=minimal"`: passed 113/113, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: succeeded, 0 warnings, 0 errors.
  - `npm ci --prefix BookOfEternityClient.WebFrontend`: succeeded, 0 vulnerabilities; generated ignored `node_modules/`.
  - `npm run verify --prefix BookOfEternityClient.WebFrontend`: succeeded; vitest passed 23/23 and generated ignored `dist/` for built-frontend smoke prerequisites.
  - Broad C# suite after frontend dist generation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --logger "console;verbosity=minimal"` passed 3375/3375, 0 failed, 0 skipped.
  - Working-tree `git diff --check`: passed; Git reported line-ending normalization warnings only.
  - Working-tree added-line security scan: `NO_MATCHES`.
- Pending by instruction:
  - Post-commit `git diff --check origin/main...HEAD`: passed.
  - Post-commit added-line security scan from the implementation prompt: false-positive on raw scan text in `plan.md`, then passed with `NO_MATCHES` after removing the raw regex from Spec Kit evidence.
  - Post-commit Spec Kit prerequisite check: returned active `specs/860-quest-reward-authority` feature directory with `tasks.md` available.
  - Independent review found one Important issue: numeric JSON scalar `reason` values could satisfy historical/unavailable player-facing reason checks.
- Review fix verification:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QuestRewardAuthority|HistoryRewardsRenderLabels|QuestRewardAuthorityContract" --logger "console;verbosity=minimal"`: passed 11/11, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Quest|FullyQualifiedName~Validation|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 1607/1607, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|Documentation|PromptDocumentationCoverageTests" --logger "console;verbosity=minimal"`: passed 113/113, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: succeeded, 0 warnings, 0 errors.
  - `git diff --check origin/main...HEAD` and working-tree `git diff --check`: passed; Git reported line-ending normalization warnings only.
  - Spec Kit prerequisite check: returned active `specs/860-quest-reward-authority` feature directory with `tasks.md` available.
  - Added-line security scan: `NO_MATCHES`.
  - Independent re-review after amend: `APPROVED`; Critical/Important/Minor none; safe to merge yes. It confirmed HEAD `977f7a3dc13fe814954a272150c28c41ab1d85a2`, focused 11/11, broad affected 1607/1607, docs 113/113, build 0 warnings/errors, `git diff --check` passed, and Spec Kit prerequisite resolved `specs/860-quest-reward-authority`.
