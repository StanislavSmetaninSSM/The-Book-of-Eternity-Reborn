---
description: "Task list for #1065 Shining Abode browser inspection drill-downs"
---

# Tasks: Shining Abode Browser Inspection Drill-Downs

**Input**: `specs/1065-shining-abode-drilldowns/spec.md`, `plan.md`, `contracts/browser-shining-abode-detail-actions.md`, `checklists/requirements.md`, GitHub issue #1065, #949 audit row AFD-004.

**Prerequisites**: AGENTS.md, `.specify/memory/constitution.md`, #1065 body, #949 audit/spec, Browser Reborn panel/detail/action-result references, afterlife drill-down child launch guidance, #1063/#1064 detail-action patterns, and current command-result patterns.

**Tests**: Strict TDD for behavior changes. Write focused failing tests/source guards before production code; verify RED failure for missing Shining detail actions/selected-detail behavior; then implement minimal GREEN changes.

## Phase 1: Setup and Investigation

- [X] T001 Hermes created ASCII worktree `E:/Games/worktrees/1065-shining-abode-drilldowns` on branch `work/1065-shining-abode-drilldowns` from `origin/main`.
- [X] T002 Hermes created active Spec Kit feature directory `specs/1065-shining-abode-drilldowns` and linked source issue #1065 plus origin audit #949 in spec/plan/tasks/contract/checklist.
- [X] T003 Hermes read `AGENTS.md`, `.specify/memory/constitution.md`, #1065 body, afterlife drill-down child guidance, #949 AFD-004 audit row, and #1063/#1064 Spec Kit patterns before Codex launch. Codex must still inspect nearby implementation code/tests/docs before editing.
- [X] T004 Inspect current command catalog descriptors and parser behavior for all covered Shining commands, including aliases and argument acceptance.
- [X] T005 Inspect current browser command service dispatch and shared C# result builders for the covered commands.
- [X] T006 Inspect console ExplorerMode Shining inspection panels for gates, core receipts, pending core actions, trade lifecycle, resident project audit, structures, politics, faction chronicles, pending political actions, and political resolutions.
- [X] T007 Inspect #1063/#1064 afterlife detail action implementations and #1057 mortal reference detail action tests/implementation as preferred shared C# patterns.
- [X] T008 Hermes records exact baseline commands and counts before production changes.

## Phase 2: RED Tests and Source Guards

- [X] T009 Add focused RED tests proving representative covered Shining overview/local-action results lack the required browser detail actions for seeded rich data.
- [X] T010 Add focused RED tests proving selected-detail commands for representative Shining gate/faction/project/politics/resource/action rows currently fail, are missing, or collapse into raw/generic output.
- [X] T011 Add or update source guards ensuring default browser detail output avoids `game_state/`, API, DTO, endpoint, protocol, debug, raw slash-command, and `UiRawJsonBlock` leakage.
- [X] T012 Run the focused tests/source guards before production code and record expected RED failure counts/reasons in the Evidence Log.

## Phase 3: Shared C# Detail Action Implementation

- [X] T013 Ensure relevant Explorer command descriptors accept arguments where the selected-detail grammar needs them.
- [X] T014 Add shared C# detail actions to overview/local-action command results while preserving all existing overview blocks/tables/forms/sections.
- [X] T015 Implement selected-detail result handling for focused Shining gates, core receipts, pending core actions, trade lifecycle, resident project audit, structures, factions, chronicles, pending political actions, political resolutions, treasury/source/resource rows, forge/action contexts, or the representative subset justified by #1065 scope.
- [X] T016 Implement graceful missing/sparse/stale/unknown id states with Russian/in-world unavailable explanations.
- [X] T017 Keep implementation read-only for the new drill-downs: no pending/control writes, write-service calls, validation/normalizer/schema changes, or React-side gameplay authority.
- [X] T018 If a desired sub-surface requires broader runtime/GM contract work, stop that sub-surface and create/link a focused follow-up rather than expanding #1065.

## Phase 4: Verification and Review Prep

- [X] T019 Run focused Browser/afterlife/Shining command tests and record exact pass/fail/skip counts.
- [X] T020 Run the broader afterlife/browser/console slice and record exact pass/fail/skip counts.
- [X] T021 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [X] T022 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files changed.
- [X] T023 Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` if afterlife runtime contracts/docs/examples/manifests change.
- [X] T024 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1065-shining-abode-drilldowns`.
- [X] T025 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T026 Update this Evidence Log with RED/GREEN counts, docs/prompts impact, verification results, and any follow-up issue links.
- [X] T027 Commit the focused implementation with `[skip ci]`.

## Phase 5: Hermes-Owned Review and Closure

- [X] T028 Hermes launches detached independent review before PR/merge and records run/verdict.
- [X] T029 Hermes resolves Critical/Important review findings, reruns fresh gates, and obtains clean review/re-review.
- [X] T030 Hermes creates PR with `Closes #1065`, local-gated verification evidence, Spec Kit links, #949 reference, and #1066-#1067 as sibling non-closing references unless fully satisfied.
- [ ] T031 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1065 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up worktree/branch, and reports closure.

## Notes

- #1065 is the third #949 child follow-up after #1063 and #1064. Keep #1066-#1067 separate unless a change fully and verifiably satisfies a sibling.
- Do not change afterlife runtime contracts, pending/control files, validation, normalizer behavior, GM prompts/examples, or manifests without same-PR docs/tests or a tracked follow-up.
- T028-T031 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/1065-shining-abode-drilldowns` on branch `work/1065-shining-abode-drilldowns` from `origin/main` at `e69ebe2e1e68d06bc5db2b3bf34f185db38535fa` and created this Spec Kit feature directory for issue #1065.
- Source issue: #1065 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1065.
- Origin audit: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949; `docs/audits/afterlife-drilldown-audit.md` row AFD-004.
- Spec Kit basis: `.specify/memory/constitution.md` version 1.1.0 requires Spec Kit for console/browser parity, player-facing UX, and afterlife/Shining Abode work; #1065 meets that policy.
- Issue ownership: Hermes added label `status: in-progress` to #1065 before launching Codex.
- Spec Kit verification before Codex: `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH && specify version` returned CLI `0.9.3`; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\1065-shining-abode-drilldowns\\specs\\1065-shining-abode-drilldowns` with `contracts/` and `tasks.md` available.
- Focused baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 225, failed 0, skipped 0, total 225.
- Broad afterlife/browser/console baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1851, failed 0, skipped 0, total 1851.
- Codex implementation inspection: Codex inspected `AGENTS.md`, `.specify/memory/constitution.md`, the active spec/plan/tasks/contract/checklist, #949 AFD-004 audit row, current Shining command catalog/service/result builder code, console Shining inspection panels, and #1063/#1064 detail-action tests/builders before production edits.
- RED evidence before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTestsShiningAbodeDrilldowns" --logger "console;verbosity=minimal"` failed as expected: passed 0, failed 21, skipped 0, total 21. Failures showed missing detail actions, selected-detail commands falling back to overview/raw output, descriptors not accepting arguments, and local form detail actions absent.
- Implementation summary: Added `BookOfEternityClient.Tests/ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs`; enabled argument passing for `/shining_abode` and `/shining_politics`; added shared C# read-only `UiAction` detail affordances and selected-detail rendering for visible Shining gates, projects, factions, faction chronicle/resource entries, pending core/political actions, and core/political receipts; preserved existing local forms and added read-only context actions for gate selection and project support.
- GREEN focused evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTestsShiningAbodeDrilldowns" --logger "console;verbosity=minimal"` passed 21, failed 0, skipped 0, total 21.
- Focused suite evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 246, failed 0, skipped 0, total 246.
- Broad slice evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1872, failed 0, skipped 0, total 1872.
- Build evidence: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` succeeded with 0 warnings and 0 errors.
- Spec Kit verification: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\1065-shining-abode-drilldowns\\specs\\1065-shining-abode-drilldowns` with `contracts/` and `tasks.md` available.
- Diff/static evidence before commit: `git diff --check origin/main...HEAD` produced no output; `git diff --check` produced no whitespace errors for the working tree; added-line static/security scan over changed C# code/test lines found no secret-like patterns.
- Docs/prompts impact: No React/frontend files changed; no afterlife runtime contracts, pending/control surfaces, validation/normalizer/schema behavior, GM prompts, examples, or manifests changed. Therefore `npm run verify --prefix BookOfEternityClient.WebFrontend` and documentation-sensitive afterlife tests were not applicable for this implementation.
- Follow-ups: No #1065 sub-surface required broader runtime/GM contract work during this implementation. #1066 and #1067 remain sibling follow-ups and are intentionally not closed by this branch.
- Independent review, PR, merge, issue comment/closure, lifecycle label transition, and cleanup evidence remain Hermes-owned (T028-T031).
- Independent review: detached read-only Codex review run `E:/Games/codex-runs/20260616-191600-review-boe-1065-shining-abode-drilldowns` reviewed implementation commit `3fd45e2044ae1472b626b7590767ed92bdae1e58`; exit code 0; verdict `APPROVED`; Critical issues: none; Important issues: none. Review reran focused #1065 tests (21 passed, 0 failed, 0 skipped), focused browser/audit suite (246 passed, 0 failed, 0 skipped), and `git diff --check origin/main...HEAD` (exit 0). Detached Spec Kit helper failed only because the review worktree was detached HEAD; implementation-worktree Spec Kit prerequisite already resolved this active feature directory.
- Review minor suggestion: consider a separate follow-up for pre-existing raw diagnostics in `/shining_treasury` and `/source_of_light` plus malformed JSON warning path text behind advanced mode. Non-blocking for #1065 because the selected-detail implementation avoids raw JSON/path leakage and did not change those local-form surfaces.
- Hermes post-review reconciliation before PR: Critical/Important findings required no code changes. Hermes will rerun a focused sanity gate, Spec Kit prerequisite, `git diff --check`, and refined static/security scan after this review-evidence-only update before creating the PR.
- PR creation: Hermes created PR #1071 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1071 — with `Closes #1065`, local-gated evidence, Spec Kit links, #949 audit reference, and #1066-#1067 only as sibling non-closing references. Initial PR head was `16151655bffe1ed37a20323a2148522caff75822`; GitHub readback showed `mergeStateStatus=CLEAN` and closing references only #1065 before this PR-evidence-only amend.
