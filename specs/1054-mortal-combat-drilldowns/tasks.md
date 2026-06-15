---
description: "Task list for #1054 mortal combat read-only detail drill-downs"
---

# Tasks: Mortal Combat Read-Only Detail Drill-Downs

**Input**: `specs/1054-mortal-combat-drilldowns/spec.md`, `plan.md`, `contracts/mortal-combat-drilldowns.md`, and GitHub issue #1054.

**Prerequisites**: plan.md, spec.md, requirements checklist, #948 audit artifact, AGENTS.md, `.specify/memory/constitution.md`.

**Tests**: Behavior changes require test-first work. Add or update focused tests before production code, prove at least one RED failure, then make them pass.

## Phase 1: Setup and Investigation

- [x] T001 Confirm `git status --short --branch`, current issue #1054, active branch `1054-mortal-combat-drilldowns`, and active feature path `specs/1054-mortal-combat-drilldowns`.
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #1054 body, `docs/audits/mortal-readonly-drilldown-audit.md`, `spec.md`, `plan.md`, and this task file.
- [x] T003 Inspect current `/combat` / `/бой` handling in `ExplorerMortalWorldCommandResultBuilder`, `ExplorerMode` console handlers, command catalog/registry, and existing tests around `ExplorerWebCommandServiceTests`, `ExplorerModeCommandTests`, and `MortalReadOnlyDrilldownAuditTests`.
- [x] T004 Record the exact focused baseline test command and counts before production changes.

## Phase 2: Test-First Coverage

- [x] T005 Add a focused browser/shared command-result test that seeds `game_state/combat/enemies.json`, runs `/бой` or `/combat`, and fails until an enemy can be inspected through player-facing table/detail/action content without relying on raw JSON.
- [x] T006 Add a focused browser/shared command-result test that seeds `game_state/combat/allies.json`, runs `/бой` or `/combat`, and fails until an ally can be inspected through player-facing table/detail/action content without relying on raw JSON.
- [x] T007 Add a focused browser/shared command-result test that seeds `game_state/combat/combat_log.json`, runs `/бой` or `/combat`, and fails until one combat-log entry can be inspected through player-facing list/detail content.
- [x] T008 Add or update console/source-guard tests proving console and browser expose semantically equivalent enemy, ally, and combat-log detail affordances, or explicitly record a narrower follow-up if parity cannot be completed in this PR.
- [x] T009 Run the new focused tests before implementation and record the expected RED failures in the Codex final report and, after GREEN, in this tasks file.

## Phase 3: Implementation

- [x] T010 Replace or extend the current generic `/combat` bundle path in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` with focused Mortal combat overview plus enemy, ally, and log list/detail player-facing blocks or actions.
- [x] T011 Preserve existing overview counts/state and graceful missing-file behavior for `enemies.json`, `allies.json`, and `combat_log.json`.
- [x] T012 Add player-facing Russian/in-world formatting helpers for combatant names, roles, health/condition, current action/intent, effects, notes, log round/turn, participants, and results; avoid raw canonical enum leakage in default blocks.
- [x] T013 Ensure dynamic GM-authored text used in console/browser blocks remains escaped/sanitized according to existing project patterns.
- [x] T014 Update the console `/бой` path to expose semantically equivalent enemy, ally, and combat-log inspection affordances, or create/link a follow-up issue and document the exact remaining console gap before merge.
- [x] T015 Update audit/spec documentation only as needed to record the #1054 implementation and any follow-up split; do not change GM-facing prompts/examples unless runtime contracts changed.

## Phase 4: Verification and Review Prep

- [x] T016 Run focused tests covering enemy, ally, combat-log detail output and existing `/бой` overview behavior; record exact pass/fail/skip counts.
- [x] T017 Run the broader mortal command-result/console/browser parity slice identified during implementation; record exact pass/fail/skip counts.
- [x] T018 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [x] T019 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1054-mortal-combat-drilldowns`.
- [x] T020 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [x] T021 Commit the implementation with `[skip ci]` in the commit message after tests and task evidence are updated.

## Phase 5: Hermes-Owned Review and Closure

- [x] T022 Hermes launched detached independent Codex review before PR/merge: `E:/Games/codex-runs/20260615-212514-review-boe-1054-mortal-combat-drilldowns/final.md` => `APPROVED`, blocking findings none.
- [x] T023 No Critical/Important review findings required code changes; reviewer notes were non-blocking detached-worktree/spec-helper and established raw-sidecar caveats.
- [x] T024 Hermes created PR #1059 with `Closes #1054`, local-gated verification evidence, Spec Kit links, and safe non-closing references for #1055/#1056/#1057: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1059
- [ ] T025 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1054 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up the worktree/branch, and reports the closure.

## Notes

- #1054 is intentionally limited to Mortal World `/combat` / `/бой` detail drill-downs. Do not implement #1055, #1056, #1057, #949, or afterlife spiritual combat as part of this branch.
- T022-T025 remain Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.
- If any acceptance criterion requires a new runtime/GM-authored schema contract, stop and document a follow-up rather than silently broadening #1054.

## Codex Implementation Evidence

- Baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` => passed 451, failed 0, skipped 0.
- Expected RED after adding focused tests: same test project with filter `"FullyQualifiedName~CombatOverview_ExposesEnemyAllyAndLogDrilldownActions|FullyQualifiedName~CombatEnemyDetail|FullyQualifiedName~CombatAllyDetail|FullyQualifiedName~CombatLogDetail|FullyQualifiedName~ConsoleExposesSharedEnemyAllyAndLogDrilldowns|FullyQualifiedName~ConsoleCombatSource"` => failed 6, passed 0, skipped 0; failures showed the generic browser bundle ignored detail arguments and console had no shared detail affordance.
- Focused GREEN after implementation: the same focused filter => passed 6, failed 0, skipped 0.
- Broader relevant slice: the baseline filter after implementation => passed 457, failed 0, skipped 0.
- Build gates: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => both succeeded with 0 warnings, 0 errors.
- Spec Kit check: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` => `FEATURE_DIR` resolved to `specs/1054-mortal-combat-drilldowns`.
- Diff/static gates: `git diff --check origin/main...HEAD` => clean; added-line security/static scan over changed non-plan files => `NO_MATCHES`.
- Docs/prompts impact: updated only the #948 audit artifact for #1054 status/evidence. No GM prompts, examples, runtime-state contracts, validation, normalizer, afterlife, Chaos Sea, or Shining Abode contracts were changed.
