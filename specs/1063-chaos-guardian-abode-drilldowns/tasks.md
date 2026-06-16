---
description: "Task list for #1063 Chaos Sea Guardian/Abode browser drill-downs"
---

# Tasks: Chaos Sea Guardian/Abode Browser Drill-Downs

**Input**: `specs/1063-chaos-guardian-abode-drilldowns/spec.md`, `plan.md`, `contracts/browser-chaos-guardian-abode-detail-actions.md`, `checklists/requirements.md`, GitHub issue #1063, #949 audit row AFD-001.

**Prerequisites**: AGENTS.md, `.specify/memory/constitution.md`, #1063 body, #949 audit/spec, Browser Reborn panel/detail-surface references, #1057 mortal reference detail action patterns, and current Browser Client direction.

**Tests**: Strict TDD for behavior changes. Write focused failing tests/source guards before production code; verify RED failure for missing detail actions/selected-detail behavior; then implement minimal GREEN changes.

## Phase 1: Setup and Investigation

- [X] T001 Hermes created ASCII worktree `E:/Games/worktrees/boe-1063-chaos-guardian-abode-drilldowns` on branch `1063-chaos-guardian-abode-drilldowns` from `origin/main`.
- [X] T002 Hermes created active Spec Kit feature directory `specs/1063-chaos-guardian-abode-drilldowns` and linked source issue #1063 plus origin audit #949 in spec/plan/tasks/contract/checklist.
- [X] T003 Hermes read `AGENTS.md`, `.specify/memory/constitution.md`, #1063 body, Browser Reborn/detail references, #949 audit context, and current command-result patterns before Codex launch. Codex must still inspect nearby implementation code/tests/docs before editing.
- [X] T004 Inspect current command catalog descriptors and parser behavior for `/guardians`, `/abodes`, `/abode_power`, and `/guardian_projects`, including aliases and argument acceptance.
- [X] T005 Inspect current browser command service dispatch and shared C# result builders for the covered commands.
- [X] T006 Inspect console ExplorerMode Guardian/Abode selectors/detail panels for equivalent player-facing semantics.
- [X] T007 Inspect #1057 mortal reference detail action tests/implementation as the preferred shared C# pattern.
- [X] T008 Hermes records exact baseline commands and counts before production changes.

## Phase 2: RED Tests and Source Guards

- [X] T009 Add focused RED tests proving `/guardians`, `/abodes`, `/abode_power`, and `/guardian_projects` overview results lack the required browser detail actions for seeded rich data.
- [X] T010 Add focused RED tests proving selected-detail commands for representative Guardian/Abode/power/project entries currently fail, are missing, or collapse into raw/generic output.
- [X] T011 Add or update source guards ensuring default browser detail output avoids `game_state/`, API, DTO, endpoint, debug, raw JSON, and `UiRawJsonBlock` leakage.
- [X] T012 Run the focused tests/source guards before production code and record expected RED failure counts/reasons in the Evidence Log.

## Phase 3: Shared C# Detail Action Implementation

- [X] T013 Ensure relevant Explorer command descriptors accept arguments where the selected-detail grammar needs them.
- [X] T014 Add shared C# detail actions to overview command results while preserving all existing overview blocks/tables/sections.
- [X] T015 Implement selected-detail result handling for one focused Guardian, Abode, Abode power entry/section, and Guardian project using existing canonical data.
- [X] T016 Implement graceful missing/sparse/unknown id states with Russian/in-world unavailable explanations.
- [X] T017 Keep implementation read-only: no pending/control writes, write-service calls, validation/normalizer/schema changes, or React-side gameplay authority.
- [X] T018 If a desired sub-surface requires broader runtime/GM contract work, stop that sub-surface and create/link a focused follow-up rather than expanding #1063.

## Phase 4: Verification and Review Prep

- [X] T019 Run focused Browser/afterlife command tests and record exact pass/fail/skip counts.
- [X] T020 Run the broader afterlife/browser/console slice and record exact pass/fail/skip counts.
- [X] T021 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [X] T022 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files changed.
- [X] T023 Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` if afterlife runtime contracts/docs/examples/manifests change.
- [X] T024 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1063-chaos-guardian-abode-drilldowns`.
- [X] T025 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T026 Update this Evidence Log with RED/GREEN counts, docs/prompts impact, verification results, and any follow-up issue links.
- [X] T027 Commit the focused implementation with `[skip ci]`.

## Phase 5: Hermes-Owned Review and Closure

- [X] T028 Hermes launches detached independent review before PR/merge and records run/verdict.
- [X] T029 Hermes resolves Critical/Important review findings, reruns fresh gates, and obtains clean review/re-review.
- [X] T030 Hermes creates PR with `Closes #1063`, local-gated verification evidence, Spec Kit links, #949 reference, and sibling issues as non-closing references unless fully satisfied.
- [ ] T031 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1063 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up worktree/branch, and reports closure.

## Notes

- #1063 is the first #949 child follow-up. Keep #1064-#1067 separate unless a change fully and verifiably satisfies a sibling.
- Do not change afterlife runtime contracts, pending/control files, validation, normalizer behavior, GM prompts/examples, or manifests without same-PR docs/tests or a tracked follow-up.
- T028-T031 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/boe-1063-chaos-guardian-abode-drilldowns` on branch `1063-chaos-guardian-abode-drilldowns` from `origin/main` at `29603e9bc1ace3c57ca7437af4afc6782426d36d` and created this Spec Kit feature directory for issue #1063.
- Source issue: #1063 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1063.
- Origin audit: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949; `docs/audits/afterlife-drilldown-audit.md` row AFD-001.
- Spec Kit verification before Codex: `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH && specify version` returned CLI `0.9.3`; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1063-chaos-guardian-abode-drilldowns\\specs\\1063-chaos-guardian-abode-drilldowns`.
- Issue ownership: Hermes added label `status: in-progress` to #1063 before launching Codex.
- Focused baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 194, failed 0, skipped 0, total 194.
- Broad afterlife/browser/console baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Chaos|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1460, failed 0, skipped 0, total 1460.
- Codex investigation: confirmed the target Chaos Sea catalog descriptors were read-only parity but did not accept arguments; `ExplorerWebCommandService` discarded Chaos Sea arguments while Mortal World/Shining builders preserved them; `ExplorerChaosSeaCommandResultBuilder` returned default raw JSON overviews and had no selected-detail handling; console Guardian/Abode panels and #1057 Mortal World detail actions provided the player-facing/detail-action pattern.
- RED focused test/source-guard run before production code: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ChaosSeaGuardianAbodeDetailActions_CatalogAllowsReadOnlyDetailArguments|FullyQualifiedName~ExecuteAsync_ChaosSeaGuardianAbode" --logger "console;verbosity=minimal"` failed 13, passed 0, skipped 0, total 13. Expected failures: target descriptors lacked `AcceptsArguments`, selected-detail commands collapsed to overview output, and default target overviews exposed `UiRawJsonBlock` raw JSON.
- GREEN focused test/source-guard run after implementation: same focused command passed 13, failed 0, skipped 0, total 13.
- Focused Browser/afterlife command suite after implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 207, failed 0, skipped 0, total 207.
- Broad afterlife/browser/console slice after implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Chaos|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1473, failed 0, skipped 0, total 1473.
- C# builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings, 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` succeeded with 0 warnings, 0 errors.
- Frontend verification: not run; no frontend files changed.
- Docs/prompts/runtime-contract verification: not run; implementation stayed in read-only command-result/catalog/service/tests and did not change runtime schema, pending/control files, validation, normalizer behavior, GM prompts, examples, manifests, or GM-authored behavior.
- Spec Kit prerequisite after implementation: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\Games\worktrees\boe-1063-chaos-guardian-abode-drilldowns\specs\1063-chaos-guardian-abode-drilldowns`.
- Diff/static checks: `git diff --check origin/main...HEAD` produced no output; added-line static/security scan over changed C# production/test diff found no secret, credential, pending/control, or production write matches.
- Follow-ups: none created; all four #1063 target surfaces fit within the bounded task slice without runtime/GM contract expansion.
- Hermes reconciliation after Codex implementation: run `E:/Games/codex-runs/20260616-145157-boe-1063-chaos-guardian-abode-drilldowns` exited 0; branch `1063-chaos-guardian-abode-drilldowns` was clean/ahead 1 at `778dc7fa88248bc13404ca5de4718436fa2eb278`; no PR existed before independent review.
- Fresh Hermes verification before review: focused #1063 slice passed 16/16; focused audit/web/registry slice passed 207/207; broad afterlife/browser/console slice passed 1473/1473; client/test builds passed with 0 warnings and 0 errors; Spec Kit prerequisite resolved this feature directory; `git diff --check origin/main...HEAD` passed; refined added-line C# static/security scan returned `NO_MATCHES`; no frontend files changed.
- Independent review: detached read-only Codex review run `E:/Games/codex-runs/20260616-052111-review-boe-1063-chaos-guardian-abode-drilldowns` reviewed commit `778dc7fa88248bc13404ca5de4718436fa2eb278` from worktree `E:/Games/review-worktrees/boe-1063-chaos-guardian-abode-drilldowns-review-20260616-052111`; exit code 0; verdict `APPROVED`; Critical/Important/Minor findings: none. Review re-ran `git diff --check` and focused #1063 tests, passing 13/13.
- PR creation: Hermes pushed branch `1063-chaos-guardian-abode-drilldowns` and created PR #1069 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1069 — with `Closes #1063`, local-gated verification evidence, Spec Kit links, #949 audit reference, and #1064-#1067 as sibling non-closing references. Initial PR readback: `OPEN`, `mergeStateStatus=CLEAN`, closing references only #1063, `headRefOid=831b58d6311df404e09e70f2f4156f54226ae492`.
