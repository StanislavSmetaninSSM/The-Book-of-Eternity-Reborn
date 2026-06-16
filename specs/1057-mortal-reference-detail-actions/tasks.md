---
description: "Task list for #1057 browser detail actions for mortal read-only reference commands"
---

# Tasks: Browser Detail Actions for Mortal Reference Commands

**Input**: `specs/1057-mortal-reference-detail-actions/spec.md`, `plan.md`, `contracts/mortal-reference-detail-actions.md`, and GitHub issue #1057.

**Prerequisites**: AGENTS.md, `.specify/memory/constitution.md`, #1057 body, #948 audit artifact, #1054/#1055/#1056 implementation patterns, browser detail/action result references, and current Browser Client direction.

**Tests**: Behavior changes require test-first work. Add or update focused tests/source guards before production code, prove at least one RED failure, then make them pass.

## Phase 1: Setup and Investigation

- [X] T001 Hermes created ASCII worktree `E:/Games/worktrees/boe-1057-mortal-reference-detail-actions` on branch `1057-mortal-reference-detail-actions` from `origin/main`.
- [X] T002 Hermes created active Spec Kit feature directory `specs/1057-mortal-reference-detail-actions` and linked source issue #1057 in spec/plan/tasks/contract/checklist.
- [X] T003 Hermes read `AGENTS.md`, `.specify/memory/constitution.md`, #1057 body, #1057-relevant Book references, `spec.md`, `plan.md`, contract, checklist, and this task file before Codex launch. Codex must still inspect nearby implementation code and tests before editing.
- [X] T004 Inspect current handling/tests for `/quests`, `/skills`, `/factions`, `/locations`, `/rival_threads`, `/guardian_corrections`, `/storage_access`, and `/transport` in `ExplorerMortalWorldCommandResultBuilder`, `ExplorerWebCommandService`, `ExplorerMode` console handlers, and `ExplorerCommandCatalog`.
- [X] T005 Hermes recorded the exact baseline command and counts before production changes.

## Phase 2: Test-First Coverage

- [X] T006 Add focused browser/shared command-result tests proving representative reference commands expose player-facing detail actions or equivalent detail affordances in default browser command-result DTOs.
- [X] T007 Add focused detail-result tests proving at least one selected entity/record renders useful safe player-facing blocks and does not collapse to generic `Выполнено` or raw-only output.
- [X] T008 Add or update console/catalog/source-guard tests documenting console/browser parity expectations and stable command aliases for the covered detail actions.
- [X] T009 Add or update overview-preservation tests proving affected command overviews remain available and sparse/missing data stays graceful.
- [X] T010 Run the new focused tests before implementation and record the expected RED failures in the Evidence Log.

## Phase 3: Implementation

- [X] T011 Extend shared C# command-result/action metadata so covered reference commands expose browser-safe detail affordances without React-side gameplay logic.
- [X] T012 Implement or wire selected detail command/result paths for covered representative entities using existing console behavior and shared command-result DTO patterns.
- [X] T013 Preserve existing overview blocks/tables/counts and graceful empty-state behavior for each affected command touched by this PR.
- [X] T014 Ensure detail output uses Russian/in-world player-facing labels and preserves safe `ExplorerCommandResult` blocks/actions in default mode.
- [X] T015 Ensure default player-facing output does not expose raw JSON, local file paths, `DTO`, `API`, `endpoint`, debug framing, or raw slash-command internals except behind established advanced/debug diagnostics.
- [X] T016 Keep the branch read-only: do not add mutations, prompt-session writes, pending files, validation/schema changes, or new GM-authored state contracts.
- [X] T017 Update `docs/audits/mortal-readonly-drilldown-audit.md` to mark #1057 coverage and list exact follow-up issues for any affected command that remains intentionally deferred.
- [X] T018 Update `ExplorerCommandCatalog` or command metadata only as needed so supported detail arguments/actions are treated as read-only command arguments.
- [X] T019 If React/Vite files must change, keep them presentation-only and add frontend verification; otherwise leave frontend untouched and rely on shared command-result DTO/browser service tests.

## Phase 4: Verification and Review Prep

- [X] T020 Run focused #1057 tests and record exact pass/fail/skip counts.
- [X] T021 Run the broader mortal command-result/console/browser parity slice and record exact pass/fail/skip counts.
- [X] T022 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [X] T023 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files changed.
- [X] T024 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1057-mortal-reference-detail-actions`.
- [X] T025 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T026 Commit the implementation with `[skip ci]` in the commit message after tests and task evidence are updated.

## Phase 5: Hermes-Owned Review and Closure

- [X] T027 Hermes launches detached independent review before PR/merge and records run/verdict.
- [X] T028 Hermes resolves Critical/Important review findings, reruns fresh gates, and obtains clean review/re-review.
- [X] T029 Hermes creates PR with `Closes #1057`, local-gated verification evidence, Spec Kit links, and safe non-closing references for #946/#947/#949 if mentioned.
- [ ] T030 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1057 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up the worktree/branch, and reports the closure.

## Notes

- #1057 is intentionally limited to browser detail actions/equivalent detail affordances for Mortal World reference-style read-only commands. Do not implement NPC #946, Books #947, afterlife #949, combat #1054, world-news #1055, or interactions #1056 as part of this branch.
- T027-T030 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.
- If any acceptance criterion requires a new runtime/GM-authored schema contract, stop and document a follow-up rather than silently broadening #1057.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/boe-1057-mortal-reference-detail-actions` on branch `1057-mortal-reference-detail-actions` from `origin/main` at `fc884eeb54fdcd41dd8134dd8b8e6f92a93674ba` and created this Spec Kit feature directory for issue #1057.
- Issue ownership: Hermes moved #1057 from `status: triaged` to `status: in-progress` before launching Codex.
- Spec Kit discoverability: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1057-mortal-reference-detail-actions\\specs\\1057-mortal-reference-detail-actions`.
- Baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 469, failed 0, skipped 0, total 469.
- Codex inspection: read AGENTS.md, constitution, spec/plan/tasks/contract/checklist, `ExplorerMortalWorldCommandResultBuilder`, `ExplorerCommandCatalog`, `ExplorerWebCommandServiceTests`, `ExplorerModeCommandTests`, `MortalReadOnlyDrilldownAuditTests`, and the #948 audit before editing. Existing patterns from #1054/#1055/#1056 use shared `ExplorerCommandResult` DTOs/actions with read-only typed detail arguments.
- RED focused test run before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserDetailActions|ReferenceDetail|MortalReadOnlyDrilldownAudit" --logger "console;verbosity=minimal"` failed as expected: passed 1, failed 17, skipped 0, total 18. Failures proved the eight commands lacked detail actions, detail commands still returned overview/raw-only output, and catalog descriptors did not preserve read-only detail arguments.
- Implementation summary: `ExplorerMortalWorldCommandResultBuilder` now preserves overview tables and raw overview diagnostics while adding Russian player-facing selected-record detail panels/actions for `/quests`, `/skills`, `/factions`, `/locations`, `/rival_threads`, `/guardian_corrections`, `/storage_access`, and `/transport`; `ExplorerCommandCatalog` now marks these read-only descriptors as accepting arguments so browser detail actions can round-trip through the command API.
- Debugging note: first GREEN attempt compiled but failed 2 storage-access assertions because the generic title resolver prioritized `locationName` before `storageName`; root cause was fixed by prioritizing entity names over context location fields. Rerun of the same focused slice passed 18, failed 0, skipped 0, total 18.
- Required focused #1057 run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserDetailActions|ReferenceDetail|MortalReadOnlyDrilldownAudit|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"` passed 467, failed 0, skipped 0, total 467.
- Required broader slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 486, failed 0, skipped 0, total 486.
- Build verification: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` succeeded with 0 warnings and 0 errors.
- Frontend verification: no `BookOfEternityClient.WebFrontend` files changed; `npm run verify --prefix BookOfEternityClient.WebFrontend` was not required.
- Spec Kit verification after implementation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR` `E:\\Games\\worktrees\\boe-1057-mortal-reference-detail-actions\\specs\\1057-mortal-reference-detail-actions` and `AVAILABLE_DOCS` `["contracts/","tasks.md"]`.
- Diff hygiene: working-tree `git diff --check` returned no whitespace findings, with Git line-ending normalization warnings only; working-tree added-line static/security scan over changed non-plan code returned `NO_MATCHES`. Post-commit `git diff --check origin/main...HEAD` returned no findings, and the post-commit added-line static/security scan over changed non-plan code returned `NO_MATCHES`.
- Commit: implementation committed with message `feat: add mortal reference detail actions [skip ci]`; T026 was marked complete and amended into the same implementation commit.
- Independent review: Hermes launched detached read-only review from `69e9efed9cf2b31ca61a10690292cc1717493451` using review worktree `E:/Games/review-worktrees/boe-1057-mortal-reference-detail-actions-review-20260616-024751` and run dir `E:/Games/codex-runs/20260616-024751-review-boe-1057-mortal-reference-detail-actions`. Review `exit-code.txt` was `0`; `final.md` verdict was `APPROVED`; Critical/Important findings: none. Review harness limitations were detached-HEAD Spec Kit branch helper failure and missing detached-worktree test DLL for `--no-build`, while Hermes implementation-worktree gates above already passed.
- PR creation: Hermes created PR #1062 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1062`) with `Closes #1057`, local-gated verification evidence, Spec Kit links, and related-work wording that references #946/#947/#949 without closing them. GitHub readback showed `mergeStateStatus=CLEAN`, `state=OPEN`, head `35c6bdbed5ec095ae240463a16d157314c0e607b`, and closing issue reference only #1057 before this evidence amend.
