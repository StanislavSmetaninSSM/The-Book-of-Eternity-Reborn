# Tasks: Browser Ink Feather Fate Reveal and Rewrite

**Input**: GitHub issue [#815](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/815), umbrella [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817), [spec.md](spec.md), [plan.md](plan.md)
**Prerequisites**: Existing console reveal/rewrite fate flow, `PendingTurnStateService`, browser prompt/write patterns from #812/#813/#814, constitution, repository `AGENTS.md`

## Phase 1: Setup & Spec Kit

- [x] T001 Confirm source branch `codex/815-ink-feathers-fate`, clean tracked worktree, and open GitHub issue #815.
- [x] T002 Inspect constitution, `AGENTS.md`, issue #815/#817 body, existing console reveal/rewrite fate source, pending-turn state service, and browser local-turn/prompt patterns.
- [x] T003 Create #815 Spec Kit artifacts linked to issues #815/#817 and scoped to existing local Ink Feather/pending dice state reuse.
- [x] T004 Run focused baseline before implementation and record exact counts.

## Phase 2: RED Tests First

- [x] T005 Add browser parity test for opening reveal fate with current cost, remaining balance, and confirmation prompt.
- [x] T006 Add reveal submit test proving one Ink Feather spend and a locked pending dice/gacha state.
- [x] T007 Add browser parity test for opening rewrite fate only when current pending fate is locked.
- [x] T008 Add rewrite submit test proving one Ink Feather spend, replacement locked pending state, and old/new dice/gacha result copy.
- [x] T009 Add guard coverage for insufficient balance, missing/malformed `soul_state.json`, missing/malformed/unlocked pending fate state, cancellation/unconfirmed submit, local write/GM-turn blockers, and stale prompt submissions.
- [x] T010 Add/update command coverage, command menu/help, API fixture, and source guard tests proving #815 actions are browser-supported and player-facing.
- [x] T011 Run focused RED command and record expected failing tests before production implementation.

## Phase 3: Minimal Implementation

- [x] T012 Add #815 reveal/rewrite command descriptor(s), aliases, mutation mode, browser handler kind, and/or `/feathers` player-facing action metadata.
- [x] T013 Add prompt-session local UI lock coverage for #815 mutating Ink Feather fate command(s).
- [x] T014 Implement browser prompt builders for reveal/rewrite cost summary, availability, and confirmation.
- [x] T015 Extract or reuse C# helpers for reading/deducting Ink Feathers, pending fate state, and dice/gacha result summaries.
- [x] T016 Implement browser write handlers that re-read current state, recompute cost, re-check confirmation/lock/balance, serialize local writes, and update only existing `soul_state.json` / `pending_dice_state.json`.
- [x] T017 Update browser command coverage/help/menu/game-screen/API fixtures so #815 reveal/rewrite fate are treated as covered guided forms while #816/#817 remain open.
- [x] T018 Keep runtime contract shape unchanged; if this becomes impossible, update contract matrix/examples/manifest/docs tests before continuing.

## Phase 4: GREEN & Verification

- [x] T019 Run focused RED/GREEN test filter and record exact result counts.
- [x] T020 Run final focused browser/API/afterlife parity sweep and record exact result counts.
- [x] T021 Run documentation-sensitive tests if any runtime contract/doc-impacting surface changes, or record why not required.
- [x] T022 Run C# build commands for touched projects and record results.
- [x] T023 Run `git diff --check origin/main...HEAD` and added-line security scan excluding Spec Kit docs; record results.
- [x] T024 Run frontend verification if frontend files or fixtures change; otherwise record why it was not run.
- [x] T025 Update this task list with completed task statuses and verification evidence.

## Phase 5: Commit & Handoff

- [x] T026 Inspect final `git diff --stat origin/main...HEAD` and `git status --short` for accidental run artifacts/caches.
- [x] T027 Create a local focused commit with `[skip ci]`.
- [x] T028 Final Codex report includes summary, files changed, Spec Kit artifacts, exact verification results/counts, commit SHA, remaining risks/blockers, and note that PR/merge/issue closure remain with Hermes.

## Verification Evidence

- Spec Kit setup: `specs/815-browser-ink-feathers-fate/spec.md`, `plan.md`, and `tasks.md` created by Hermes on 2026-06-07 from issue #815/#817 before implementation.
- Spec Kit CLI check before artifact creation: `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH; specify version`; result: Specify CLI 0.9.3.
- Spec Kit prerequisite check after artifact creation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`; result: `FEATURE_DIR=E:\\Games\\worktrees\\boe-815-ink-feathers\\specs\\815-browser-ink-feathers-fate`, `AVAILABLE_DOCS=["tasks.md"]`.
- Baseline command before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInkFeatherFateParityTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`.
- Baseline result before implementation: passed, 0 failed / 430 passed / 0 skipped / 430 total. Restore/build ran first in the fresh worktree and produced test binaries normally.
- Focused RED command to run after adding failing tests: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInkFeatherFateParityTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`.
- Initial RED harness compile check: same focused command failed before production edits because the new test file was missing `BookOfEternityClient.UI` and returned `IReadOnlyDictionary<string, JsonNode?>` where the submit DTO requires `Dictionary<string, JsonNode?>`. The test harness was corrected before production code changes.
- Behavioral RED result before production implementation: same focused command failed as expected, 12 failed / 430 passed / 0 skipped / 442 total. Failures covered missing `/feathers` actions, reveal/rewrite prompt routes, write handlers, local-turn pending behavior, and #815 coverage metadata.
- Focused GREEN without API fixtures after implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInkFeatherFateParityTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`; result: passed, 0 failed / 418 passed / 0 skipped / 418 total.
- Full focused GREEN after fixture refresh: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInkFeatherFateParityTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`; result: passed, 0 failed / 442 passed / 0 skipped / 442 total.
- Documentation-sensitive sweep if contract/doc-impacting surfaces change: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- Documentation-sensitive sweep result: passed, 0 failed / 99 passed / 0 skipped / 99 total. No GM-authored contract shape changed.
- Production build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`; result: succeeded, 0 warnings / 0 errors.
- Test build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`; result: succeeded, 0 warnings / 0 errors.
- Frontend dependency setup: `npm ci --prefix BookOfEternityClient.WebFrontend`; result: added 54 packages, audited 55 packages, 0 vulnerabilities.
- Frontend verification: `npm run verify --prefix BookOfEternityClient.WebFrontend`; result: typecheck passed, player-facing JS checks passed, Vitest 2 files / 29 tests passed, production build succeeded.
- Diff hygiene: `git diff --check origin/main...HEAD`; result: no output, exit 0.
- Added-line security scan excluding Spec Kit docs: PowerShell equivalent of the requested added-line scan; result: `NO_MATCHES`.
- Final staged diff/status inspection before commit: `git diff --cached --stat` showed 15 intended files and `git status --short` showed only staged source/test/fixture/spec changes, with no run artifacts, caches, `node_modules`, `bin`, `obj`, or frontend `dist`.
- Local commit created with `[skip ci]`; final SHA is recorded in the Codex handoff message. Hermes retains PR creation, merge, and issue closure.

## Notes

- Sibling issue #816 and umbrella #817 closure are out of scope.
- Reveal/rewrite fate are existing local client-owned Ink Feather behavior. Contract docs/examples are intentionally unchanged unless implementation changes runtime state shape or GM-facing guidance.
- PR creation, merge, issue closure, cron edit, or task lifecycle closure is not part of the Codex implementation run.
