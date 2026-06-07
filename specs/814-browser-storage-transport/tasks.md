# Tasks: Browser Storage and Transport Item Moves

**Input**: GitHub issue [#814](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/814), umbrella [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817), [spec.md](spec.md), [plan.md](plan.md)
**Prerequisites**: Existing console storage/transport flow, browser #806/#812/#813 local prompt/write patterns, constitution, repository `AGENTS.md`

## Phase 1: Setup & Spec Kit

- [x] T001 Confirm source branch `task/814-browser-storage-transport`, clean tracked worktree, and open GitHub issue #814.
- [x] T002 Inspect constitution, `AGENTS.md`, issue #814/#817 body, existing console storage/transport source, and browser local-turn/prompt patterns.
- [x] T003 Create #814 Spec Kit artifacts linked to issues #814/#817 and scoped to existing local storage/transport state reuse.
- [x] T004 Run focused baseline before implementation and record exact counts.

## Phase 2: RED Tests First

- [x] T005 Add browser parity test for opening the location-storage move prompt with player-facing storage, direction, and item choices.
- [x] T006 Add browser parity test for storage deposit moving exactly one selected JSON item from player inventory to `current_location.json.locationStorages[].contents`.
- [x] T007 Add browser parity test for storage retrieve moving exactly one selected JSON item from storage contents to player inventory.
- [x] T008 Add browser parity test for opening the transport move prompt with player-facing vehicle, direction, and item choices.
- [x] T009 Add browser parity test for transport deposit moving exactly one selected JSON item from player inventory to selected vehicle `inventory`.
- [x] T010 Add browser parity test for transport retrieve moving exactly one selected JSON item from selected vehicle `inventory` to player inventory.
- [x] T011 Add command-open and stale prompt-submit guard coverage for missing/malformed inventory, missing storage, missing vehicle, missing selected item, duplicate display names, and local write/GM-turn blockers.
- [x] T012 Add/update command coverage, command menu/help, API fixture, and source guard tests proving #814 actions are browser-supported and player-facing.
- [x] T013 Run focused RED command and record expected failing tests before production implementation.

## Phase 3: Minimal Implementation

- [x] T014 Add #814 storage/transport command descriptor(s), aliases, mutation mode, browser handler kind, and player-facing metadata.
- [x] T015 Add prompt-session local UI lock coverage for #814 mutating storage/transport command(s).
- [x] T016 Implement browser prompt builders for storage vs transport, direction, target storage/vehicle, selected item, and final confirmation.
- [x] T017 Extract or reuse C# item-move helpers so console and browser share the same inventory/storage/vehicle mutation semantics where practical.
- [x] T018 Implement browser write handlers that re-read current state, re-check selected target/item, serialize local writes, and update only existing inventory/current-location/vehicle files.
- [x] T019 Update browser command coverage/help/menu/game-screen/API fixtures so #814 storage/transport moves are treated as covered guided forms while #817 remains open.
- [x] T020 Keep runtime contract shape unchanged; if this becomes impossible, update contract matrix/examples/manifest/docs tests before continuing.

## Phase 4: GREEN & Verification

- [x] T021 Run focused RED/GREEN test filter and record exact result counts.
- [x] T022 Run final focused storage/transport/browser/API parity sweep and record exact result counts.
- [x] T023 Run documentation-sensitive tests if any runtime contract/doc-impacting surface changes, or record why not required.
- [x] T024 Run C# build commands for touched projects and record results.
- [x] T025 Run `git diff --check origin/main...HEAD` and added-line security scan excluding Spec Kit docs; record results.
- [x] T026 Run frontend verification if frontend files or generated frontend-facing artifacts change; otherwise record why it was not run.
- [x] T027 Update this task list with completed task statuses and verification evidence.

## Phase 5: Commit & Handoff

- [x] T028 Inspect final `git diff --stat origin/main...HEAD` and `git status --short` for accidental run artifacts/caches.
- [x] T029 Create a local focused commit with `[skip ci]`.
- [x] T030 Final Codex report includes summary, files changed, Spec Kit artifacts, exact verification results/counts, commit SHA, remaining risks/blockers, and note that PR/merge/issue closure remain with Hermes.

## Verification Evidence

- Spec Kit setup: `specs/814-browser-storage-transport/spec.md`, `plan.md`, and `tasks.md` created by Hermes on 2026-06-07 from issue #814/#817 before implementation.
- Spec Kit CLI check before artifact creation: `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH; specify version && specify integration list`; result: Specify CLI 0.9.3, Codex integration installed/default.
- Spec Kit prerequisite check after artifact creation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`; result: `FEATURE_DIR=E:\Games\worktrees\boe-814-storage-transport\specs\814-browser-storage-transport`, `AVAILABLE_DOCS=["tasks.md"]`.
- Baseline command before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`.
- Baseline result before implementation: passed, 0 failed / 442 passed / 0 skipped / 442 total. Restore/build ran first in the fresh worktree and produced test binaries normally.
- Focused RED command to run after adding failing tests: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransportParityTests|BrowserInventoryManagement|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`.
- RED run after adding `BrowserStorageTransportParityTests`: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransportParityTests|BrowserInventoryManagement|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`; result: failed as expected, 11 failed / 36 passed / 0 skipped / 47 total. Expected failures show `/storage_move` and `/vehicle_move` are not registered yet, prompt option lists are absent, and #814 coverage rows are missing before production implementation.
- Independent review RED command on 2026-06-07 before review-fix production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransportParityTests" --logger "console;verbosity=minimal"`; result: failed as expected, 6 failed / 13 passed / 0 skipped / 19 total. Expected failures showed `/storage_move` and `/vehicle_move` opened prompts in `Chaos Sea`, stale submit after realm switch completed and mutated state, and name-only accessible storage after an inaccessible predecessor resolved as missing.
- Focused GREEN command after implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransportParityTests|BrowserInventoryManagement|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`; initial implementation result: passed, 0 failed / 47 passed / 0 skipped / 47 total.
- Independent review focused GREEN command on 2026-06-07: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransportParityTests" --logger "console;verbosity=minimal"`; result: passed, 0 failed / 19 passed / 0 skipped / 19 total.
- Final focused sweep command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransportParityTests|BrowserInventoryManagement|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`.
- Final focused sweep result: initial implementation passed, 0 failed / 453 passed / 0 skipped / 453 total. Independent review-fix result passed, 0 failed / 461 passed / 0 skipped / 461 total.
- Documentation-sensitive sweep command if contract/doc-impacting surfaces changed: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- Documentation-sensitive sweep result: passed, 0 failed / 99 passed / 0 skipped / 99 total. No afterlife pending/control/action field, GM-authored contract shape, or docs matrix/example change was introduced.
- Build commands: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`.
- Build results: both builds passed with 0 warnings / 0 errors.
- Frontend verification command if frontend files or fixtures change: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- Frontend verification: initial run failed before typecheck because `node_modules` was absent and `tsc` was not found. After `npm ci --prefix BookOfEternityClient.WebFrontend` from the checked-in lockfile, `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: TypeScript app/node typechecks passed, player-facing fixture scripts passed, Vitest passed 29/29, and Vite production build succeeded.
- Whitespace check: `git diff --check origin/main...HEAD`; result: passed with no output.
- Added-line static security scan excluding `specs/814-browser-storage-transport/*`: `git diff --unified=0 origin/main...HEAD -- . ':(exclude)specs/814-browser-storage-transport/*' | rg -n "^\+.*(secret|token|password|api[_-]?key|authorization|bearer|private key|BEGIN RSA|BEGIN OPENSSH|credential)" -i`; result: no matches.
- Staged final checks after adding new files: `git diff --cached --check` passed with no output; `git diff --cached --unified=0 -- . ':(exclude)specs/814-browser-storage-transport/*' | rg -n "^\+.*(secret|token|password|api[_-]?key|authorization|bearer|private key|BEGIN RSA|BEGIN OPENSSH|credential)" -i` returned no matches.
- Final pre-commit status inspection: `git status --short --branch` showed only tracked source/fixture changes plus intentional untracked `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs`, `BookOfEternityClient/Services/StorageTransportMoveService.cs`, and `specs/814-browser-storage-transport/`. Ignored `dist/`, `node_modules/`, and temp fixture-generator directories created during verification were removed before staging.

## Notes

- Sibling issues #815/#816 and umbrella #817 closure are out of scope.
- Storage/transport moves are existing local file-backed console behavior. Contract docs/examples are intentionally unchanged unless implementation changes runtime state shape or GM-facing guidance.
- PR creation, merge, issue closure, cron edit, or task lifecycle closure is not part of the Codex implementation run.
