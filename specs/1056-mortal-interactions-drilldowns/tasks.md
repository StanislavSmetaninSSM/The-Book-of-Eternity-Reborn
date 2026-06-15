---
description: "Task list for #1056 mortal player-interaction read-only detail drill-downs"
---

# Tasks: Mortal Player-Interaction Read-Only Detail Drill-Downs

**Input**: `specs/1056-mortal-interactions-drilldowns/spec.md`, `plan.md`, `contracts/mortal-interactions-drilldowns.md`, and GitHub issue #1056.

**Prerequisites**: plan.md, spec.md, requirements checklist, #948 audit artifact, #1054/#1055 drill-down patterns, AGENTS.md, `.specify/memory/constitution.md`.

**Tests**: Behavior changes require test-first work. Add or update focused tests before production code, prove at least one RED failure, then make them pass.

## Phase 1: Setup and Investigation

- [X] T001 Confirm `git status --short --branch`, current issue #1056, active branch `1056-mortal-interactions-drilldowns`, and active feature path `specs/1056-mortal-interactions-drilldowns`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #1056 body, `docs/audits/mortal-readonly-drilldown-audit.md`, #1054/#1055 drill-down patterns, `spec.md`, `plan.md`, contract, checklist, and this task file.
- [X] T003 Inspect current `/interactions` / `/взаимодействия` handling in `ExplorerMortalWorldCommandResultBuilder`, `ExplorerMode` console handlers, command catalog/registry, and existing tests around `ExplorerWebCommandServiceTests`, `ExplorerModeCommandTests`, and `MortalReadOnlyDrilldownAuditTests`.
- [X] T004 Record the exact focused baseline test command and counts before production changes.

## Phase 2: Test-First Coverage

- [X] T005 Add a focused browser/shared command-result test that seeds `game_state/misc/player_interactions.json` with multiple player entries and fails until a single player can be inspected through player-facing action/detail content without relying on raw JSON.
- [X] T006 Add a focused browser/shared command-result test that seeds one selected player with multiple nested interaction records and fails until one interaction record/payload can be inspected through player-facing action/detail content.
- [X] T007 Add or update console/source-guard tests proving console and browser expose semantically equivalent player-entry and record detail affordances, or explicitly record a narrower follow-up if parity cannot be completed in this PR.
- [X] T008 Add or update overview tests proving existing `/взаимодействия` summary behavior remains available and sparse/missing files stay graceful.
- [X] T009 Run the new focused tests before implementation and record the expected RED failures in the Codex final report and, after GREEN, in this tasks file.

## Phase 3: Implementation

- [X] T010 Replace or extend the current generic `/interactions` bundle path in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` with focused Mortal interactions overview plus player-entry and interaction-record list/detail player-facing blocks or actions.
- [X] T011 Preserve existing overview counts/state and graceful missing-file behavior for `player_interactions.json`.
- [X] T012 Add player-facing Russian/in-world formatting helpers for player names/ids, relationship/context, record titles/summaries, participants, location/time/turn, status, notes, outcomes, consequences, and tags; avoid raw canonical enum/key leakage in default blocks.
- [X] T013 Ensure dynamic GM-authored text used in console/browser blocks remains escaped/sanitized according to existing project patterns.
- [X] T014 Update the console `/взаимодействия` path to expose semantically equivalent player and record inspection affordances, or create/link a follow-up issue and document the exact remaining console gap before merge.
- [X] T015 Update `ExplorerCommandCatalog` or command metadata only as needed so supported detail arguments are treated as read-only command arguments.
- [X] T016 Update audit/spec documentation only as needed to record the #1056 implementation and any follow-up split; do not change GM-facing prompts/examples unless runtime contracts changed.

## Phase 4: Verification and Review Prep

- [X] T017 Run focused tests covering player-entry detail, interaction-record detail, console parity/source guards, and existing `/взаимодействия` overview behavior; record exact pass/fail/skip counts.
- [X] T018 Run the broader mortal command-result/console/browser parity slice identified during implementation; record exact pass/fail/skip counts.
- [X] T019 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [X] T020 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1056-mortal-interactions-drilldowns`.
- [X] T021 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T022 Commit the implementation with `[skip ci]` in the commit message after tests and task evidence are updated.

## Phase 5: Hermes-Owned Review and Closure

- [X] T023 Hermes launched detached independent review before PR/merge and recorded the run/verdict: initial review `E:/Games/codex-runs/20260616-001138-review-boe-1056-mortal-interactions-drilldowns/final.md` returned `CHANGES_REQUIRED`; detached re-review `E:/Games/codex-runs/20260616-004515-rereview-boe-1056-mortal-interactions-drilldowns/final.md` returned `APPROVED` with no Critical/Important findings.
- [X] T024 Hermes resolved the Critical/Important review finding for canonical `otherPlayersInteractions[playerId]` command-object arrays, reran fresh gates, and obtained clean re-review: canonical regression 1/1 passed; focused #1056/audit slice 8/8 passed; broader builder/web/console/registry slice 469/469 passed; client/test builds passed with 0 warnings and 0 errors; Spec Kit prerequisites resolved this feature; `git diff --check` passed; refined added-line C# static/security scan reported `NO_MATCHES`.
- [X] T025 Hermes created PR #1061 with `Closes #1056`, local-gated verification evidence, Spec Kit links, and safe non-closing references for #1057/#949: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1061.
- [ ] T026 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1056 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up the worktree/branch, and reports the closure.

## Notes

- #1056 is intentionally limited to Mortal World `/interactions` / `/взаимодействия` detail drill-downs. Do not implement #1057, #949, NPC/Guardian/resident mutating social flows, or afterlife surfaces as part of this branch.
- T023-T026 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.
- If any acceptance criterion requires a new runtime/GM-authored schema contract, stop and document a follow-up rather than silently broadening #1056.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/boe-1056-mortal-interactions-drilldowns` on branch `1056-mortal-interactions-drilldowns` from `origin/main` at `1add50eb4c7f64ca0b1a4110fad2145528403864` and created this Spec Kit feature directory for issue #1056.
- Codex setup: `git status --short --branch` showed `## 1056-mortal-interactions-drilldowns...origin/main` with untracked `specs/1056-mortal-interactions-drilldowns/`; `gh issue view 1056` confirmed issue #1056 is open with `status: in-progress`.
- Baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 463, failed 0, skipped 0, total 463.
- RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PlayerInteractionsOverview|PlayerInteractionPlayerDetail|PlayerInteractionRecordDetail|ConsoleExposesSharedPlayerAndRecordDrilldowns|ConsolePlayerInteractionsSource" --logger "console;verbosity=minimal"` failed 5, passed 0, skipped 0, total 5. Failures showed missing overview drill-down actions, detail commands falling back to raw generic output, and console not using the shared interaction result.
- GREEN focused: same new-test filter passed 5, failed 0, skipped 0, total 5.
- GREEN expanded focused: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "Interactions|PlayerInteractions|MortalReadOnlyDrilldownAudit|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"` passed 472, failed 0, skipped 0, total 472.
- GREEN broader slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 468, failed 0, skipped 0, total 468.
- Build gates: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` both succeeded with 0 warnings and 0 errors.
- Spec Kit discoverability: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\Games\worktrees\boe-1056-mortal-interactions-drilldowns\specs\1056-mortal-interactions-drilldowns`.
- Diff hygiene/security: `git diff --check` exited 0 with only Git line-ending normalization warnings; added-line static/security scan over changed non-plan code reported `NO_MATCHES`.
- Independent review fix: CHANGES_REQUIRED found canonical `otherPlayersInteractions[playerId]` arrays of command objects were addressable but record detail omitted nested payload content such as `{ "UpdateInventory": [...] }`.
- Review-fix RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_PlayerInteractionRecordDetail_RendersCanonicalCommandPayloadWithoutRawJson" --logger "console;verbosity=minimal"` failed 1, passed 0, skipped 0, total 1. Failure showed the detail panel was missing `Серебряный ключ`.
- Review-fix GREEN focused: same canonical payload filter passed 1, failed 0, skipped 0, total 1.
- Review-fix focused slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PlayerInteractions|MortalReadOnlyDrilldownAudit" --logger "console;verbosity=minimal"` passed 5, failed 0, skipped 0, total 5.
- Review-fix final #1056 slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~PlayerInteraction|FullyQualifiedName~MortalReadOnlyDrilldownAudit" --logger "console;verbosity=minimal"` passed 8, failed 0, skipped 0, total 8.
- Review-fix final broader slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 469, failed 0, skipped 0, total 469.
- Review-fix build gates: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` both succeeded with 0 warnings and 0 errors.
- Review-fix Spec Kit/diff/static gates: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\Games\worktrees\boe-1056-mortal-interactions-drilldowns\specs\1056-mortal-interactions-drilldowns`; `git diff --check origin/main...HEAD` exited 0; the narrowed added-line static/security scan over changed non-Spec code reported `NO_MATCHES`.
