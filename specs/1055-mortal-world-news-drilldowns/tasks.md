---
description: "Task list for #1055 mortal world-news read-only detail drill-downs"
---

# Tasks: Mortal World-News Read-Only Detail Drill-Downs

**Input**: `specs/1055-mortal-world-news-drilldowns/spec.md`, `plan.md`, `contracts/mortal-world-news-drilldowns.md`, and GitHub issue #1055.

**Prerequisites**: plan.md, spec.md, requirements checklist, #948 audit artifact, #1054 implementation patterns, AGENTS.md, `.specify/memory/constitution.md`.

**Tests**: Behavior changes require test-first work. Add or update focused tests before production code, prove at least one RED failure, then make them pass.

## Phase 1: Setup and Investigation

- [x] T001 Confirm `git status --short --branch`, current issue #1055, active branch `1055-mortal-world-news-drilldowns`, and active feature path `specs/1055-mortal-world-news-drilldowns`.
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #1055 body, `docs/audits/mortal-readonly-drilldown-audit.md`, #1054 combat drill-down patterns, `spec.md`, `plan.md`, and this task file.
- [x] T003 Inspect current `/world_news` / `/новости_мира` handling in `ExplorerMortalWorldCommandResultBuilder`, `ExplorerMode` console handlers, command catalog/registry, and existing tests around `ExplorerWebCommandServiceTests`, `ExplorerModeCommandTests`, and `MortalReadOnlyDrilldownAuditTests`.
- [x] T004 Record the exact focused baseline test command and counts before production changes.

## Phase 2: Test-First Coverage

- [x] T005 Add a focused browser/shared command-result test that seeds `game_state/world/world_events.json`, runs `/новости_мира` or `/world_news`, and fails until a world event can be inspected through player-facing action/detail content without relying on raw JSON.
- [x] T006 Add a focused browser/shared command-result test that seeds the command's real major non-event world-news subsection state, runs `/новости_мира` or `/world_news`, and fails until one representative subsection item can be inspected through player-facing action/detail content.
- [x] T007 Add a focused browser/shared command-result test that seeds `game_state/world/progression.json`, runs `/новости_мира` or `/world_news`, and fails until one progression entry can be inspected through player-facing list/detail content.
- [x] T008 Add or update console/source-guard tests proving console and browser expose semantically equivalent event, subsection, and progression detail affordances, or explicitly record a narrower follow-up if parity cannot be completed in this PR.
- [x] T009 Run the new focused tests before implementation and record the expected RED failures in the Codex final report and, after GREEN, in this tasks file.

## Phase 3: Implementation

- [x] T010 Replace or extend the current generic `/world_news` bundle path in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` with focused Mortal world-news overview plus event, subsection, and progression list/detail player-facing blocks or actions.
- [x] T011 Preserve existing overview counts/state and graceful missing-file behavior for world-news files.
- [x] T012 Add player-facing Russian/in-world formatting helpers for event titles, timing, locations, actors, status, descriptions, consequences, flags/threats/activity/project notes, progression status, and related tags; avoid raw canonical enum/key leakage in default blocks.
- [x] T013 Ensure dynamic GM-authored text used in console/browser blocks remains escaped/sanitized according to existing project patterns.
- [x] T014 Update the console `/новости_мира` path to expose semantically equivalent event, subsection, and progression inspection affordances, or create/link a follow-up issue and document the exact remaining console gap before merge.
- [x] T015 Update audit/spec documentation only as needed to record the #1055 implementation and any follow-up split; do not change GM-facing prompts/examples unless runtime contracts changed.

## Phase 4: Verification and Review Prep

- [x] T016 Run focused tests covering event, subsection, and progression detail output and existing `/новости_мира` overview behavior; record exact pass/fail/skip counts.
- [x] T017 Run the broader mortal command-result/console/browser parity slice identified during implementation; record exact pass/fail/skip counts.
- [x] T018 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changed.
- [x] T019 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/1055-mortal-world-news-drilldowns`.
- [x] T020 Run `git diff --check origin/main...HEAD` and an added-line static/security scan over changed non-plan code.
- [x] T021 Commit the implementation with `[skip ci]` in the commit message after tests and task evidence are updated.

## Phase 5: Hermes-Owned Review and Closure

- [x] T022 Hermes launched detached independent Codex review before PR/merge: `E:/Games/codex-runs/20260615-223137-review-boe-1055-mortal-world-news-drilldowns/final.md` => `APPROVED`, blocking findings none.
- [x] T023 No Critical/Important review findings required code changes. Non-blocking review note about stale “likely shape” contract wording was reconciled to the implemented `/флаг` / `/flag` detail syntax before PR.
- [x] T024 Hermes created PR #1060 with `Closes #1055`, local-gated verification evidence, Spec Kit links, and safe non-closing references for #1056/#1057/#949: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1060
- [ ] T025 Hermes squash-merges after local gates/review, posts an issue evidence comment, verifies #1055 is `CLOSED / COMPLETED`, moves lifecycle label to `status: verified` when available, cleans up the worktree/branch, and reports the closure.

## Notes

- #1055 is intentionally limited to Mortal World `/world_news` / `/новости_мира` detail drill-downs. Do not implement #1056, #1057, #949, or afterlife world-state surfaces as part of this branch.
- T022-T025 remain Hermes-owned lifecycle steps. Codex may leave them unchecked unless Hermes performs them before the implementation commit is finalized.
- If any acceptance criterion requires a new runtime/GM-authored schema contract, stop and document a follow-up rather than silently broadening #1055.

## Evidence Log

- Setup/context evidence: `git status --short --branch` => `## 1055-mortal-world-news-drilldowns...origin/main` with untracked `.hermes/` and `specs/1055-mortal-world-news-drilldowns/`; linked worktree branch `1055-mortal-world-news-drilldowns`; Spec Kit prerequisite check resolved `FEATURE_DIR` to `specs/1055-mortal-world-news-drilldowns`; `requirements.md` checklist `15/15` complete.
- Baseline before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` => passed 457, failed 0, skipped 0, total 457.
- Expected RED after adding focused tests: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~WorldNewsOverview|FullyQualifiedName~WorldNewsEventDetail|FullyQualifiedName~WorldNewsFlagDetail|FullyQualifiedName~WorldNewsProgressionDetail|FullyQualifiedName~ConsoleExposesSharedEventFlagAndProgressionDrilldowns|FullyQualifiedName~ConsoleWorldNewsSource" --logger "console;verbosity=minimal"` => failed 6, passed 0, skipped 0, total 6. Failures showed the generic browser bundle ignored world-news detail arguments, exposed raw JSON blocks as the detail path, lacked event/flag/progression actions, and the console handler did not route world-news through the shared command-result renderer.
- Implementation evidence: `ExplorerMortalWorldCommandResultBuilder` now delegates `/world_news` / `/новости_мира` to `ExplorerMortalWorldNewsCommandResultBuilder`; `ExplorerCommandCatalog` preserves world-news detail arguments; `ExplorerMode.FactionsAndWorldNews.ShowWorldNews` renders the shared DTO surface; `docs/audits/mortal-readonly-drilldown-audit.md` records #1055 as implemented. No GM-authored schema, validation, prompt, example, or afterlife contract changed.
- GREEN focused detail tests: same new-test filter as RED => passed 6, failed 0, skipped 0, total 6.
- Product-focused world-news/mortal slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "WorldNews|MortalReadOnlyDrilldownAudit|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"` => passed 444, failed 0, skipped 0, total 444.
- Broader relevant slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"` => passed 463, failed 0, skipped 0, total 463.
- Build verification: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded, warnings 0, errors 0; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded, warnings 0, errors 0.
- Spec Kit check: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` => `FEATURE_DIR` resolved to `specs/1055-mortal-world-news-drilldowns`, `AVAILABLE_DOCS` included `contracts/` and `tasks.md`.
- Diff hygiene: `git diff --check` and `git diff --cached --check` => no whitespace errors.
- Added-line static/security scan: `git diff --cached --unified=0 -- BookOfEternityClient BookOfEternityClient.Tests` filtered for secrets/shell/eval/deserialization/SQL patterns. Broad matches reviewed: `commandToken` local command-routing variables (benign, not credentials), `secret` as a translated world-news visibility label (benign player-facing copy), and `JsonNode.Parse(raw)` for existing local JSON state reads (expected structured parser use). No credential, shell execution, eval, or SQL-construction findings.
- Commit evidence: implementation commit prepared after the above gates with message `Implement world news drilldowns [skip ci]`; final SHA is recorded in the Codex/Hermes completion report.
