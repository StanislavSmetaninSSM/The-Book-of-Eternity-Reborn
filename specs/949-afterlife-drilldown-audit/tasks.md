---
description: "Task list for #949 afterlife detail drill-down audit"
---

# Tasks: Afterlife Detail Drill-Down Audit

**Input**: `specs/949-afterlife-drilldown-audit/spec.md`, `plan.md`, `contracts/afterlife-drilldown-audit.md`, `checklists/requirements.md`, and GitHub issue #949.

**Prerequisites**: AGENTS.md, `.specify/memory/constitution.md`, #949 body, #948/#1054/#1055/#1056/#1057 audit/drill-down patterns, afterlife contract documentation guardrails, browser Reborn panel/detail/action-result references, and current Browser Client direction.

**Tests**: Behavior changes require test-first work. Add/update audit/source guards before changing production behavior. If a small fix is implemented, prove RED failure first, then GREEN.

## Phase 1: Setup and Investigation

- [X] T001 Hermes created ASCII worktree `E:/Games/worktrees/boe-949-afterlife-drilldown-audit` on branch `949-afterlife-drilldown-audit` from `origin/main`.
- [X] T002 Hermes created active Spec Kit feature directory `specs/949-afterlife-drilldown-audit` and linked source issue #949 in spec/plan/tasks/contract/checklist.
- [X] T003 Hermes read `AGENTS.md`, `.specify/memory/constitution.md`, #949 body, relevant Book references, `spec.md`, `plan.md`, contract, checklist, and this task file before Codex launch. Codex must still inspect nearby implementation code/tests/docs before editing.
- [X] T004 Inspect current handling/tests for afterlife commands/surfaces in `BookOfEternityClient/`, console ExplorerMode handlers, command catalog/aliases, browser command-result services, and existing afterlife docs/tests.
- [X] T005 Inspect existing good patterns for guardian list/detail, soul relic/archive inbox detail, guardian project detail, and #948 mortal audit artifact structure.
- [X] T006 Hermes recorded exact baseline commands and counts before production changes.

## Phase 2: Audit Artifact and RED Guards

- [X] T007 Create `docs/audits/afterlife-drilldown-audit.md` covering all #949 candidate categories: guardians, abodes/abode power, soul relics, archive candidates, guardian local systems, Shining systems, spiritual conflict, and afterlife profile/support surfaces.
- [X] T008 Add a focused audit/source guard that fails when the audit artifact omits required categories, classification values, severity, console/browser parity notes, or follow-up links for `follow-up required` rows.
- [X] T009 Run the new audit/source guard before finishing the artifact and record the expected RED failure in the Evidence Log.
- [X] T010 Fill the audit artifact with current-state classifications: `adequate`, `fixed in #949`, `follow-up required`, or `not applicable`, including player-facing reasons.
- [X] T011 Create GitHub follow-up issues for every confirmed larger `follow-up required` gap and link those issue URLs/numbers in the audit artifact.

## Phase 3: Optional Small Safe Fixes

- [X] T012 Decide whether any confirmed gap is small enough to fix inside #949 under the contract: read-only, overview-preserving, shared C# pattern, no runtime/GM contract change.
- [X] T013 For each small in-PR fix, write a focused failing test first proving the selected detail path is missing or raw-only/generic-completion.
- [X] T014 Run the focused small-fix test before production code and record the expected RED failure in the Evidence Log.
- [X] T015 Implement the minimal shared C# command-result/detail change for any approved small fix.
- [X] T016 Preserve overview output and graceful missing/sparse-data states for every touched afterlife surface.
- [X] T017 Ensure default detail output uses Russian/in-world player-facing labels and does not expose raw JSON/API/DTO/debug/local path copy as ordinary player content.
- [X] T018 If a desired fix requires runtime contract/pending/control/validation/GM doc changes, stop that fix, create a follow-up issue, and do not implement it in #949 unless the same PR updates required docs/tests.

## Phase 4: Verification and Review Prep

- [X] T019 Run focused #949 audit/source-guard tests and record exact pass/fail/skip counts.
- [X] T020 Run the broader afterlife/browser/console slice and record exact pass/fail/skip counts.
- [X] T021 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [X] T022 Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` if afterlife runtime contracts/docs/examples/manifests change.
- [X] T023 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files changed.
- [X] T024 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/949-afterlife-drilldown-audit`.
- [X] T025 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T026 Update this Evidence Log with RED/GREEN counts, follow-up issue URLs, docs impact, and verification results.

## Phase 5: Hermes-Owned Review and Closure

- [X] T028 Hermes launches detached independent review before PR/merge and records run/verdict.
- [X] T029 Hermes resolves Critical/Important review findings, reruns fresh gates, and obtains clean review/re-review.
- [X] T030 Hermes creates PR with `Closes #949`, local-gated verification evidence, Spec Kit links, and follow-up issue links as non-closing references unless any follow-up is fully satisfied.
- [ ] T031 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #949 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up worktree/branch, and reports closure.

## Notes

- #949 is intentionally the afterlife audit closure unit. Do not implement all confirmed drill-down gaps in one PR.
- Larger afterlife gaps must become linked GitHub issues with precise scopes.
- Do not change afterlife runtime contracts, pending/control files, validation, normalizer behavior, GM prompts/examples, or manifests without same-PR docs/tests or a tracked follow-up.
- T028-T031 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/boe-949-afterlife-drilldown-audit` on branch `949-afterlife-drilldown-audit` from `origin/main` at `46e044e63a4de29e71f83597e0b63c9499511522` and created this Spec Kit feature directory for issue #949.
- Source issue: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949.
- Issue ownership: Hermes added label `status: in-progress` to #949 before launching Codex.
- Spec Kit verification before Codex: `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH && specify version` returned CLI `0.9.3`; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-949-afterlife-drilldown-audit\\specs\\949-afterlife-drilldown-audit`.
- Baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~Chaos|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1884, failed 0, skipped 0, total 1884.
- Codex investigation: inspected `AGENTS.md`, `.specify/memory/constitution.md`, this feature's spec/plan/tasks/contract/checklist, `docs/audits/mortal-readonly-drilldown-audit.md`, `specs/1057-mortal-reference-detail-actions/`, `ExplorerCommandCatalog`, afterlife command-result builders, `ExplorerWebCommandService`, console ExplorerMode afterlife handlers, and afterlife/browser/console tests before editing.
- Audit findings: `docs/audits/afterlife-drilldown-audit.md` covers guardians, abodes/abode power, soul relics, archive candidates, guardian local systems, Shining Abode systems, spiritual conflict, and afterlife profile/support surfaces. Rows include classification, severity, console parity, browser parity, player-facing reason, docs/contract impact, and follow-up links where required.
- Follow-up issue created: #1063 Chaos Sea Guardian/Abode browser drill-downs - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1063.
- Follow-up issue created: #1064 Soul relic/archive browser drill-downs - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1064.
- Follow-up issue created: #1065 Shining Abode browser inspection drill-downs - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1065.
- Follow-up issue created: #1066 Afterlife profile/inbox follow-through drill-downs - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1066.
- Follow-up issue created: #1067 Spiritual conflict exchange/art drill-downs - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1067.
- RED guard: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` failed as expected before the artifact existed: passed 0, failed 3, skipped 0, total 3. Failure reason: missing `docs/audits/afterlife-drilldown-audit.md`.
- #949 behavior decision: no small C# behavior fix was implemented. Confirmed detail gaps are broader than the safe #949 slice and were split to #1063-#1067. T013-T017 are therefore not applicable beyond the audit/source guard; no production afterlife command/result behavior changed.
- Docs/prompts impact: changed only the new audit artifact and Spec Kit task evidence. No afterlife runtime contracts, pending/control files, validation rules, normalizer side effects, GM prompts, worked examples, manifests, or frontend files changed. Therefore `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests` and `npm run verify --prefix BookOfEternityClient.WebFrontend` were not required.
- Focused GREEN guard: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 3, failed 0, skipped 0, total 3.
- Broad afterlife/browser/console slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~Chaos|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1887, failed 0, skipped 0, total 1887.
- C# build verification: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` completed with 0 warnings and 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` completed with 0 warnings and 0 errors.
- Spec Kit prerequisite verification after changes: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-949-afterlife-drilldown-audit\\specs\\949-afterlife-drilldown-audit`.
- Diff/static checks: `git diff --check origin/main...HEAD` produced no output; added-line scan over changed non-plan C# code found no matches for credential, dangerous shell, serialization, SQL, or eval patterns.
- Commit gate: implementation completed in a focused commit with message `docs: audit afterlife drilldown gaps [skip ci]`.
- Independent review: Hermes launched detached read-only review from `3d2cfa57f27442a27c4b0dcf00e4050349acb267` using review worktree `E:/Games/review-worktrees/boe-949-afterlife-drilldown-audit-review-20260616-142242` and run dir `E:/Games/codex-runs/20260616-142242-review-boe-949-afterlife-drilldown-audit`. Review `exit-code.txt` was `0`; `final.md` verdict was `APPROVED`; Critical/Important findings: none. Review harness limitation: Spec Kit branch helper failed in detached HEAD, while implementation-worktree prerequisite checks had already passed.
- Review note resolved before PR: audit severity values were aligned with the contract vocabulary (`P2 medium`, `P3 low`; no `P4`) without changing product scope or follow-up classification.
- PR creation: Hermes created PR #1068 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1068`) with `Closes #949`, local-gated verification evidence, Spec Kit links, and follow-up issue references #1063-#1067 in a non-closing section. GitHub readback after parser update showed `mergeStateStatus=CLEAN`, `state=OPEN`, head `9afc3d44308058bb1f592451e3ea93b5c4969cd9`, and closing issue reference only #949.
