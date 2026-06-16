---
description: "Task list for #1072 Shining treasury/source advanced diagnostics boundary"
---

# Tasks: Shining Advanced Diagnostics Boundary

**Input**: `specs/1072-shining-advanced-diagnostics/spec.md`, `plan.md`, `contracts/browser-shining-advanced-diagnostics.md`, `checklists/requirements.md`, GitHub issue #1072, #1065 review note, #949 AFD-004.

**Prerequisites**: AGENTS.md, `.specify/memory/constitution.md`, #1072 body, #1065 final/review evidence, #949 audit row, Browser player-vs-advanced UI separation, Browser action result surfaces, Browser Reborn panels guidance, and current command-result patterns.

**Tests**: Strict TDD for behavior changes. Write focused failing tests/source guards before production code; verify RED failure for default treasury/source raw diagnostic leakage or missing advanced gating; then implement minimal GREEN changes.

## Phase 1: Setup and Investigation

- [X] T001 Hermes created ASCII worktree `E:/Games/worktrees/1072-shining-advanced-diagnostics` on branch `work/1072-shining-advanced-diagnostics` from `origin/main`.
- [X] T002 Hermes created active Spec Kit feature directory `specs/1072-shining-advanced-diagnostics` and linked source issue #1072 plus origin #1065/#949 in spec/plan/tasks/contract/checklist.
- [X] T003 Hermes marked #1072 as `status: in-progress` after branch/worktree creation.
- [X] T004 Codex inspects `AGENTS.md`, `.specify/memory/constitution.md`, #1072, active Spec Kit artifacts, #1065 review note, #949 AFD-004 audit row, and nearby Shining/browser command-result code before editing.
- [X] T005 Inspect current `/shining_treasury` and `/source_of_light` command dispatch, command-result blocks/actions, and advanced/debug-mode flag handling.
- [X] T006 Identify the exact source of default raw `UiRawJsonBlock`, raw JSON, `game_state/` path, local filesystem path, API/DTO/endpoint/protocol/debug copy, or malformed JSON warning path text.
- [X] T007 Inspect #1065 selected-detail implementation and tests as the preferred no-leak default-output pattern.
- [X] T008 Hermes records exact baseline commands and counts before production changes.

## Phase 2: RED Tests and Source Guards

- [X] T009 Add focused RED tests proving default `/shining_treasury` output currently leaks or can leak raw diagnostics/path/meta copy, or lacks the required safe player-facing replacement.
- [X] T010 Add focused RED tests proving default `/source_of_light` output currently leaks or can leak raw diagnostics/path/meta copy, or lacks the required safe player-facing replacement.
- [X] T011 Add malformed/sparse-state regression coverage for treasury/source diagnostics where current code can reproduce the issue.
- [X] T012 Add or update source guards/tests distinguishing default player output from explicit advanced/debug diagnostics.
- [X] T013 Run the focused tests/source guards before production code and record expected RED failure counts/reasons in the Evidence Log.

## Phase 3: Shared C# Boundary Implementation

- [X] T014 Implement shared C# gating so default treasury/source browser results exclude raw diagnostic blocks, raw JSON, paths, parser exception text, and API/DTO/endpoint/protocol/debug wording.
- [X] T015 Preserve useful default Russian/in-world treasury/source summaries, empty states, and actionable unavailable guidance.
- [X] T016 Preserve or wire explicit advanced/debug diagnostics where the existing command-result model supports them, without exposing them in default mode.
- [X] T017 Preserve existing Shining write/prompt authority: no new pending/control files, write-service calls, validation/normalizer/schema changes, or React-side gameplay authority.
- [X] T018 If a required sub-surface needs runtime/GM contract work, stop that sub-surface and create/link a focused follow-up rather than expanding #1072.

## Phase 4: Verification and Review Prep

- [X] T019 Run focused default-vs-advanced treasury/source tests and record exact pass/fail/skip counts.
- [X] T020 Run the broader afterlife/Shining/browser command slice and record exact pass/fail/skip counts.
- [X] T021 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [X] T022 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files changed.
- [X] T023 Run documentation-sensitive afterlife tests if runtime contracts/docs/examples/manifests change.
- [X] T024 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1072-shining-advanced-diagnostics`.
- [X] T025 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T026 Update this Evidence Log with RED/GREEN counts, docs/prompts impact, verification results, and any follow-up issue links.
- [X] T027 Commit the focused implementation with `[skip ci]`.

## Phase 5: Hermes-Owned Review and Closure

- [X] T028 Hermes launches detached independent review before PR/merge and records run/verdict.
- [X] T029 Hermes resolves Critical/Important review findings, reruns fresh gates, and obtains clean review/re-review.
- [X] T030 Hermes creates PR with `Closes #1072`, local-gated verification evidence, Spec Kit links, #1065/#949 references, and #1066/#1067 as sibling non-closing references unless fully satisfied.
- [ ] T031 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1072 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up worktree/branch, and reports closure.

## Notes

- #1072 is a focused follow-up from #1065 review. Keep #1066 and #1067 separate afterlife drill-down children unless a change fully and verifiably satisfies a sibling.
- Do not change afterlife runtime contracts, pending/control files, validation, normalizer behavior, GM prompts/examples, or manifests without same-PR docs/tests or a tracked follow-up.
- T028-T031 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/1072-shining-advanced-diagnostics` on branch `work/1072-shining-advanced-diagnostics` from `origin/main` at `ca84accd310f519f345d922c18948bdac81b1894` and created this Spec Kit feature directory for issue #1072.
- Source issue: #1072 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1072.
- Origin review/audit: #1065 independent review recommended moving pre-existing `/shining_treasury` and `/source_of_light` raw diagnostics plus malformed JSON warning path text behind advanced mode; #949 AFD-004 is the related Shining Abode audit context.
- Spec Kit basis: `.specify/memory/constitution.md` version 1.1.0 requires Spec Kit for player-facing UX, default-vs-advanced client integrity, and afterlife/Shining Abode work; #1072 meets that policy.
- Issue ownership: Hermes added label `status: in-progress` to #1072 before launching Codex and removed stale `status: triaged`.
- Baseline evidence before production changes: `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH && specify version` returned CLI `0.9.3`; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\1072-shining-advanced-diagnostics\\specs\\1072-shining-advanced-diagnostics` with `contracts/` and `tasks.md` available.
- Focused baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 246, failed 0, skipped 0, total 246.
- Broad afterlife/Shining/browser baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1872, failed 0, skipped 0, total 1872.
- Codex inspection: read `AGENTS.md`, constitution v1.1.0, #1072 issue body, active spec/plan/tasks/contract/checklist, #1065 closure comment/review note, #949 AFD-004 audit row, `ExplorerWebCommandService`, `ExplorerShiningAbodeCommandResultBuilder`, command-result block types, and #1065 Shining drill-down tests before production edits. No artifact conflict found.
- Leak source found: `/shining_treasury` and `/source_of_light` called `AddRawOrWarning` unconditionally, while existing `/shining_abode` and `/shining_politics` used `AdvancedEnabled` / `ShowGmThoughts` gating. Default copy also exposed local UI protocol wording and Source pending file / raw reward ids.
- RED focused #1072 test: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTestsShiningAdvancedDiagnostics" --logger "console;verbosity=minimal"` failed as expected: passed 2, failed 4, skipped 0, total 6. Expected failures were default `UiRawJsonBlock` leakage and visible `.json` / `game_state` / pending / protocol warning copy.
- Implementation: added focused tests in `BookOfEternityClient.Tests/ExplorerWebCommandServiceTestsShiningAdvancedDiagnostics.cs`; changed `ExplorerShiningAbodeCommandResultBuilder` so treasury/source default output omits raw diagnostics and safe malformed-state warnings use player-facing GM-check copy. Explicit advanced diagnostics remain available through `ExplorerWebCommandRequest.AdvancedEnabled = true` and `StateManager.Settings.ShowGmThoughts = true`.
- GREEN focused #1072 test: same focused command passed 6, failed 0, skipped 0, total 6.
- Focused browser/Shining/registry/audit post-change gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 252, failed 0, skipped 0, total 252.
- Broad afterlife/Shining/browser post-change gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1878, failed 0, skipped 0, total 1878.
- C# builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings / 0 errors.
- Spec Kit prerequisite check after implementation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\1072-shining-advanced-diagnostics\\specs\\1072-shining-advanced-diagnostics` with `contracts/` and `tasks.md` available.
- Static/security scan: added-line scan over changed C# source/tests for secret-bearing patterns returned `NO_MATCHES`. Pre-commit `git diff --check` passed; Git emitted only its existing CRLF normalization warning for `ExplorerShiningAbodeCommandResultBuilder.cs`.
- Frontend/docs impact: no React/Vite files changed, so `npm run verify --prefix BookOfEternityClient.WebFrontend` was not applicable. No runtime contracts, pending/control files, validation/normalizer/schema behavior, GM prompts/examples/manifests, or afterlife docs changed, so afterlife documentation coverage tests were not applicable.
- Follow-ups: #1066 and #1067 remain sibling non-closing follow-ups; #1072 is the only closing task for this implementation.
- Hermes post-Codex verification before review: implementation worktree clean/ahead 1 at `a8843485409ea245a9043302ad0394d94cac1a4a`; client build passed with 0 warnings / 0 errors; tests build passed with 0 warnings / 0 errors; focused #1072 diagnostics test passed 6, failed 0, skipped 0; focused browser/Shining/registry/audit slice passed 252, failed 0, skipped 0; broad afterlife/Shining/browser slice passed 1878, failed 0, skipped 0; Spec Kit prerequisite resolved `specs/1072-shining-advanced-diagnostics`; `git diff --check origin/main...HEAD` passed; refined added-line C# static/security scan over changed non-Spec/non-doc code returned `NO_MATCHES`.
- Independent review: detached read-only Codex review run `E:/Games/codex-runs/20260616-214517-review-boe-1072-shining-advanced-diagnostics` against review worktree `E:/Games/review-worktrees/boe-1072-shining-advanced-diagnostics-review`; reviewed head `a8843485409ea245a9043302ad0394d94cac1a4a`; exit code 0; verdict `APPROVED`; Critical issues: none; Important issues: none. Reviewer reran focused diagnostics test (6 passed, 0 failed, 0 skipped), focused browser/Shining/registry/audit slice (252 passed, 0 failed, 0 skipped), and `git diff --check origin/main...HEAD` (exit 0). Detached Spec Kit prerequisite reported `Not on a feature branch`, classified as review-harness caveat because the implementation worktree prerequisite already resolved the active feature directory. Minor non-blocking suggestion: consider future regression fixture for absent/valid-sparse state files.
- Hermes PR creation: created https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1073 with `Closes #1072`, local-gated verification evidence, Spec Kit links, origin #1065/#949 context, and #1066/#1067 as sibling non-closing references. PR readback reported `headRefOid=efcaaeded2ce9496fa730c173d068af1b357009d`, `mergeStateStatus=CLEAN`, and closing issues only #1072 before this PR-evidence amend.
