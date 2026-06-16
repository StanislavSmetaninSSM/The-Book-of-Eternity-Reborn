---
description: "Task list for #1064 Soul Relic/Archive browser drill-downs"
---

# Tasks: Afterlife Soul Relic/Archive Browser Drill-Downs

**Input**: `specs/1064-afterlife-soul-relic-archive-drilldowns/spec.md`, `plan.md`, `contracts/browser-afterlife-relic-archive-detail-actions.md`, `checklists/requirements.md`, GitHub issue #1064, #949 audit row AFD-003.

**Prerequisites**: AGENTS.md, `.specify/memory/constitution.md`, #1064 body, #949 audit/spec, Browser Reborn panel/detail/action-result references, afterlife drill-down child launch guidance, #1063 Chaos Sea drill-down patterns, and current command-result patterns.

**Tests**: Strict TDD for behavior changes. Write focused failing tests/source guards before production code; verify RED failure for missing detail actions/selected-detail behavior; then implement minimal GREEN changes.

## Phase 1: Setup and Investigation

- [X] T001 Hermes created ASCII worktree `E:/Games/worktrees/1064-afterlife-soul-relic-archive-drilldowns` on branch `work/1064-afterlife-soul-relic-archive-drilldowns` from `origin/main`.
- [X] T002 Hermes created active Spec Kit feature directory `specs/1064-afterlife-soul-relic-archive-drilldowns` and linked source issue #1064 plus origin audit #949 in spec/plan/tasks/contract/checklist.
- [X] T003 Hermes read `AGENTS.md`, `.specify/memory/constitution.md`, #1064 body, afterlife drill-down child guidance, Browser Reborn/action-result/contextual-action references, #949 audit context, and sibling #1063 Spec Kit patterns before Codex launch. Codex must still inspect nearby implementation code/tests/docs before editing.
- [X] T004 Inspect current command catalog descriptors and parser behavior for `/soul_relics`, `/soul_relic_equip`, `/soul_relic_unequip`, `/afterlife_archive`, `/archive_candidates`, `/archive_consultation`, and `/archive_project_fuel`, including aliases and argument acceptance.
- [X] T005 Inspect current browser command service dispatch and shared C# result builders for the covered commands.
- [X] T006 Inspect console ExplorerMode Soul Relic and Archive selectors/detail panels for equivalent player-facing semantics.
- [X] T007 Inspect #1063 Chaos Sea detail action implementation and #1057 mortal reference detail action tests/implementation as preferred shared C# patterns.
- [X] T008 Hermes records exact baseline commands and counts before production changes.

## Phase 2: RED Tests and Source Guards

- [X] T009 Add focused RED tests proving `/soul_relics`, `/soul_relic_equip`, `/soul_relic_unequip`, `/afterlife_archive`, `/archive_candidates`, `/archive_consultation`, and `/archive_project_fuel` overview/local-action results lack the required browser detail actions for seeded rich data.
- [X] T010 Add focused RED tests proving selected-detail commands for representative relic/archive/candidate/fuel entries currently fail, are missing, or collapse into raw/generic output.
- [X] T011 Add or update source guards ensuring default browser detail output avoids `game_state/`, API, DTO, endpoint, protocol, debug, raw slash-command, and `UiRawJsonBlock` leakage.
- [X] T012 Run the focused tests/source guards before production code and record expected RED failure counts/reasons in the Evidence Log.

## Phase 3: Shared C# Detail Action Implementation

- [X] T013 Ensure relevant Explorer command descriptors accept arguments where the selected-detail grammar needs them.
- [X] T014 Add shared C# detail actions to overview/local-action command results while preserving all existing overview blocks/tables/forms/sections.
- [X] T015 Implement selected-detail result handling for one focused Soul Relic, archive entry, archive candidate, consultation context, and project-fuel row using existing canonical data.
- [X] T016 Implement graceful missing/sparse/stale/unknown id states with Russian/in-world unavailable explanations.
- [X] T017 Keep implementation read-only for the new drill-downs: no pending/control writes, write-service calls, validation/normalizer/schema changes, or React-side gameplay authority.
- [X] T018 If a desired sub-surface requires broader runtime/GM contract work, stop that sub-surface and create/link a focused follow-up rather than expanding #1064.

## Phase 4: Verification and Review Prep

- [X] T019 Run focused Browser/afterlife command tests and record exact pass/fail/skip counts.
- [X] T020 Run the broader afterlife/browser/console slice and record exact pass/fail/skip counts.
- [X] T021 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [X] T022 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files changed.
- [X] T023 Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` if afterlife runtime contracts/docs/examples/manifests change.
- [X] T024 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1064-afterlife-soul-relic-archive-drilldowns`.
- [X] T025 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T026 Update this Evidence Log with RED/GREEN counts, docs/prompts impact, verification results, and any follow-up issue links.
- [X] T027 Commit the focused implementation with `[skip ci]`.

## Phase 5: Hermes-Owned Review and Closure

- [X] T028 Hermes launches detached independent review before PR/merge and records run/verdict.
- [X] T029 Hermes resolves Critical/Important review findings, reruns fresh gates, and obtains clean review/re-review.
- [X] T030 Hermes creates PR with `Closes #1064`, local-gated verification evidence, Spec Kit links, #949 reference, and #1065-#1067 as sibling non-closing references unless fully satisfied.
- [ ] T031 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1064 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up worktree/branch, and reports closure.

## Notes

- #1064 is the second #949 child follow-up after #1063. Keep #1065-#1067 separate unless a change fully and verifiably satisfies a sibling.
- Do not change afterlife runtime contracts, pending/control files, validation, normalizer behavior, GM prompts/examples, or manifests without same-PR docs/tests or a tracked follow-up.
- T028-T031 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/1064-afterlife-soul-relic-archive-drilldowns` on branch `work/1064-afterlife-soul-relic-archive-drilldowns` from `origin/main` at `127b17fc5f716a385442c4be8fa2d9d63a1b83b2` and created this Spec Kit feature directory for issue #1064.
- Source issue: #1064 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1064.
- Origin audit: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949; `docs/audits/afterlife-drilldown-audit.md` row AFD-003.
- Spec Kit verification before Codex: `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH && specify version` returned CLI `0.9.3`; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\1064-afterlife-soul-relic-archive-drilldowns\\specs\\1064-afterlife-soul-relic-archive-drilldowns`.
- Issue ownership: Hermes added label `status: in-progress` to #1064 before launching Codex.
- Focused baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 207, failed 0, skipped 0, total 207.
- Broad afterlife/browser/console baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~SoulRelic|FullyQualifiedName~Archive|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1516, failed 0, skipped 0, total 1516.
- Codex investigation: inspected command catalog argument flags, `ExplorerWebCommandService` dispatch, `ExplorerUniversalMetaCommandResultBuilder`, `ExplorerLifecycleLocalTurnCommandResultBuilder`, console Soul Relic/Archive ExplorerMode detail flows, #1063 Chaos Sea detail action patterns, and #949 AFD-003 audit requirements before production edits.
- RED: added #1064 catalog/action/detail tests and source-leak assertions. `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SoulRelicArchiveDetailActions_CatalogAllowsIssue1064DetailArguments|FullyQualifiedName~AfterlifeRelicArchive" --logger "console;verbosity=minimal"` failed as expected: passed 0, failed 18, skipped 0, total 18. Expected failures were missing `AcceptsArguments`, missing overview/local detail actions, selected-detail commands collapsing into raw overview output or prompt sessions, and unknown ids lacking player-facing unavailable detail text.
- Implementation: updated shared C# command-result/action metadata for `/soul_relics`, `/soul_relic_equip`, `/soul_relic_unequip`, `/afterlife_archive`, `/archive_candidates`, `/archive_consultation`, and `/archive_project_fuel`; selected-detail commands now render focused Russian/player-facing detail blocks for a relic, archive entry, archive candidate, consultation guardian, and archive project-fuel target.
- Read-only contract impact: no new pending/control files, write-service calls, validation/normalizer/schema changes, React gameplay authority, GM prompts, GM examples, docs, manifests, or afterlife runtime contracts were changed. Existing overview tables and local action forms remain present.
- GREEN focused target: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SoulRelicArchiveDetailActions_CatalogAllowsIssue1064DetailArguments|FullyQualifiedName~AfterlifeRelicArchive" --logger "console;verbosity=minimal"` passed 18, failed 0, skipped 0, total 18.
- Focused suite: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 225, failed 0, skipped 0, total 225.
- Broad afterlife/browser/console slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~SoulRelic|FullyQualifiedName~Archive|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1534, failed 0, skipped 0, total 1534.
- Builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` succeeded with 0 warnings and 0 errors.
- Spec Kit prerequisite: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\1064-afterlife-soul-relic-archive-drilldowns\\specs\\1064-afterlife-soul-relic-archive-drilldowns`.
- Diff hygiene: `git diff --check origin/main...HEAD` exited 0 with no whitespace errors after the focused implementation commit.
- Static/security scan: broad added-line scan over changed non-plan code only matched benign fixture terms (`secret_record`, `candidate_secret`, `projectType: secret`) and local variable `commandToken`; stricter credential-pattern scan reported no added-line credential/security pattern matches.
- Not run: `npm run verify --prefix BookOfEternityClient.WebFrontend` because no frontend files changed; `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests` because no afterlife runtime contracts, docs, examples, or manifests changed.
- Follow-up context: #1065-#1067 remain sibling follow-ups and were not implemented by this #1064 slice. Hermes retains T028-T031 review/PR/merge/issue-closure ownership.
- Independent review: detached read-only Codex review run `E:/Games/codex-runs/20260616-170246-review-boe-1064-afterlife-soul-relic-archive-drilldowns` reviewed implementation commit `8c21e38d0acbb000507eef6f68cfd0bb1ae8a401`; exit code 0; verdict `APPROVED`; Critical issues: none; Important issues: none. Minor non-blocking suggestion: future hardening test for detail selectors containing escaped quotes/backslashes. Review worktree Spec Kit helper failed only because it was detached HEAD; implementation-worktree Spec Kit prerequisite had already resolved this active feature directory.
- Hermes post-review reconciliation before PR: Critical/Important findings required no code changes. Hermes will rerun a focused sanity gate, Spec Kit prerequisite, `git diff --check`, and refined static/security scan after this review-evidence-only update before creating the PR.
- PR creation: Hermes created PR #1070 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1070 — with `Closes #1064`, local-gated evidence, Spec Kit links, #949 audit reference, and #1065-#1067 only as sibling non-closing references. Initial PR head was `08a93cf2b5a690b781864a9d325abb15697ee16e`; GitHub readback showed `mergeStateStatus=CLEAN` and closing references only #1064 before this PR-evidence-only amend.
