---
description: "Task list for #1066 afterlife profile/inbox follow-through drill-downs"
---

# Tasks: Afterlife Profile and Inbox Follow-Through Drill-Downs

**Input**: `specs/1066-afterlife-profile-inbox-drilldowns/spec.md`, `plan.md`, `contracts/browser-afterlife-profile-inbox-drilldowns.md`, `checklists/requirements.md`, GitHub issue #1066, #949 AFD-005.

**Prerequisites**: AGENTS.md, `.specify/memory/constitution.md`, #1066 body, #949 audit row, afterlife drill-down child launch guidance, Browser action result/detail surface guidance, Browser Afterlife relationship-gate boundary notes, and current command-result patterns.

**Tests**: Strict TDD for behavior changes. Write focused failing tests/source guards before production code; verify RED failure for missing detail/follow-through action exposure or unsafe missing-target behavior; then implement minimal GREEN changes.

## Phase 1: Setup and Investigation

- [X] T001 Hermes created ASCII worktree `E:/Games/worktrees/1066-afterlife-profile-inbox-drilldowns` on branch `work/1066-afterlife-profile-inbox-drilldowns` from `origin/main`.
- [X] T002 Hermes created active Spec Kit feature directory `specs/1066-afterlife-profile-inbox-drilldowns` and linked source issue #1066 plus origin #949 AFD-005 in spec/plan/tasks/contract/checklist.
- [X] T003 Hermes marked #1066 as `status: in-progress` after branch/worktree creation.
- [X] T004 Hermes records exact baseline commands and counts before production changes.
- [X] T005 Codex inspects `AGENTS.md`, `.specify/memory/constitution.md`, #1066, active Spec Kit artifacts, #949 AFD-005 audit row, and nearby afterlife/browser command-result code before editing.
- [X] T006 Inspect current `/afterlife_profiles`, `/afterlife_threats`, `/afterlife_chronicles`, and `/afterlife_inbox` command dispatch, command-result blocks/actions, selected-detail argument patterns, and advanced/debug-mode flag handling.
- [X] T007 Inspect #1063-#1065 selected-detail implementation/tests as preferred read-only follow-through patterns.
- [X] T008 Map concrete stable IDs and supported inbox/support target link types that can remain read-only.

## Phase 2: RED Tests and Source Guards

- [X] T009 Add focused RED tests proving `/afterlife_profiles` overview lacks required safe selected-detail action exposure or selected detail rendering.
- [X] T010 Add focused RED tests proving `/afterlife_threats` and `/afterlife_chronicles` overview/detail paths lack required safe selected-detail action exposure or hidden/gm-only boundaries.
- [X] T011 Add focused RED tests proving `/afterlife_inbox` follow-through is incomplete for at least one supported notification/context link type.
- [X] T012 Add missing/stale/unsupported target coverage that expects Russian/in-world unavailable output with no raw IDs, raw JSON, API/DTO/debug/protocol wording, or local paths.
- [X] T013 Add or update source guards/tests proving read-only follow-through does not auto-mark inbox notifications read and does not create pending/write state.
- [X] T014 Run the focused tests/source guards before production code and record expected RED failure counts/reasons in the Evidence Log.

## Phase 3: Shared C# Detail/Follow-Through Implementation

- [X] T015 Implement shared C# command-result/action metadata so default profile/threat/chronicle overviews expose read-only selected-detail actions for concrete visible rows.
- [X] T016 Implement selected-detail rendering for scoped profile/threat/chronicle rows using canonical visible state and player-facing Russian/in-world copy.
- [X] T017 Implement `/afterlife_inbox` read-only follow-through actions for supported profile, threat, chronicle, Guardian, archive, Shining, resident, project, and trade context links where current canonical state can resolve them safely.
- [X] T018 Implement safe missing/stale/unsupported target results without raw diagnostic leakage or generic completion text.
- [X] T019 Preserve existing overview output and existing mutating prompt/write authority; no new pending/control files, write-service calls, validation/normalizer/schema changes, or React-side gameplay authority.
- [X] T020 If a required sub-surface needs runtime/GM contract work, stop that sub-surface and create/link a focused follow-up rather than expanding #1066.

## Phase 4: Verification and Review Prep

- [X] T021 Run focused profile/threat/chronicle/inbox detail/follow-through tests and record exact pass/fail/skip counts.
- [X] T022 Run the broader afterlife/browser command slice and record exact pass/fail/skip counts.
- [X] T023 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [X] T024 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files changed.
- [X] T025 Run documentation-sensitive afterlife tests if runtime contracts/docs/examples/manifests change.
- [X] T026 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1066-afterlife-profile-inbox-drilldowns`.
- [X] T027 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T028 Update this Evidence Log with RED/GREEN counts, docs/prompts impact, verification results, and any follow-up issue links.
- [X] T029 Commit the focused implementation with `[skip ci]`.

## Phase 5: Hermes-Owned Review and Closure

- [X] T030 Hermes launched detached independent review before PR/merge and recorded run/verdict.
- [X] T031 Hermes resolved Critical/Important review findings, reran fresh gates, and obtained clean review/re-review.
- [X] T032 Hermes created PR with `Closes #1066`, local-gated verification evidence, Spec Kit links, #949 references, and #1067 as sibling non-closing reference unless fully satisfied.
- [ ] T033 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1066 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up worktree/branch, and reports closure.

## Notes

- #1066 is the AFD-005 child from #949. Keep #1067 spiritual conflict/art drill-downs separate unless a change fully and verifiably satisfies it.
- Preserve read-only boundaries unless a tracked issue/spec explicitly authorizes runtime/GM contract changes.
- Do not change afterlife runtime contracts, pending/control files, validation, normalizer behavior, GM prompts/examples, or manifests without same-PR docs/tests or a tracked follow-up.
- T030-T033 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/1066-afterlife-profile-inbox-drilldowns` on branch `work/1066-afterlife-profile-inbox-drilldowns` from `origin/main` at `25b317a93fde7f4897fb0beca8a33b6c6c425cdc` and created this Spec Kit feature directory for issue #1066.
- Source issue: #1066 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1066.
- Origin audit: #949 AFD-005 records the scoped gap for `/afterlife_profiles`, `/afterlife_threats`, `/afterlife_chronicles`, `/afterlife_inbox`, and read-only inbox/support follow-through.
- Spec Kit basis: `.specify/memory/constitution.md` version 1.1.0 requires Spec Kit for player-facing UX, console/browser parity, and afterlife work; #1066 meets that policy.
- Issue ownership: Hermes added label `status: in-progress` to #1066 before launching Codex.
- Spec Kit baseline: `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH && specify version` returned CLI `0.9.3`; `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\1066-afterlife-profile-inbox-drilldowns\\specs\\1066-afterlife-profile-inbox-drilldowns` with `contracts/` and `tasks.md` available.
- Focused baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeProfiles|FullyQualifiedName~AfterlifeThreats|FullyQualifiedName~AfterlifeChronicles|FullyQualifiedName~AfterlifeInbox|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 257, failed 0, skipped 0, total 257.
- Broad afterlife/browser baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1437, failed 0, skipped 0, total 1437.
- Codex inspection: read `AGENTS.md`, `.specify/memory/constitution.md`, this feature's spec/plan/tasks/contract/checklist, and #949 AFD-005 audit row before editing. Inspected browser command dispatch/catalog and nearby #1063-#1065 detail/action patterns.
- RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns" --logger "console;verbosity=minimal"` failed as expected before production changes: passed 0, failed 12, skipped 0, total 12. Failures covered missing selected-detail args/actions, default raw/technical leakage, unsafe missing inbox detail handling, and read-only inbox follow-through/no-mutation boundaries.
- Implementation: enabled argument preservation for the four scoped afterlife browser commands, added player-facing selected-detail renderers/actions for profile/threat/chronicle rows, added read-only inbox follow-through actions for resolvable linked Guardian/profile/threat/chronicle/archive/project/Shining contexts, and kept inbox write authority behind the existing prompt/write service.
- Focused GREEN: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns" --logger "console;verbosity=minimal"` passed 12, failed 0, skipped 0, total 12.
- Focused afterlife/browser slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeProfiles|FullyQualifiedName~AfterlifeThreats|FullyQualifiedName~AfterlifeChronicles|FullyQualifiedName~AfterlifeInbox|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"` passed 269, failed 0, skipped 0, total 269.
- Broad afterlife/browser/console slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` passed 1449, failed 0, skipped 0, total 1449.
- C# builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings and 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings and 0 errors.
- Spec Kit prerequisite check: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\1066-afterlife-profile-inbox-drilldowns\\specs\\1066-afterlife-profile-inbox-drilldowns` and reported `contracts/` plus `tasks.md`.
- Diff/static checks before commit: `git diff --check` passed with no whitespace errors; added-line credential scan over changed C# code/tests found no `api_key`, password, authorization bearer, private-key, client-secret, access-token, refresh-token, or auth-token markers.
- Post-commit diff/static checks: `git diff --check origin/main...HEAD` passed with no whitespace errors; committed-diff credential scan over changed C# code/tests found no `api_key`, password, authorization bearer, private-key, client-secret, access-token, refresh-token, or auth-token markers.
- Docs/prompts impact: no afterlife runtime contracts, pending/control contract shape, validation, normalizer, GM prompts, examples, manifests, daemon/launcher prompt entrypoints, or React/Vite files were changed. Documentation-sensitive afterlife tests and `npm run verify --prefix BookOfEternityClient.WebFrontend` were not run because those conditional scopes were not touched.
- Follow-up boundary: #1067 spiritual conflict/art drill-downs remain out of scope; no new follow-up was required because #1066 was completed with existing read-only surfaces and no runtime/GM contract expansion.
- Independent review run `E:/Games/codex-runs/20260617-013434-review-boe-1066-afterlife-profile-inbox-drilldowns` at implementation head `12a2f2f39bc317634864515e47c380043914337e` returned `CHANGES_REQUIRED`: Important finding that malformed default `/afterlife_profiles` state could still expose `JSON повреждён` and raw `JsonException` parser diagnostics via `AddRawOrWarning(..., includeRawDiagnostics: false)`.
- Review fix RED: added `ExecuteAsync_AfterlifeProfilesMalformedState_DefaultModeReturnsPlayerFacingUnavailableText`; before production fix the focused #1066 filter failed 1/13 because malformed profile state did not return the expected player-facing unavailable copy.
- Review fix GREEN: updated `AddRawOrWarning` so default/non-advanced paths return player-facing unavailable text while advanced/GM diagnostics still preserve raw parser details. Focused #1066 filter then passed 13, failed 0, skipped 0, total 13.
- Review fix verification: focused afterlife/profile/inbox slice passed 270/270; broad afterlife/browser/console slice passed 1450/1450; both C# builds passed with 0 warnings/0 errors; `git diff --check origin/main...HEAD` was clean; scoped static/security and default player-facing prose/meta scans returned `NO_MATCHES`.
- Independent re-review run `E:/Games/codex-runs/20260617-015131-rereview-boe-1066-afterlife-profile-inbox-drilldowns` at amended head `057cefa27434bdc18af364b5762ad7c5a713628d` returned `APPROVED`; Critical/Important/Minor findings: none. Reviewer reran focused #1066 13/13 and focused afterlife/profile/inbox slice 270/270, checked full diff, prior fix delta, diff-check, targeted raw/debug/parser scans, and clean status.
- PR creation/readback: Hermes created PR #1074 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1074`) with `Closes #1066`; GitHub readback reported `OPEN`, `CLEAN`, and closing references `[1066]` only. #949 and #1067 are mentioned only as non-closing context.
