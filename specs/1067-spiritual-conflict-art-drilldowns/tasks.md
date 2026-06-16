# Tasks: Spiritual Conflict Exchange and Art Drill-Downs

**Input**: `specs/1067-spiritual-conflict-art-drilldowns/spec.md`, `plan.md`, #1067 issue body, #949 AFD-006 audit row.

**Source issue**: #1067 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1067

**Origin audit**: #949 AFD-006 — `docs/audits/afterlife-drilldown-audit.md`

## Execution Rules

- Follow Superpowers TDD: write focused failing tests before production code and record RED/GREEN evidence.
- Keep the feature bounded to read-only selected-detail/action presentation for spiritual conflict/log/art rows.
- Do not change spiritual-conflict dice, reward, validation, pending/control, normalizer, GM prompt/example/manifest, or write authority unless a true contract gap is found and required docs/tests are updated in the same PR.
- Hermes owns independent review, PR, merge, issue comment/closure, label transition, and cleanup.

## Phase 0 - Setup and Baseline Evidence

- [X] **T001** Verify the issue/worktree context: branch `work/1067-spiritual-conflict-art-drilldowns`, issue #1067 labeled `status: in-progress`, and `git status --short --branch` recorded.
- [X] **T002** Read governance and scope sources: `AGENTS.md`, `.specify/memory/constitution.md`, #1067 issue body, #949 AFD-006 row, `docs/audits/afterlife-drilldown-audit.md`, and `references/afterlife-drilldown-child-launch.md` summary from Hermes prompt.
- [X] **T003** Run Spec Kit prerequisite check and record output:
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- [X] **T004** Run focused baseline and record counts:
  `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SpiritualConflict|FullyQualifiedName~SpiritualCombat|FullyQualifiedName~SpiritualArts|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"`
- [X] **T005** Run broad afterlife/browser/console baseline and record counts:
  `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`

## Phase 1 - RED Tests for Exchange and Log Details

- [X] **T006** Add focused tests for `/spiritual_conflict` overview exposing safe read-only detail actions for concrete `exchangeLog[]` rows. Verify these tests fail before production changes because actions/details are missing.
- [X] **T007** Add focused tests for selected active-conflict exchange detail rendering: actor/opposition, action, dice/contest context, position/tension changes, costs, outcome, rewards/reasons where present, no raw/default diagnostic leakage.
- [X] **T008** Add focused tests for `/spiritual_combat_log` overview exposing safe read-only detail actions for concrete log/recent-conflict rows. Verify these tests fail before production changes because actions/details are missing.
- [X] **T009** Add focused tests for selected combat-log/recent-conflict detail rendering and hidden/gm-only suppression in default mode.

## Phase 2 - RED Tests for Spiritual Art Details and Safety

- [X] **T010** Add focused tests for `/spiritual_arts` exposing read-only inspect actions for concrete spiritual-art rows while preserving existing local-turn upgrade/write actions.
- [X] **T011** Add focused tests for selected spiritual-art details: rank/level, cost/action-point impact, effect/use context, availability, and player-facing write-boundary copy.
- [X] **T012** Add missing/stale/sparse/malformed target tests for exchange/log/art details. Default output must be Russian/in-world and must not include raw JSON, `JsonException`, `Path:`, `LineNumber`, `BytePositionInLine`, local paths, file names, API/DTO/endpoint/protocol/debug wording, or hidden/gm-only fields.
- [X] **T013** Add no-mutation tests proving read-only detail actions do not create pending/control files, route through prompt/write services, or mutate spiritual-conflict/spiritual-arts state.

## Phase 3 - Minimal Implementation

- [X] **T014** Inspect #1063-#1066 selected-detail patterns and reuse existing action metadata/command-result helpers where possible.
- [X] **T015** Implement `/spiritual_conflict` read-only exchange detail actions and selected-detail rendering in shared C# command-result code.
- [X] **T016** Implement `/spiritual_combat_log` read-only log/recent-conflict detail actions and selected-detail rendering.
- [X] **T017** Implement `/spiritual_arts` read-only art inspect actions/details without bypassing existing local-turn upgrade/write authority.
- [X] **T018** Implement safe unavailable/default malformed output helpers for unsupported, stale, sparse, or malformed selected targets if existing helpers are insufficient.
- [X] **T019** Preserve overview/help output and contextual `/spiritual_combat_help` links only where useful; do not create help-row entity lifecycle.

## Phase 4 - Verification and Evidence

- [X] **T020** Run focused #1067 tests and record GREEN counts.
- [X] **T021** Run focused afterlife/browser slice and record counts:
  `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SpiritualConflict|FullyQualifiedName~SpiritualCombat|FullyQualifiedName~SpiritualArts|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"`
- [X] **T022** Run broad afterlife/browser/console slice and record counts:
  `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`
- [X] **T023** Run C# builds when C# files changed:
  `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  and
  `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- [X] **T024** Run Spec Kit prerequisite check again and `git diff --check origin/main...HEAD`.
- [X] **T025** Run added-line static/security scan over production/test C# diff, excluding Spec Kit docs when plan wording false-positives.
- [X] **T026** If runtime/GM contract files changed, run `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests` and update required docs/examples/manifests in the same PR. If not changed, record the no-docs-impact rationale.
- [X] **T027** If React/Vite files changed, run `npm run verify --prefix BookOfEternityClient.WebFrontend`; otherwise record why frontend verify is not needed.

## Phase 5 - Codex Commit Handoff

- [X] **T028** Update this task file with RED/GREEN and verification evidence for implementation-owned tasks.
- [X] **T029** Commit focused implementation changes with `[skip ci]` in the commit message.
- [X] **T030** Codex final report must include summary, files changed, verification commands/results, docs/prompts impact, Spec Kit drift, and remaining risks.

## Phase 6 - Hermes-Owned Lifecycle Tasks

- [X] **T031** Hermes runs independent read-only implementation review before PR/merge.
- [X] **T032** Hermes fixes or launches a focused fix run for any critical/important review findings.
- [X] **T033** Hermes creates/updates PR with `Closes #1067` only; sibling issue references must be non-closing.
- [ ] **T034** Hermes performs final local verification, squash-merges after local gates, deletes remote branch, verifies issue #1067 is closed, transitions label to `status: verified`, and cleans worktrees.
- [ ] **T035** Hermes sends final Russian closure report and does not create a cosmetic PR solely to tick post-merge lifecycle boxes.

## Evidence Log

- 2026-06-17: Hermes selected #1067 after #1066 was verified closed/reported. Created ASCII worktree `E:/Games/worktrees/1067-spiritual-conflict-art-drilldowns` on branch `work/1067-spiritual-conflict-art-drilldowns` and labeled #1067 `status: in-progress`.
- 2026-06-17: Spec Kit CLI `specify version` succeeded (`0.9.3`); prerequisite check resolved `specs/1067-spiritual-conflict-art-drilldowns` with `contracts/` and `tasks.md`.
- 2026-06-17: Focused baseline before RED tests passed: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SpiritualConflict|FullyQualifiedName~SpiritualCombat|FullyQualifiedName~SpiritualArts|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` → 665 passed, 0 failed, 0 skipped.
- 2026-06-17: Broad afterlife/browser/console baseline passed: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` → 1450 passed, 0 failed, 0 skipped.
- 2026-06-17: RED focused #1067 tests failed before production implementation as expected: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns" --logger "console;verbosity=minimal"` → 0 passed, 18 failed, 0 skipped. Failures covered missing exchange/log/art actions, raw default diagnostics, malformed parser leaks, and catalog argument support.
- 2026-06-17: GREEN focused #1067 tests passed after implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns" --logger "console;verbosity=minimal"` → 18 passed, 0 failed, 0 skipped.
- 2026-06-17: Focused afterlife/browser slice passed after updating the legacy default-mode raw-audit expectation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SpiritualConflict|FullyQualifiedName~SpiritualCombat|FullyQualifiedName~SpiritualArts|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` → 683 passed, 0 failed, 0 skipped.
- 2026-06-17: Broad afterlife/browser/console slice passed: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` → 1468 passed, 0 failed, 0 skipped.
- 2026-06-17: C# builds passed with 0 warnings and 0 errors: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- 2026-06-17: Repeat Spec Kit prerequisite check passed and resolved `specs/1067-spiritual-conflict-art-drilldowns` with `contracts/` and `tasks.md`; `git diff --check` over the working diff passed.
- 2026-06-17: Added-line static/security scan over production/test C# diff found no hits for credential/token/key patterns.
- 2026-06-17: Docs/prompts impact: implementation stayed presentation/read-only in shared C# command-result/browser catalog/tests. No dice, reward, validation, pending/control, normalizer, GM prompts, examples, manifests, or afterlife runtime contract files changed; docs coverage tests were not required.
- 2026-06-17: Frontend impact: no React/Vite files changed; `npm run verify --prefix BookOfEternityClient.WebFrontend` was not required.
- 2026-06-17: Post-commit `git diff --check origin/main...HEAD` passed.
- 2026-06-17: Codex committed local implementation with message `Add spiritual conflict drill-down actions [skip ci]`; Hermes lifecycle tasks T031-T035 remain pending for independent review/PR/merge/closure.
- 2026-06-17: Hermes reconciled detached read-only review run `E:/Games/codex-runs/20260617-040446-review-boe-1067-spiritual-conflict-art-drilldowns` against review worktree `E:/Games/review-worktrees/boe-1067-spiritual-conflict-art-drilldowns-review-20260617-040446`. Review verdict: `APPROVED`; critical issues: none; important issues: none; minor non-blocking future cleanup note only for `/spiritual_arts` overview malformed-state warning titles outside selected-detail scope.
- 2026-06-17: Hermes reran fresh pre-PR gates on implementation branch `work/1067-spiritual-conflict-art-drilldowns`: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings/0 errors; focused `FullyQualifiedName~SpiritualConflictArtDrilldowns` passed 18/18; broad afterlife/spiritual/browser/console filter passed 1470/1470; Spec Kit prerequisite resolved this feature directory; `git diff --check origin/main...HEAD` passed; refined added-line C# static/security scan returned `NO_MATCHES`.
- 2026-06-17: Hermes created PR #1075 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1075`) with exactly one closing reference, `Closes #1067`; `gh pr view` reported `mergeStateStatus: CLEAN`, `headRefOid: f4550b95d446150483033fcbfa460b0905eb4723`, and closing issue #1067 only before the PR-lifecycle evidence amend.
