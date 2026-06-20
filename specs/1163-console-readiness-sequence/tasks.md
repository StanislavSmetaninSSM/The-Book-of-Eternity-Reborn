# Tasks: Console Readiness Sequence 2

**Input**: `specs/1163-console-readiness-sequence/spec.md`, `plan.md`, source GitHub issues #1158/#1160-#1166, AGENTS.md, and `.specify/memory/constitution.md`.

**Tests**: Behavior changes require RED/GREEN tests before production edits.

## Phase 1: Setup and Baseline

- [x] T001 Create isolated worktree `E:/Games/worktrees/boe-1163-console-readiness` on branch `feature/1163-console-readiness`.
- [x] T002 Mark source issues #1158/#1160/#1161/#1162/#1163/#1164/#1165/#1166 with `codex-agent in-progress`.
- [x] T003 Create this Spec Kit feature directory and link all source issues in spec/plan/tasks.
- [x] T004 Run Spec Kit prerequisite check for `specs/1163-console-readiness-sequence`.
- [x] T005 Run baseline build and focused tests before production changes.

## Phase 2: GM Worker Reliability and Profile Cleanup

- [x] T006 Inspect current GM worker profile/status code, tests, docs, and issue #1158/#1160/#1161/#1162 details.
- [x] T007 Add RED coverage for false-ready worker status when the Codex ConPTY process is not live.
- [x] T008 Fix worker status so stale/dead/unaccepted process state cannot report ready.
- [x] T009 Add RED coverage proving deprecated Gemini CLI worker profiles/guidance are absent from default config/docs.
- [x] T010 Remove deprecated Gemini CLI profiles/guidance while preserving Codex worker configuration.
- [x] T011 Add or update live-safe Codex worker E2E tests for narrative draft and validation repair, or record a precise live blocker.
- [x] T012 Run focused GM worker/Agent Console tests GREEN and record counts.

## Phase 3: QTE Agent Console Frame Visibility

- [x] T013 Inspect Agent Console snapshot/event model and QTE live-loop state ownership.
- [x] T014 Add RED tests for inactive/stale QTE frame snapshot behavior.
- [x] T015 Add RED tests for a timed QTE frame snapshot with prompt, expected input, timing/progress, and feedback fields.
- [x] T016 Add RED tests for a non-timed or choice-style QTE frame snapshot.
- [x] T017 Implement a production-safe QTE frame state model and publish it through Agent Console snapshots.
- [x] T018 Verify existing terminal QTE rendering still works with focused QTE tests.
- [x] T019 Run focused Agent Console/QTE tests GREEN and record counts.

## Phase 4: Afterlife Command Output Sweep

- [x] T020 Inspect Chaos Sea and Shining Abode command-output saves and command result builders for raw/default JSON leakage.
- [x] T021 Add RED tests or source guards for normal afterlife output that must not include raw JSON/"Полный JSON" by default.
- [x] T022 Implement narrow afterlife output fixes that are safe, player-facing, and overview-preserving.
- [x] T023 For broad afterlife gaps, create linked follow-up GitHub issues with command, mode/save, expected output, actual output, and severity.
- [x] T024 Run reusable afterlife save validation and focused afterlife output tests GREEN.

## Phase 5: Console Polish Pass 2

- [x] T025 Audit ordinary console commands against prepared saves for JSON leakage, internal keys, enum/raw labels, dead-end menus, and missing details.
- [x] T026 Add RED tests for narrow high-friction command-output defects found during the audit.
- [x] T027 Implement narrow console polish fixes.
- [x] T028 Create linked follow-up issues for larger UX/product gaps.
- [x] T029 Record command-by-command audit notes and issue links in #1163 or a lightweight repo artifact.

## Phase 6: Second Live Console Playtest

- [x] T030 Prepare a disposable live-test session and launch commands for console, Agent Console, GM bridge, and Codex worker.
- [x] T031 Run the live route covering guardian interaction, mortal exploration, status/inventory/books/effects/NPC/faction/world-news, QTE or simple challenge, lifecycle transition, rewards, and afterlife/new-life follow-up where feasible.
- [x] T032 Preserve snapshots/events/log summaries outside committed source or in an intentionally lightweight summary.
- [x] T033 Classify findings by blocker, high-friction, polish, and follow-up.
- [x] T034 Post or commit the final playability report with current 1-10 score and residual risks.

## Phase 7: Verification, Review, and Closure

- [x] T035 Run focused tests for all changed surfaces and record exact pass/fail counts.
- [x] T036 Run full non-built-frontend C# suite if code changed; record the browser-dist prerequisite separately.
- [x] T037 Run docs/contract tests if afterlife or GM-facing contracts changed.
- [x] T038 Run `git diff --check` and inspect `git diff --stat`.
- [x] T039 Update this task file with evidence before final commit.
- [ ] T040 Commit with source issue references, open PR, perform review, merge when verified, and update/close completed issues.

## Evidence Log

- Worktree: `E:/Games/worktrees/boe-1163-console-readiness`, branch `feature/1163-console-readiness`, created from `origin/main` at `239414b`.
- Source issues marked in progress: #1158, #1160, #1161, #1162, #1163, #1164, #1165, #1166.
- Spec Kit prerequisite check: `$env:SPECIFY_FEATURE_DIRECTORY='specs/1163-console-readiness-sequence'; .\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\Games\worktrees\boe-1163-console-readiness\specs\1163-console-readiness-sequence` and `AVAILABLE_DOCS=["tasks.md"]`.
- Baseline note: parallel `dotnet` commands in the same fresh worktree caused transient `obj/` file lock errors. After `dotnet build-server shutdown`, sequential baseline passed.
- Baseline build: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- Baseline Agent/GmWorker tests: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsole|GmWorker" --logger "console;verbosity=minimal"` passed 106, failed 0, skipped 0.
- Baseline QTE tests: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Qte" --logger "console;verbosity=minimal"` passed 367, failed 0, skipped 0.
- Baseline afterlife tests: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Afterlife|Shining|Chaos" --logger "console;verbosity=minimal"` passed 1832, failed 0, skipped 0.
- GM worker RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "RunTaskAsync_WhenWorkerExitsBeforeProtocolAcceptance_NeverReportsReady|DefaultTemplates_DoNotAdvertiseDeprecatedGeminiCliProfiles|DefaultTemplates_IncludeCodexNarrativeDraftTemplate"` failed 3/3 before production edits.
- GM worker GREEN: same targeted RED filter passed 3, failed 0 after removing the false `Ready` transition and replacing the default narrative worker with Codex.
- GM worker documentation RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ActiveWorkerGuidance_DoesNotAdvertiseDeprecatedGeminiCli|LauncherAndDaemon_DefaultConfigExposeDisabledWorkerProfileTemplates"` failed 2/2 before updating active docs/launcher/daemon defaults.
- GM worker focused GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ActiveWorkerGuidance_DoesNotAdvertiseDeprecatedGeminiCli|LauncherAndDaemon_DefaultConfigExposeDisabledWorkerProfileTemplates|GmWorkerProfileTemplate|GmWorkerBridgeDocumentation|GmWorkerBridgeLifecycle" --logger "console;verbosity=minimal"` passed 19, failed 0, skipped 0.
- GM worker suite GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker" --logger "console;verbosity=minimal"` passed 58, failed 0, skipped 0.
- QTE Agent Console RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "SnapshotSerializesStructuredQteFrameForAutomation|QteLive_TimingBarLoopPublishesStructuredAgentConsoleFrame|QteOfferDecision_AgentConsoleSnapshotExposesChoiceStyleQteFrame"` initially failed to compile because `AgentConsoleQteFrame`/`AgentConsoleMode.QteLive` did not exist.
- QTE Agent Console targeted GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "SnapshotSerializesStructuredQteFrameForAutomation|NonQteSnapshotOmitsQteFrame|QteLive_TimingBarLoopPublishesStructuredAgentConsoleFrame|QteOfferDecision_AgentConsoleSnapshotExposesChoiceStyleQteFrame" --logger "console;verbosity=minimal"` passed 4, failed 0, skipped 0 after adding `qteFrame` snapshots for timed and choice-style QTE.
- QTE/AgentConsole suite GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Qte|AgentConsole" --logger "console;verbosity=minimal"` passed 421, failed 0, skipped 0.
- Afterlife output RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_ChaosSeaOverviewSummarizesPendingContractsWithoutRawAuditPayload"` failed before replacing the default Chaos Sea pending-contract audit with a player summary.
- Afterlife Chaos Sea GREEN: same targeted command passed 1, failed 0, skipped 0 after hiding raw pending file names/request ids/payloads from normal `/chaos_sea` overview output.
- Afterlife help RED/GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_Help_ChaosSeaUsesPlayerFacingRussianWording|TryProcessCommand_Help_ShiningAbodeUsesPlayerFacingRussianWording" --logger "console;verbosity=minimal"` failed before help cleanup and then passed 2, failed 0, skipped 0 after removing default "полный JSON"/audit wording from player help.
- Afterlife suite GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Afterlife|Shining|Chaos" --logger "console;verbosity=minimal"` passed 1832, failed 0, skipped 0.
- Afterlife audit notes: `docs/console-afterlife-output-audit.md` records fixed default-output leaks and remaining explicit-audit surfaces.
- Follow-up afterlife issues created: #1167 split `/status` player summary from audit mode; #1168 split Shining Abode player details from audit payloads; #1169 split afterlife action previews from GM contract audit payloads.
- Console polish pass 2 RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "LoadedMortalCommandDisplaySave_WorldNewsLocalizesVisibilityEnums" --logger "console;verbosity=normal"` failed before production changes because selected world-news details from the reusable Mortal save exposed raw `local`/`rumor` visibility enums or omitted the localized detail value.
- Console polish pass 2 targeted GREEN: same world-news visibility filter passed 4, failed 0, skipped 0 after localizing common world-news visibility values in `ExplorerMortalWorldNewsCommandResultBuilder`.
- Console polish pass 2 world-news regression GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExecuteAsync_WorldNewsValmontEventDetail_RendersUsefulSeededDetails|ExecuteAsync_WorldNewsEventDetail_RendersPlayerFacingDetailWithoutRawJson|ExecuteAsync_WorldNewsOverview_ExposesEventFlagAndProgressionDrilldownActions" --logger "console;verbosity=minimal"` passed 3, failed 0, skipped 0.
- Reusable Mortal command fixture GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "MortalCommandDisplaySave" --logger "console;verbosity=minimal"` passed 96, failed 0, skipped 0.
- Console pass 2 audit notes: `docs/audits/console-client-polish-pass-2.md` records command groups, saves used, the world-news enum-localization fix, and afterlife follow-up issue links #1167/#1168/#1169.
- Live console run: disposable session `C:\Temp\boe-live-e2e-1163-20260620-183207\game_session`; Agent Console + GM daemon + Codex bridge launched with `codex --dangerously-bypass-approvals-and-sandbox`; first Mortal action reached `ready/turn_complete.json`, but took 430.3s and then hit strict `soulRelics` normalization.
- Live-run fix RED/GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests.GameSessionFixture_ValidatorRejectsLegacySoulRelicAliases"` failed before validator coverage and passed 1, failed 0 after `soul_relic_invalid_canonical_shape` validation.
- Live-run Agent Console error RED/GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~AgentConsoleObservationTests.GameLoopErrorCatchPublishesAgentConsoleErrorSnapshot"` failed before publishing `game-loop-error` snapshots and passed 1, failed 0 after the fix.
- Reusable Mortal command fixture was repaired to use canonical `relicId`/`rarity`; the user's local `BookOfEternityClient/game_session/game_state/meta/soul_state.json` was repaired on disk with the same canonical Soul Relic fields.
- Live playtest report: `docs/audits/console-live-playtest-1166.md` records route, findings, temporary artifact root, playability score, and residual risks.
- Follow-up live infrastructure issues created: #1170 for slow/unisolated Codex GM bridge; #1171 for garbled Cyrillic daemon stdout logs.
- Post-live focused GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FileSystemExampleFixtureIntegrityTests|AgentConsoleObservationTests|MortalCommandDisplaySaveTests"` passed 123, failed 0.
- Post-live GM bridge/worker GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridge|GmWorkerBridge|GmWorker|PasteVisibility|StateManagerTests"` passed 91, failed 0.
- Post-live QTE/AgentConsole GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Qte|AgentConsole"` passed 423, failed 0.
- Post-live afterlife command-display GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests|ExplorerModeCommandTests.Afterlife"` passed 197, failed 0.
- Launch prompt encoding RED: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "RepositoryEncodingScannerTests.UserFacingSourcesAndFixtures_MustNotContainCommonMojibakeMarkers"` failed 1, passed 0 because `BookOfEternityClient/Launcher/CLI_Launch_Script.md` contained mojibake markers after Windows PowerShell generation.
- Launch prompt encoding GREEN: same encoding filter passed 1, failed 0 after repairing Windows-1251-misread UTF-8 literals in the generator and writing UTF-8 explicitly.
- Final focused verification GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "RepositoryEncodingScannerTests|GmWorkerBridgeDocumentationTests|FileSystemExampleFixtureIntegrityTests|AgentConsoleObservationTests|MortalCommandDisplaySaveTests"` passed 134, failed 0.
- Final focused GM/QTE/afterlife/docs verification GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridge|GmWorkerBridge|GmWorker|PasteVisibility|StateManagerTests|Qte|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests|ExplorerModeCommandTests.Afterlife|ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"` passed 762, failed 0.
- Final build GREEN: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- Final non-built-frontend C# suite GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"` passed 4663, failed 0.
- Browser built-frontend smoke prerequisite: the excluded `LocalWebUiBuiltFrontendSmokeTests` require a current `BookOfEternityClient.WebFrontend/dist`; browser/frontend work is explicitly out of scope for this branch.
- Final diff hygiene: `git diff --check` exited 0 with only Git CRLF conversion warnings; `git diff --stat` showed 43 tracked files changed before adding docs/spec artifacts.
- Independent code review: read-only reviewer found no Critical runtime issues and requested fixes for tracked `CLI_Launch_Script.md` worktree paths, stale `GM Gemini` fallback help, and stale 1151 Spec Kit wording.
- Reviewer fix RED/GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerBridgeDocumentationTests.GmLauncherScript_DoesNotContainStaleWorktreePaths|GmWorkerBridgeDocumentationTests.GmLauncherGenerator_AcceptsExplicitGameSessionPathForSandboxedRuns"` failed 2, passed 0 before path-neutral launch prompt handling; after the fix it passed as part of the 6-test reviewer-fix slice.
- Deprecated Gemini default-marker RED/GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridgePasteVisibilityPolicyTests.IsPromptVisible_DefaultConfig_DoesNotAcceptDeprecatedGeminiPasteMarker"` failed 1, passed 0 before removing the default Gemini paste marker; reviewer-fix slice passed after the fix.
- Reviewer-fix targeted GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerBridgeDocumentationTests.GmLauncherScript_DoesNotContainStaleWorktreePaths|GmWorkerBridgeDocumentationTests.GmLauncherGenerator_AcceptsExplicitGameSessionPathForSandboxedRuns|GmWorkerBridgeDocumentationTests.ActiveWorkerGuidance_DoesNotAdvertiseDeprecatedGeminiCli|GmBridgePasteVisibilityPolicyTests.IsPromptVisible_DefaultConfig_DoesNotAcceptDeprecatedGeminiPasteMarker|GmBridgePasteVisibilityPolicyTests.IsPromptVisible_DefaultConfig_AcceptsCodexLargePasteMarker|StateManagerTests.EnsureSettingsFileExistsAsync_CreatesConfigWithCurrentDefaults_WhenMissing"` passed 6, failed 0.
- Reviewer-fix final focused GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "RepositoryEncodingScannerTests|GmWorkerBridgeDocumentationTests|GmBridgePasteVisibilityPolicyTests|GmWorkerProfileTemplateTests|GmWorkerBridgeLifecycleTests|StateManagerTests|FileSystemExampleFixtureIntegrityTests|AgentConsoleObservationTests|MortalCommandDisplaySaveTests"` passed 165, failed 0.
- Reviewer-fix final GM/QTE/afterlife/docs GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridge|GmWorkerBridge|GmWorker|PasteVisibility|StateManagerTests|Qte|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests|ExplorerModeCommandTests.Afterlife|ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"` passed 762, failed 0.
- Reviewer-fix final build GREEN: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- Reviewer-fix final non-built-frontend C# suite GREEN: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"` passed 4663, failed 0.
- Reviewer-fix final diff hygiene: `git diff --check` exited 0 with only Git CRLF conversion warnings; `git diff --stat` showed 53 tracked files changed before adding docs/spec artifacts.
