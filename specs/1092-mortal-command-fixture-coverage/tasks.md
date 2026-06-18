# Tasks: Mortal Command Fixture Coverage

**Input**: Design documents from `/specs/1092-mortal-command-fixture-coverage/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/mortal-command-fixture-matrix.md

**Source issues**: #1092 original Mortal command fixture coverage; #1095 reusable Mortal World command-display save continuation; #1096 reusable Chaos Sea afterlife command-display save continuation; #1097 reusable Shining Abode afterlife command-display save continuation.

**Tests**: This task is fixture/audit-heavy. Add automated coverage when command coverage can be tested from tracked files; otherwise document manual verification evidence in the issue.

## Phase 1: Setup (Shared Infrastructure)

- [x] T001 Confirm source GitHub issue #1092, `git status --short`, and active Spec Kit feature path `specs/1092-mortal-command-fixture-coverage`
- [x] T002 Read `AGENTS.md`, constitution, active spec/plan, source issue acceptance criteria, and command catalog/builders
- [x] T003 [P] Identify focused verification commands for command coverage and fixture validation

---

## Phase 2: Foundational (Blocking Prerequisites)

- [x] T004 Build the authoritative Mortal World command inventory from `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`
- [x] T005 Map each display command to state files read by `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`
- [x] T006 Map each local-turn Mortal World command to prompt-option state files read by `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs`
- [x] T007 Compare the local ignored `BookOfEternityClient/game_session` fixture with the matrix in `specs/1092-mortal-command-fixture-coverage/contracts/mortal-command-fixture-matrix.md`

---

## Phase 3: User Story 1 - Preview Every Mortal Display Command (Priority: P1)

**Goal**: Every Mortal World read-only command has representative display data.

**Independent Test**: Execute the read-only smoke commands from `quickstart.md` and confirm non-empty player-facing output.

- [x] T008 [US1] Repair inventory/books/effects/status fixture data in `BookOfEternityClient/game_session`
- [x] T009 [US1] Repair map/location/weather/world-news/rival/guardian-correction fixture data in `BookOfEternityClient/game_session`
- [x] T010 [US1] Repair NPC/quest/faction/skill/combat/storage/interaction fixture data in `BookOfEternityClient/game_session`
- [x] T011 [US1] Update `specs/1092-mortal-command-fixture-coverage/contracts/mortal-command-fixture-matrix.md` with covered/needs-follow-up statuses

---

## Phase 4: User Story 2 - Exercise Mortal Action Forms (Priority: P2)

**Goal**: Local-turn Mortal World command forms have useful state/options without auto-submitting GM turns.

**Independent Test**: Execute local-turn smoke commands from `quickstart.md` and confirm prompts/actions are useful and non-empty where state should provide options.

- [x] T012 [US2] Ensure inventory action fixture data supports equip, unequip, drop, split, merge, storage move, and vehicle move previews
- [x] T013 [US2] Ensure NPC/faction/feather/craft fixture data supports talk, trade, directives, fate, stat distribution, and craft previews
- [x] T014 [US2] Record any local-turn command that needs a code/UI follow-up instead of fixture-only repair

---

## Phase 5: User Story 3 - Keep Coverage Visible (Priority: P3)

**Goal**: Future agents can see and repeat fixture coverage checks.

**Independent Test**: Re-run inventory and smoke command lists from `quickstart.md`.

- [x] T015 [US3] Add or update a lightweight command coverage helper/test if feasible from tracked files
- [x] T016 [US3] Update `quickstart.md` with final verification commands and any local-session caveats
- [x] T017 [US3] Comment on GitHub issue #1092 with coverage evidence and remaining follow-ups

---

## Phase 6: #1095 Reusable Mortal World Save Continuation

**Goal**: Package the rich #1092 Mortal World command-display fixture as a tracked reusable save/load-compatible artifact that survives clean checkouts and can be validated/rendered repeatedly.

**Independent Test**: Load the named save into a disposable session root, run validation with zero blocking issues, and run console/browser command-display smoke checks for the #1092-covered command set.

- [x] T021 Confirm source issue #1095, create isolated branch `work/1095-mortal-command-save`, and mark the GitHub issue in progress.
- [x] T022 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"` => 666 passed / 0 failed / 0 skipped / 666 total.
- [x] T023 Update active Spec Kit artifacts to reference #1095 and reusable save packaging scope.
- [x] T024 Inspect save/load code and choose the normal tracked save/package path for the reusable Mortal fixture: `SaveLoadService` uses `saves/manual_saves/*.zip`; tracked source path chosen as `FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture.zip`.
- [x] T025 Add RED discoverability/loadability/validation tests for the named reusable save: `MortalCommandDisplaySaveTests.NamedMortalCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable` failed RED on missing archive, then passed after packaging.
- [x] T026 Add RED or mutation-backed console/browser command-display smoke coverage for the #1092-covered command set against the loaded save: `MortalCommandDisplaySaveTests.LoadedMortalCommandDisplaySave_RendersCoveredCommandInBrowserAndConsole` covers 77 Mortal aliases + 14 universal Mortal preview commands with browser result and console renderer checks.
- [x] T027 Create/copy the reusable named Mortal World display-test save and metadata using the discovered save/load-compatible layout: added `mortal_world_command_display_fixture.zip` and `mortal_world_command_display_fixture_metadata.json` under `FileSystemExample/game_session/saves/manual_saves/`.
- [x] T028 Document save location, loading steps, command coverage matrix, representative invocations, expected visible data, and verification commands in `quickstart.md`, `contracts/mortal-command-fixture-matrix.md`, `data-model.md`, `plan.md`, `spec.md`, and `FileSystemExample/README.md`.
- [x] T029 Run focused and broader validation/Explorer command tests; record exact GREEN counts: focused reusable save test 92 passed / 0 failed / 0 skipped; Hermes baseline suite 666 passed / 0 failed / 0 skipped; broader validation/Explorer suite 1781 passed / 0 failed / 0 skipped.
- [x] T030 Run Spec Kit prerequisite check and confirm the helper resolves `FEATURE_DIR` to `specs/1092-mortal-command-fixture-coverage` for this branch.
- [x] T031 Run build, `git diff --check`, and added-line static scan; record results: client build 0 warnings / 0 errors; test build 0 warnings / 0 errors; `git diff --check` exit 0 with only LF-to-CRLF warnings; static scan found no added-line matches for secrets, shell execution, eval/exec, unsafe deserialization, or SQL string-formatting patterns.
- [x] T032 Reconcile docs/prompts impact and confirm Chaos Sea #1096 / Shining Abode #1097 remain out of scope: no gameplay mechanics, GM-authored contracts, prompts, examples, Chaos Sea fixture, or Shining Abode fixture changed.
- [x] T033 Commit #1095 implementation with `[skip ci]`: included in the final implementation commit `test: add reusable Mortal command display save [skip ci]`.

---

## Phase 7: #1096 Reusable Chaos Sea Save Continuation

**Goal**: Package representative Chaos Sea afterlife command-display state as a tracked reusable save/load-compatible artifact that survives clean checkouts and can be validated/rendered repeatedly without mixing in the dedicated Shining Abode fixture task.

**Independent Test**: Load the named save into a disposable session root, run validation with zero blocking issues, and run console/browser command-display smoke checks for every command available in the Chaos Sea save.

- [x] T034 Confirm source issue #1096, create isolated branch `work/1096-chaos-sea-command-save`, and mark the GitHub issue in progress.
- [x] T035 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~FileSystemExampleAfterlifeStateExamplesTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~Validation" --logger "console;verbosity=minimal"` => 1780 passed / 0 failed / 0 skipped / 1780 total.
- [x] T036 Update active Spec Kit artifacts to reference #1096 and reusable Chaos Sea save packaging scope.
- [x] T037 Inspect save/load code, afterlife display builders, and existing FileSystemExample afterlife state to confirm the tracked save path `FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture.zip` plus metadata sidecar. Implementation note: `SaveLoadService` strips `input/` and `game_state/control/pending_turn_snapshot*`, so the #1096 fixture is an at-rest manual save.
- [x] T038 Add RED discoverability/loadability/validation tests for the named reusable Chaos Sea save, expected to fail before the archive/metadata exist. RED evidence: `ChaosSeaCommandDisplaySaveTests` initially failed on missing `chaos_sea_command_display_fixture.zip`, then on validation/display failures while fixture state was still incomplete.
- [x] T039 Add RED or mutation-backed console/browser command-display smoke coverage for every command available in the loaded Chaos Sea save, including relevant universal afterlife/status commands and detail-capable invocations. Coverage: all `ExplorerCommandGroup.ChaosSea` and `ExplorerCommandGroup.AfterlifeCombatAndEntities` aliases, practical universal afterlife/status commands, and representative detail targets render through browser command service plus console renderer.
- [x] T040 Create or copy the reusable named Chaos Sea display-test save and metadata using the existing save/load-compatible layout: added `chaos_sea_command_display_fixture.zip` and `chaos_sea_command_display_fixture_metadata.json` under `FileSystemExample/game_session/saves/manual_saves/`.
- [x] T041 Fill representative Chaos Sea state so each available command returns useful player-facing data or a clear in-world unavailable reason; include Shining Abode data only as historical/contextual references required by Chaos Sea output. Implementation note: guardian project display is journal-backed, while `/archive_project_fuel` shows a clear unavailable reason because active project tracker authority requires a validated pre-turn baseline not preserved by manual saves.
- [x] T042 Document save location, loading steps, command coverage matrix/checklist, representative invocations, expected visible data, and verification commands in `quickstart.md`, the coverage contract/checklist, `data-model.md`, `plan.md`, `spec.md`, and `FileSystemExample/README.md` as needed. Added `contracts/chaos-sea-command-fixture-checklist.md`.
- [x] T043 Run focused and broader validation/Explorer command tests; record exact GREEN counts in this task list and the PR/issue evidence. GREEN evidence: focused `ChaosSeaCommandDisplaySaveTests` => 96 passed / 0 failed / 0 skipped / 96 total; broader afterlife/fixture/Explorer/Validation gate => 1780 passed / 0 failed / 0 skipped / 1780 total. One parallel rerun attempt hit a compiler file-lock (`CS2012`) because two `dotnet test` processes wrote the same obj assembly concurrently; rerunning the focused suite sequentially passed.
- [x] T044 Run Spec Kit prerequisite check and confirm the helper resolves `FEATURE_DIR` to `specs/1092-mortal-command-fixture-coverage` for this branch: `AVAILABLE_DOCS` included `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, and `tasks.md` before Codex launch.
- [x] T045 Run build, `git diff --check`, and added-line static scan; record exact results. Client build: 0 warnings / 0 errors. Test build: 0 warnings / 0 errors. Working-tree `git diff --check`: exit 0 with only LF-to-CRLF warnings. Added-line static scan: no matches for real secrets, shell/eval execution, unsafe deserialization, or SQL string-formatting patterns.
- [x] T046 Reconcile docs/prompts impact and confirm no runtime afterlife contract, GM prompt, or dedicated Shining Abode #1097 fixture scope changed. Result: no GM prompts/examples/contracts changed; no afterlife runtime state contract files were added/renamed/removed. Player-facing display behavior changed only for `/archive_project_fuel` default preview when no active project exists, returning completed unavailable output instead of a blocked display result; write paths still block without canonical active project authority. Shining Abode #1097 remains out of scope.
- [x] T047 Commit #1096 implementation with `[skip ci]`: `test: add reusable Chaos Sea command display save [skip ci]`.

---

## Phase 8: #1097 Reusable Shining Abode Save Continuation

**Goal**: Package representative Shining Abode afterlife command-display state as a tracked reusable save/load-compatible artifact that survives clean checkouts and can be validated/rendered repeatedly without mixing in the dedicated Chaos Sea fixture task.

**Independent Test**: Load the named save into a disposable session root, run validation with zero blocking issues, and run console/browser command-display smoke checks for every command available in the Shining Abode save.

- [x] T048 Confirm source issue #1097, create isolated branch `work/1097-shining-abode-command-save`, and mark the GitHub issue in progress. Evidence: worktree `E:/Games/worktrees/boe-1097-shining-abode-command-save`; labels now include `status: in-progress` and `codex-agent in-progress`.
- [x] T049 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~FileSystemExampleAfterlifeStateExamplesTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~Validation" --logger "console;verbosity=minimal"` => 1780 passed / 0 failed / 0 skipped / 1780 total.
- [x] T050 Update active Spec Kit artifacts to reference #1097 and reusable Shining Abode save packaging scope.
- [x] T051 Inspect save/load code, Shining Abode display builders, and existing FileSystemExample afterlife state to confirm the tracked save path `FileSystemExample/game_session/saves/manual_saves/shining_abode_command_display_fixture.zip` plus metadata sidecar. Implementation note: `SaveLoadService` strips live `input/` and `game_state/control/pending_turn_snapshot*` artifacts, so the #1097 fixture is an at-rest manual save.
- [x] T052 Add RED discoverability/loadability/validation tests for the named reusable Shining Abode save, expected to fail before the archive/metadata exist. RED evidence: focused `ShiningAbodeCommandDisplaySaveTests` initially failed on missing `shining_abode_command_display_fixture.zip`; the narrow discoverability test reported 0 passed / 1 failed / 0 skipped / 1 total for that intended missing-archive reason.
- [x] T053 Add RED or mutation-backed console/browser command-display smoke coverage for every command available in the loaded Shining Abode save, including relevant universal afterlife/status commands and detail-capable invocations. Coverage: all `ExplorerCommandGroup.ShiningAbode` and `ExplorerCommandGroup.AfterlifeCombatAndEntities` aliases, practical universal afterlife/status commands, and representative detail targets render through browser command service plus console renderer.
- [x] T054 Create or copy the reusable named Shining Abode display-test save and metadata using the existing save/load-compatible layout: added `shining_abode_command_display_fixture.zip` and `shining_abode_command_display_fixture_metadata.json` under `FileSystemExample/game_session/saves/manual_saves/`.
- [x] T055 Fill representative Shining Abode state so each available command returns useful player-facing data or a clear in-world unavailable reason; include Chaos Sea data only as historical/contextual references required by Shining Abode output. Implementation note: the archive carries mandatory afterlife bootstrap lore under `lore/chaos_sea/`, but no Chaos Sea command-display state; `ValidationService` now permits idle manual-save guardian references from current stored guardian state only when no live input, pending snapshot, or current guardian mutation surface exists.
- [x] T056 Document save location, loading steps, command coverage matrix/checklist, representative invocations, expected visible data, and verification commands in `quickstart.md`, the coverage contract/checklist, `data-model.md`, `plan.md`, `spec.md`, and `FileSystemExample/README.md` as needed. Added `contracts/shining-abode-command-fixture-checklist.md`.
- [x] T057 Run focused and broader validation/Explorer command tests; record exact GREEN counts in this task list and the PR/issue evidence. GREEN evidence: focused `ShiningAbodeCommandDisplaySaveTests` => 101 passed / 0 failed / 0 skipped / 101 total; broader afterlife/fixture/Explorer/Validation gate => 1780 passed / 0 failed / 0 skipped / 1780 total.
- [x] T058 Run Spec Kit prerequisite check and confirm the helper resolves `FEATURE_DIR` to `specs/1092-mortal-command-fixture-coverage` for this branch: `AVAILABLE_DOCS` included `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, and `tasks.md`.
- [x] T059 Run build, `git diff --check`, and added-line static scan; record exact results. Client build: 0 warnings / 0 errors. Test build: 0 warnings / 0 errors. Staged `git diff --cached --check`: exit 0. Added-line static scan over staged C# production/test lines: `NO_MATCHES` for hardcoded secrets, shell/eval execution, unsafe deserialization, or SQL string-formatting patterns.
- [x] T060 Reconcile docs/prompts impact and confirm no runtime afterlife contract, GM prompt, or dedicated Chaos Sea #1096 fixture scope changed. Result: no GM prompts/examples/contracts changed; no afterlife pending/control file, `actionType`, response field, receipt, report, or command behavior contract changed. The only runtime code change is a narrow `ValidationService` idle manual-save reference fallback for loaded saves with no live turn or guardian mutation authority.
- [x] T061 Commit #1097 implementation with `[skip ci]`: `test: add reusable Shining Abode command display save [skip ci]`.

---

## Final Phase: Polish & Cross-Cutting Concerns

- [x] T018 Run focused C# verification and record exact results
- [x] T019 Run manual/local fixture validation or document why it could not be automated
- [x] T020 Reconcile `tasks.md` and the fixture matrix before reporting completion

## Dependencies & Execution Order

- Phase 1 and Phase 2 must complete before fixture edits.
- US1 can be validated independently from US2.
- US3 depends on the final matrix and verification evidence.

## Implementation Strategy

1. Complete command inventory and file mapping.
2. Repair read-only display data first.
3. Repair local-turn prompt/action data second.
4. Update durable matrix and verification evidence.
