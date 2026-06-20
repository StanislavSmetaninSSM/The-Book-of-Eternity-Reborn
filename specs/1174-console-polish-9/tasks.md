# Tasks: Console Client Polish Pass 2 to 9/10

**Input**: `specs/1174-console-polish-9/spec.md`, `plan.md`, source GitHub issues #1174-#1183, `AGENTS.md`, `.specify/memory/constitution.md`.

**Tests**: Behavior changes require RED/GREEN tests before production edits.

**Hard exclusions**: Do not modify browser/frontend files. Do not modify QTE engine, QTE balance, QTE scenarios, or QTE test mode.

## Phase 1: Setup and Baseline

- [x] T001 Create isolated worktree `E:/Games/worktrees/boe-1174-console-polish-9` on branch `work/1174-console-polish-9`.
- [x] T002 Confirm source issues #1174-#1180 exist and are linked in Spec Kit artifacts.
- [x] T003 Run baseline build for `BookOfEternityClient/BookOfEternityClient.csproj`.
- [x] T004 Run baseline build for `BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj`.
- [x] T005 Run Spec Kit prerequisite check for `specs/1174-console-polish-9`.
- [x] T006 Mark source issues #1174-#1180 with `codex-agent in-progress` once implementation begins.

## Phase 2: Command Matrix and Fixture Coverage (#1175)

- [x] T007 Inspect console command registration, aliases, help text, and command result builders.
- [x] T008 Inspect reusable Mortal World, Chaos Sea, and Shining Abode command-display fixtures/tests.
- [x] T009 Create `docs/audits/console-command-matrix-1174.md` with every ordinary non-QTE console command, realm, lifecycle, fixture coverage, expected output shape, and current status.
- [x] T010 Create follow-up issues for missing fixture data that blocks honest command inspection.
- [x] T011 Update #1175 with the matrix path and coverage summary.

## Phase 3: Dry Command Sweep (#1176)

- [x] T012 Identify the safest existing command execution/test harness for non-interactive command rendering.
- [x] T013 Add RED tests for dry sweep classification of raw JSON/internal keys/markup errors on representative command output.
- [x] T014 Implement or document the dry sweep runner/report format.
- [x] T015 Run the sweep on prepared non-QTE command fixtures and save a lightweight report under `docs/audits/`.
- [x] T016 Update #1176 with the sweep command, report path, and first findings.

## Phase 4: Mortal World Output and Navigation Polish (#1177)

- [x] T017 Run targeted command-display tests and dry sweep for Mortal World commands.
- [x] T018 Add RED tests for the highest-impact narrow Mortal World output/navigation defects.
- [x] T019 Implement narrow fixes in console renderers/builders without touching browser files.
- [x] T020 Rerun focused Mortal World command-display tests and affected dry sweep entries.
- [x] T021 Create follow-up issues for broad Mortal World UX gaps not safely fixed in this branch.
- [x] T022 Update #1177 with fixes, tests, and residual issues.

## Phase 5: Afterlife Output and Navigation Polish (#1178)

- [x] T023 Run targeted command-display tests and dry sweep for Chaos Sea and Shining Abode commands.
- [x] T024 Add RED tests for the highest-impact narrow afterlife output/navigation defects.
- [x] T025 Implement narrow afterlife display fixes. If runtime/GM contracts change, update afterlife docs/examples/tests in the same commit.
- [x] T026 Rerun focused afterlife command-display tests and documentation coverage tests when required.
- [x] T027 Create follow-up issues for broad afterlife UX gaps not safely fixed in this branch.
- [x] T028 Update #1178 with fixes, tests, and residual issues.

## Phase 6: Live Codex-GM E2E Playtest Without QTE (#1179)

- [x] T029 Prepare a disposable game session and launch metadata for the console client, Agent Console, GM daemon, and Codex bridge.
- [x] T030 Run a short adventure without QTE that covers conversation, exploration, inventory/item, book/document, NPC/faction/quest/world-news, simple non-QTE action, lifecycle/reward/afterlife where feasible.
- [x] T031 Record route notes, command outputs, friction findings, bridge status, and player-visible failures in `docs/audits/console-live-playtest-1179.md`.
- [x] T032 File or link follow-up issues for blocker/major friction not fixed before the final audit.
- [x] T033 Update #1179 with the live report and residual risk.

## Phase 7: Final Acceptance Audit (#1180)

- [x] T034 Create `docs/audits/console-acceptance-score-1180.md` with the final 1-10 rubric.
- [x] T035 Run focused tests for all changed command surfaces.
- [x] T036 Run final build and broad non-browser C# verification.
- [x] T037 Run `git diff --check` and inspect `git diff --stat`.
- [x] T038 Update #1180 with score, verification commands, changed files, and residual risks.
- [x] T039 Commit with source issue references, merge after verification evidence, and close completed issues only after evidence is inspected.

## Phase 8: Follow-Up Console 9/10 Gaps (#1181, #1182, #1183)

- [x] T040 Link follow-up issues #1181, #1182, and #1183 in Spec Kit artifacts before implementation.
- [x] T041 Add RED focused dry-sweep coverage for `/сареф` and memory-scene aliases using `ConsoleCommandOutputQualityClassifier`.
- [x] T042 Replace default `/сареф` raw JSON output with player-facing story detail tables without changing afterlife runtime contracts.
- [x] T043 Verify Saref and memory-scene focused command quality coverage.
- [x] T044 Add RED Agent Console observability tests for command drilldowns/options menu.
- [x] T045 Implement Agent Console prompt/menu snapshot fixes without browser or QTE changes.
- [x] T046 Investigate #1181 GM fact persistence root cause and choose the smallest safe contract/test slice.
- [x] T047 Implement #1181 only if it can be fixed without broad narrative/runtime risk; otherwise document the required contract work precisely.
- [x] T048 Rerun focused follow-up tests and broad non-browser C# verification before merge.

## Evidence Log

- Worktree: `E:/Games/worktrees/boe-1174-console-polish-9`, branch `work/1174-console-polish-9`, created from `origin/main` at `4c25586`.
- Baseline client build: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj` succeeded with 0 warnings and 0 errors.
- Baseline test-project build: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj` succeeded with 0 warnings and 0 errors.
- Baseline full test note: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj` did not complete before the 120s tool timeout; targeted verification will be used during implementation and broad non-browser suite will be retried before merge.
- Spec Kit prerequisite check: `$env:SPECIFY_FEATURE_DIRECTORY='specs/1174-console-polish-9'; .\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\Games\worktrees\boe-1174-console-polish-9\specs\1174-console-polish-9` and `AVAILABLE_DOCS=["tasks.md"]`.
- Command matrix: `docs/audits/console-command-matrix-1174.md` covers all 91 `ExplorerCommandCatalog` descriptors, excludes QTE/browser, and maps reusable Mortal/Chaos/Shining display-save coverage. No blocking missing-fixture issue was required before dry sweep; universal/Saref/diagnostic gaps are marked as sweep targets in the matrix.
- GitHub #1175 updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1175#issuecomment-4757658750
- Dry sweep RED 1: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ConsoleCommandOutputQualityClassifierTests"` failed to compile because `ConsoleCommandOutputQualityClassifier` did not exist.
- Dry sweep GREEN 1: same classifier filter passed 2, failed 0 after adding `ConsoleCommandOutputQualityClassifier`.
- Dry sweep RED 2: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Classify_DefaultPlayerOutputFlagsAfterlifeContractMarkers"` failed 1, passed 0 before adding `pending_`, `requestId`, `actionType`, and `protocol` markers.
- Dry sweep GREEN 2: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ConsoleCommandOutputQualityClassifierTests" --logger "console;verbosity=minimal"` passed 3, failed 0.
- Dry sweep RED 3: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Classify_DefaultPlayerOutputFlagsDebugMarker" --logger "console;verbosity=minimal"` failed 1, passed 0 before restoring the old afterlife `debug` marker policy in the shared classifier.
- Dry sweep RED 4: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Classify_DefaultPlayerOutputFlagsNullMarker" --logger "console;verbosity=minimal"` failed 1, passed 0 before adding literal `null` leakage to the shared classifier.
- Dry sweep GREEN 4: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ConsoleCommandOutputQualityClassifierTests" --logger "console;verbosity=minimal"` passed 5, failed 0.
- Dry sweep baseline: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ConsoleCommandOutputQualityClassifierTests|MortalCommandDisplaySaveTests|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests" --logger "console;verbosity=minimal"` passed 298, failed 0. Report: `docs/audits/console-dry-sweep-1176.md`.
- GitHub #1176 updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1176#issuecomment-4757691343
- GitHub #1176 dry-sweep correction updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1176#issuecomment-4757710615
- GitHub #1176 null-leakage correction updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1176#issuecomment-4757737678
- Mortal/afterlife RED 1: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExecuteAsync_Gacha_ReturnsDirectChaosSeaPrompt|ExecuteAsync_Gacha_WhenPendingDiceMissing_CreatesAuthoritativeBaseForPromptAndSubmit" --logger "console;verbosity=minimal"` failed 2, passed 0 while gacha preview still displayed `Common/Uncommon/Rare/Epic/Legendary`.
- Mortal/afterlife GREEN 1: same gacha filter passed 2, failed 0 after localizing gacha rarity thresholds and base-rarity text.
- Afterlife RED 2: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ChaosSeaCommandDisplaySaveTests.LoadedChaosSeaCommandDisplaySave_RendersRepresentativeDetailTargets|ShiningAbodeCommandDisplaySaveTests.LoadedShiningAbodeCommandDisplaySave_RendersRepresentativeDetailTargets" --logger "console;verbosity=minimal"` failed 2, passed 39 while `/archive_project_fuel` leaked `display_snapshot`, `visible`, and `display`.
- Afterlife GREEN 2: same afterlife detail filter passed 41, failed 0 after localizing project type/tier/mode labels.
- Mortal RED 2: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "StructuredBonusDisplayTests" --logger "console;verbosity=minimal"` failed 2, passed 0 while `StructuredBonusDisplay` returned literal `null`/`undefined`.
- Mortal GREEN 2: same structured formatter filter passed 3, failed 0 after omitting technical empty JSON values and null child fields from player-facing structured output.
- Combined polish sweep: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ConsoleCommandOutputQualityClassifierTests|StructuredBonusDisplayTests|MortalCommandDisplaySaveTests|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests" --logger "console;verbosity=minimal"` passed 301, failed 0.
- GitHub #1177 polish batch updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1177#issuecomment-4757820584
- GitHub #1178 polish batch updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1178#issuecomment-4757822308
- Live playtest #1179 run root: `C:\Temp\boe-live-e2e-1179-20260620-223955`; commit under test `598bb369db8c1b12c29c5f1efc598b57293f55b0`; Agent Console `http://127.0.0.1:60547`; QTE disabled.
- Live command sweep #1179: 29 Mortal World read-only commands returned without hangs; report artifact `mortal-command-pass-summary.json` in the run root.
- Live adventure #1179: two Codex-GM Mortal World turns completed with readable narrative; location movement was reflected by `/карта` and `/где_я`.
- Live playtest report: `docs/audits/console-live-playtest-1179.md`.
- Follow-up #1181 created for accepted GM narrative facts not persisting into `/нпс`, `/хроника`, and `/квесты`: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1181
- Follow-up #1182 created for Agent Console drilldown/options observability gaps: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1182
- Console polish RED map/location/mods: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Render_MapUsesPlayerFacingRealmAndCurrentLocationName|TryProcessCommand_MapRussian_InMortalRealm_OpensVisualMapViewerInsteadOfLocationList|TryProcessCommand_Locations_HidesUnknownAdjacentLinkState|TryProcessCommand_SystemMods_HidesTechnicalFileNamesInPlayerChoices" --logger "console;verbosity=minimal"` failed 4, passed 0 before the player-facing label fixes.
- Console polish GREEN map/location/mods: same filter passed 4, failed 0 after adding shared player-facing labels and hiding raw map/mod/location state.
- Console polish expanded verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExplorerCommandResultConsoleRendererTests|TryProcessCommand_MapRussian_InMortalRealm_OpensVisualMapViewerInsteadOfLocationList|TryProcessCommand_Locations_RendersWithoutHiddenErrors|TryProcessCommand_Locations_HidesUnknownAdjacentLinkState|TryProcessCommand_SystemMods_RendersDetailLoopWithoutHiddenErrors|TryProcessCommand_SystemMods_HidesTechnicalFileNamesInPlayerChoices|MortalCommandDisplaySaveTests|ConsoleCommandOutputQualityClassifierTests" --logger "console;verbosity=minimal"` passed 108, failed 0.
- GitHub #1177 map/location/mods polish updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1177#issuecomment-4758154987
- GitHub #1179 live report updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1179#issuecomment-4758156399
- Follow-up #1183 created for Saref/memory-scene reusable afterlife output coverage: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1183
- GitHub #1178 residual afterlife coverage updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1178#issuecomment-4758163203
- Full all-tests note: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --logger "console;verbosity=minimal"` passed 4679 and failed 3 before final test expectation cleanup; 2 failures were `LocalWebUiBuiltFrontendSmokeTests` missing browser `dist`, outside the console-only scope, and 1 was an outdated `ExplorerWebCommandServiceTests` expectation for raw `lore_research`.
- Afterlife web-command expectation fix verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExecuteAsync_AfterlifeRelicArchiveDetails_RenderFocusedPlayerFacingDetailWithoutRawJson" --logger "console;verbosity=minimal"` passed 5, failed 0.
- Final client build: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` passed with 0 warnings and 0 errors.
- Final broad non-browser C# verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"` passed 4680, failed 0.
- Final whitespace check: `git diff --check` exited 0 with CRLF normalization warnings only.
- Acceptance audit: `docs/audits/console-acceptance-score-1180.md`, score 8/10 with 9/10 conditions tied to #1181, #1182, and #1183.
- GitHub #1180 acceptance audit updated: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1180#issuecomment-4758246978
- Follow-up branch: `E:/Games/worktrees/boe-1181-console-followups`, branch `work/1181-console-followups`, created from `origin/main` at `0dd1899`.
- Follow-up baseline restore/build note: initial `--no-restore` build failed because the new worktree had no `project.assets.json`; `dotnet restore BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj` restored both projects. Parallel build of client and tests conflicted on a shared `obj` DLL lock, so follow-up builds/tests are run sequentially.
- Follow-up baseline client build: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` passed with 0 warnings and 0 errors.
- Follow-up baseline test-project build: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore` passed with 0 warnings and 0 errors.
- #1183 RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "SarefCommandDisplayQualityTests" --logger "console;verbosity=minimal"` failed 2, passed 4 because `/сареф` and `/saref` returned `UiRawJsonBlock`, leaked `game_state/` and `.json`, and lacked useful story detail text.
- #1183 GREEN: same filter passed 6, failed 0 after adding focused Saref/memory-scene quality coverage and replacing default `/сареф` raw JSON with player-facing detail tables.
- #1182 RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsoleLiveControl_InGameOptionsCommandPublishesMenuSnapshotAndCloses" --logger "console;verbosity=minimal"` timed out on the stale `game-loop` snapshot because `/опции` did not publish a menu observation; `AgentConsoleLiveControl_CommandDrilldownSelectionPromptPublishesMenuSnapshot` timed out on `explorer-command-1` because explorer `SelectionPrompt` drilldowns were flattened into wait-key text snapshots.
- #1182 GREEN narrow: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsoleLiveControl_CommandDrilldownSelectionPromptPublishesMenuSnapshot|AgentConsoleLiveControl_InGameOptionsCommandPublishesMenuSnapshotAndCloses" --logger "console;verbosity=minimal"` passed 2, failed 0 after publishing live menu snapshots for explorer selection prompts and generic single-choice menus.
- #1182 GREEN adjacent Agent Console suite: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsoleLiveControl_CommandCaptureDoesNotLeakAndSelectionPromptsDoNotThrow|AgentConsoleLiveControl_CommandDrilldownSelectionPromptPublishesMenuSnapshot|AgentConsoleLiveControl_InGameOptionsCommandPublishesMenuSnapshotAndCloses|AgentConsoleRecordingExplorerConsoleTests|AgentConsoleLiveInputSourceTests|AgentConsoleObservationTests" --logger "console;verbosity=minimal"` passed 31, failed 0.
- #1181 root cause: live run `C:\Temp\boe-live-e2e-1179-20260620-223955` showed Иветта/Ролан/clue facts in `output/narrative_response.json`, `output/debug_logs.json`, and `current_location.lastEventsDescription`, but no `game_state/npcs/npc_core.json`, no new quest surface, and no chronicle/world-event surface. This is a GM accepted-turn persistence contract gap, not a console renderer bug.
- #1181 RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "MortalRelevantNpc" --logger "console;verbosity=minimal"` failed 1, passed 1 because `AcceptedTurnReasoning_MortalRelevantNpcWithoutPersistenceReportsError` received no validation issues.
- #1181 GREEN: same `MortalRelevantNpc` filter passed 2, failed 0 after adding `mortal_relevant_actor_missing_persistence` for Mortal World relevant NPC actors that appear only in reasoning/narrative and not in player-facing NPC state.
- #1181 focused scope regression: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "MortalRelevantNpc|ShiningPoliticalActorDiffRequiresActorScope|ShiningPoliticalActorDiffPassesWithActorBrainBlock|ShiningFactionDiffRequiresActorScope" --logger "console;verbosity=minimal"` passed 5, failed 0.
- #1181 follow-up refinement: `NPCsInScene` is now included in accepted-turn actor-scope extraction, so scene-present NPCs can satisfy `mortal_relevant_actor_missing_persistence` without requiring an unrelated full `UpdateNPCs` rewrite.
- #1181 GM docs verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` passed 103, failed 0 after updating `TaskGuides/CLI_Step_Main.txt`, `Rules/Block_FINAL.txt`, and `Examples/E_CLI_Step_Main.txt`.
- Follow-up final build: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore` passed with 0 warnings and 0 errors.
- Follow-up broad non-browser C# verification: an initial `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"` run passed 4690, failed 0 before the final `NPCsInScene` extraction refinement. After that refinement, the full broad run twice exposed unrelated flaky lifecycle behavior: `CheckLifeTransitions_VoluntaryEnd_DispatchesAutomaticLifeEvaluationInsteadOfReturningToMortalWorld` failed with a transient file lock in broad mode but passed alone, and `CheckLifeTransitions_FileSystemExampleState_DispatchesAutomaticLifeEvaluation` timed out in broad mode but passed alone.
- Follow-up broad-minus-known-flaky verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests&FullyQualifiedName!~CheckLifeTransitions_VoluntaryEnd_DispatchesAutomaticLifeEvaluationInsteadOfReturningToMortalWorld&FullyQualifiedName!~CheckLifeTransitions_FileSystemExampleState_DispatchesAutomaticLifeEvaluation" --logger "console;verbosity=minimal"` passed 4688, failed 0; both excluded lifecycle tests passed individually.
- Follow-up whitespace check: `git diff --check` exited 0 with CRLF normalization warnings only.
