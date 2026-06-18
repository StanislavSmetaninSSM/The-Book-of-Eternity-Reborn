---
description: "Task list for #1086 console faction detail drill-down menu sections"
---

# Tasks: Console Faction Detail Drill-Down Menu Sections

**Input**: `specs/1086-console-faction-drilldowns/spec.md`, `plan.md`, `contracts/console-faction-detail-drilldowns.md`, and GitHub issue #1086.

**Prerequisites**: AGENTS.md, `.specify/memory/constitution.md`, #1086 body/comment, console faction detail triage lessons, console column alignment lessons, #1085 implementation state, nearby console faction/detail rendering code, and current Console Client direction.

**Tests**: Behavior changes require test-first work. Add or update focused tests/source guards before production code, prove at least one RED failure, then make them pass.

## Phase 1: Setup and Investigation

- [X] T001 Hermes created ASCII worktree `E:/Games/worktrees/boe-1086-console-faction-drilldowns` on branch `work/1086-console-faction-drilldowns` from `origin/main`.
- [X] T002 Hermes created active Spec Kit feature directory `specs/1086-console-faction-drilldowns` and linked source issue #1086 in spec/plan/tasks/contract/checklist.
- [X] T003 Hermes read `AGENTS.md`, `.specify/memory/constitution.md`, #1086 body/comment, console faction detail triage lessons, console column alignment lessons, `spec.md`, `plan.md`, contract, checklist, and this task file before Codex launch. Codex must still inspect nearby implementation code and tests before editing.
- [X] T004 Inspect current handling/tests for selected faction details in `ExplorerMode.FactionsAndWorldNews`, `ExplorerMortalWorldCommandResultBuilder`, `ExplorerShiningAbodeCommandResultBuilder`, `ExplorerModeCommandTests`, `ExplorerModeSourceGuardTests`, and #1085 alignment evidence.
- [X] T005 Hermes records the exact baseline command and counts before production changes.

## Phase 2: Test-First Coverage

- [X] T006 Add a focused failing console command/result or source-guard test proving selected faction detail exposes section actions/menu choices beyond `Показать изображение` for rich faction data.
- [X] T007 Add focused failing tests for representative resource/economics, chronicles, ranks/hierarchy, and projects/territory/strategy section detail output with Russian player-facing labels.
- [X] T008 Add a focused failing test/source guard proving hidden/GM-only/raw/internal faction data is not exposed in default player-facing section output.
- [X] T009 Add or update overview-preservation/alignment tests proving `/factions` overview and selected faction summary remain available and #1085 column alignment usage is preserved.
- [X] T010 Run the new focused tests before production implementation and record the expected RED failures in the Evidence Log.

## Phase 3: Implementation

- [X] T011 Implement selected faction section action/menu construction from available canonical faction data while preserving the existing image action.
- [X] T012 Implement read-only section detail renderers for resources/economics, chronicles, ranks/hierarchy, projects/operations, strategic state, territory/influence, or ledger data where available.
- [X] T013 Add useful Russian empty states for missing/sparse section files or faction-specific empty sections.
- [X] T014 Preserve existing faction overview and selected summary rendering, including `ConsoleLayout` / shared metric-table alignment expectations from #1085.
- [X] T015 Ensure default output escapes dynamic text and avoids raw JSON, file paths, API/DTO/endpoint/debug wording, hidden factions, hidden chronicles, raw `strategicMemory`, and raw `resourceLedger` content.
- [X] T016 Keep the branch read-only: do not add mutations, prompt-session writes, pending files, validation/schema changes, or new GM-authored state contracts.
- [X] T017 If Shining faction-specific sections are intentionally deferred, document the exact follow-up issue/comment before closure rather than silently broadening or dropping acceptance criteria.

## Phase 4: Verification and Review Prep

- [X] T018 Run focused #1086 tests and record exact pass/fail/skip counts.
- [X] T019 Run the broader relevant console/source-guard/browser-service slice and record exact pass/fail/skip counts.
- [X] T020 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- [X] T021 Run `SPECIFY_FEATURE_DIRECTORY=specs/1086-console-faction-drilldowns powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1086-console-faction-drilldowns`.
- [X] T022 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [X] T023 Produce Console Client screenshot/terminal/plain-text capture evidence showing the new selected faction section menu/actions; save under `TestResults/console-faction-drilldowns-*` and copy/preserve critical artifacts under the owning Codex run directory.
- [X] T024 Commit the implementation with `[skip ci]` in the commit message after tests and task evidence are updated.

## Phase 5: Hermes-Owned Review and Closure

- [X] T025 Hermes launches detached independent review before PR/merge and records run/verdict.
- [X] T026 Hermes resolves Critical/Important review findings, reruns fresh gates, and obtains clean review/re-review.
- [X] T027 Hermes creates PR with `Closes #1086`, local-gated verification evidence, Spec Kit links, and any required follow-up references.
- [ ] T028 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1086 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up the worktree/branch, and reports the closure.

## Notes

- #1086 is intentionally limited to read-only Console Client faction detail drill-down menu/actions. Do not re-open #1085 alignment work unless a regression is discovered by tests.
- T025-T028 are Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.
- If any acceptance criterion requires a new runtime/GM-authored schema contract, stop and document a follow-up rather than silently broadening #1086.

## Evidence Log

- Launch setup: Hermes created ASCII worktree `E:/Games/worktrees/boe-1086-console-faction-drilldowns` on branch `work/1086-console-faction-drilldowns` from `origin/main` at `b1b064ae40898c3cdd368b82905b513ecc9032ff` and created this Spec Kit feature directory for issue #1086.
- Issue ownership: Hermes added `status: in-progress` and `codex-agent in-progress` to #1086 before launching Codex.
- Spec Kit discoverability: plain prerequisite check returned the previous `specs/1109-world-news-overview-summaries` because `.specify/feature.json` on main still points at that feature. Hermes verified the intended feature with `SPECIFY_FEATURE_DIRECTORY=specs/1086-console-faction-drilldowns powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`, which returned `FEATURE_DIR` `E:\\Games\\worktrees\\boe-1086-console-faction-drilldowns\\specs\\1086-console-faction-drilldowns` and `AVAILABLE_DOCS` `["contracts/","tasks.md"]`.
- Baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~ExplorerWebCommandServiceTests" --logger "console;verbosity=minimal"` passed 711, failed 0, skipped 0, total 711.
- Codex investigation: inspected current selected faction detail rendering and nearby tests/source guards in `ExplorerMode.FactionsAndWorldNews.cs`, `ExplorerMortalWorldCommandResultBuilder.cs`, `ExplorerShiningAbodeCommandResultBuilder.cs`, `ExplorerModeCommandTests*.cs`, and `ExplorerModeSourceGuardTests.cs`. The existing selected faction summary was useful, but the prompt actions effectively only surfaced image/return flow, and hidden faction filtering was missing from the selected faction choice list.
- RED TDD run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FactionDrilldown" --logger "console;verbosity=minimal"` failed 5, passed 0, skipped 0, total 5 before production changes. Expected failures: missing `ShowFactionDetailSectionMenu`, missing rich section choices such as `Ресурсы и экономика` / `Стратегия и память`, hidden faction still selectable, sparse sections missing useful empty states, and source guard missing player-facing/read-only section helpers.
- Implementation summary: added a read-only selected-faction section hub in the Mortal World Console Client with Russian menu actions for resources/economics, chronicles, ranks/hierarchy, projects/operations, strategy/memory, and territory/influence. Detail views render player-visible sidecar/canonical data, escape dynamic text through the existing safe console APIs, filter hidden factions/hidden knowledge entries, avoid raw JSON/internal labels, and preserve the existing selected faction summary and image action. No Shining write flow, pending file, validation/schema, normalizer, GM prompt, or afterlife contract changes were made.
- Debugging note: the first GREEN attempt produced compiler error `CS0136` in `ExplorerMode.FactionsAndWorldNews.cs` because a nested local variable named `line` conflicted in the same scope. Root cause was a local-name collision in the new knowledge-entry collector; renaming the inner variable to `entryLine` fixed the compile failure.
- Local review fix: a final read-through found that core `scribeChronicle` object entries needed the same visibility predicate as sidecar chronicle entries. Added a fixture case for hidden core chronicle text (`Тайный долговой список`) and filtered object entries before building chronicle lines. Narrow rerun `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FactionDrilldown" --logger "console;verbosity=minimal"` passed 5, failed 0, skipped 0, total 5.
- Focused GREEN run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FactionDetail|FactionDrilldown|FactionSection|ExplorerModeSourceGuardTests" --logger "console;verbosity=minimal"` passed 51, failed 0, skipped 0, total 51.
- Broader GREEN slice after rebase onto latest `origin/main`: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~ExplorerWebCommandServiceTests" --logger "console;verbosity=minimal"` passed 718, failed 0, skipped 0, total 718.
- Build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- Spec Kit prerequisite: `SPECIFY_FEATURE_DIRECTORY=specs/1086-console-faction-drilldowns powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR` `E:\\Games\\worktrees\\boe-1086-console-faction-drilldowns\\specs\\1086-console-faction-drilldowns` and `AVAILABLE_DOCS` `["contracts/","tasks.md"]`.
- Diff hygiene: `git diff --check` exited 0 before commit with only CRLF normalization warnings for touched C# files. After commit, `git diff --check origin/main...HEAD` exited 0 with no output.
- Added-line static/security scan over changed non-plan code found no secrets, shell execution, eval, or deserialization findings. Expected findings were limited to test negative assertions/test fixture keys (`resourceLedger`, `strategicMemory`, `debug`, `Полный JSON`) and production internal property reads/helper names (`resourceLedger`, `strategicMemory`, `secret`) used to render safe player-facing labels; these are not emitted as default player-facing raw output.
- Console evidence: deterministic terminal/plain-text capture saved at `TestResults/console-faction-drilldowns-20260618-234520/faction-detail-menu.txt` and copied to `E:/Games/codex-runs/20260618-232336-boe-1086-console-faction-drilldowns/visual-evidence/console-faction-drilldowns-20260618-234520-faction-detail-menu.txt`. The capture shows the selected faction detail for `Серебряная гильдия`, all six section menu choices, and no hidden faction in the selection list.
- Shining scope: #1086 implementation intentionally stayed focused on Mortal World selected faction details because broad Shining parity risks afterlife contract work. No Shining-specific contract was changed; Hermes should decide whether a separate follow-up issue is needed for Shining faction hubs during final acceptance.
- Commit: Codex created the implementation commit with message `Implement console faction drilldowns [skip ci]` after verification and task evidence updates; Hermes rebased it onto latest `origin/main` and amended review/evidence reconciliation before PR.
- Independent review: Hermes delegate_task review completed after the latest rebase and fresh gates with verdict `APPROVED`, no Critical or Important issues. Minor notes were limited to task evidence count freshness, Shining parity scope wording, and non-blocking section-menu count polish; the task evidence count was corrected to the post-rebase 718-pass broader slice before PR.
- PR creation: Hermes created PR #1115 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1115`) with `Closes #1086`, local-gated verification evidence, Spec Kit links, console evidence, independent review summary, and `GitHub Actions: not used / not required`. `gh pr view` reported `mergeStateStatus` `CLEAN` and closing issue `[1086]` before merge.
