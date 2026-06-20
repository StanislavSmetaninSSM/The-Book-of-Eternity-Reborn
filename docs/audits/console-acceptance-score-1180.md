# Console Acceptance Score #1180

Tracked issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1180

Scope: console client polish pass 2 for non-browser, non-QTE surfaces.

Branches: original `work/1174-console-polish-9`; final follow-up `work/1174-console-final-sweep`

Date: 2026-06-21

## Score

Current console-client score: **9/10**.

Rationale:

- Ordinary Mortal World command rendering is stable enough for play and inspection.
- The dry command sweep rejects raw JSON, file paths, DTO/API wording, contract markers, `debug`, and `null` leakage on default player-facing command output.
- The original live Codex-GM console run completed two playable turns without terminal hangs.
- Follow-up issues #1181, #1182, and #1183 were fixed and verified: accepted Mortal NPC facts are enforced into player-visible state, Agent Console menu/drilldown snapshots are observable, and Saref/memory-scene output has focused player-facing coverage.
- A second live Codex-GM console run on 2026-06-21 completed a 30-command Mortal World sweep plus two playable GM turns.
- Several high-visibility player-facing leaks found in the live run were fixed: raw map realm/current ids, local map HTML path, `[Unknown]` route state, and mod file names.
- Two additional second-run polish defects were fixed after RED/GREEN tests: journal-only NPCs now expose drilldown actions/details, and empty `/извечные_хранители` no longer shows a local directory/path-shaped table.
- No known blocker or major non-QTE console-flow issue remains after the second run. Remaining risks are polish/test-runtime concerns, not basic playability.

## Verification

Passed:

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore
```

Result: 0 warnings, 0 errors.

Passed:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExplorerCommandResultConsoleRendererTests|TryProcessCommand_MapRussian_InMortalRealm_OpensVisualMapViewerInsteadOfLocationList|TryProcessCommand_Locations_RendersWithoutHiddenErrors|TryProcessCommand_Locations_HidesUnknownAdjacentLinkState|TryProcessCommand_SystemMods_RendersDetailLoopWithoutHiddenErrors|TryProcessCommand_SystemMods_HidesTechnicalFileNamesInPlayerChoices|MortalCommandDisplaySaveTests|ConsoleCommandOutputQualityClassifierTests" --logger "console;verbosity=minimal"
```

Result: 108 passed, 0 failed.

Passed:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"
```

Result: 4680 passed, 0 failed.

Passed:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExecuteAsync_NpcWithRepositoryJournalFixtureAndNoCore_ShowsKnownJournalNotes|ExecuteAsync_NpcJournalFallbackDetail_ShowsFullJournalEntriesAndBackAction|ExecuteAsync_SystemGuardiansWithEmptyLibrary_HidesLocalDirectoryPath"
```

Result: 3 passed, 0 failed.

Passed:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ConsoleCommandOutputQualityClassifierTests|StructuredBonusDisplayTests|MortalCommandDisplaySaveTests|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests|SarefCommandDisplayQualityTests|AgentConsoleRecordingExplorerConsoleTests|AgentConsoleLiveInputSourceTests|AgentConsoleObservationTests|ExplorerWebCommandServiceTests"
```

Result: 710 passed, 0 failed.

Passed:

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore
```

Result: 0 warnings, 0 errors.

Passed:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"
```

Result: 4703 passed, 0 failed.

Passed:

```powershell
git diff --check
```

Result: exit code 0. Git emitted CRLF normalization warnings only. The 2026-06-21 check is recorded in the branch evidence log.

Not counted as console acceptance:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

This full all-tests command initially passed 4679 tests and failed 3:

- 2 `LocalWebUiBuiltFrontendSmokeTests` failed because `BookOfEternityClient.WebFrontend\dist\index.html` was missing. This is a browser built-asset precondition and outside the current console-only scope.
- 1 `ExplorerWebCommandServiceTests` expectation still required raw `lore_research`; the expectation was updated to the localized player-facing label and the focused test passed.

## Live Test Evidence

Report: `docs/audits/console-live-playtest-1179.md`

Run root: `C:\Temp\boe-live-e2e-1179-20260620-223955`

Result:

- 29 ordinary Mortal World commands returned without hangs.
- Two Codex-GM turns completed with readable narrative.
- Location movement was reflected by `/карта` and `/где_я`.
- Follow-up consistency defects were filed for state surfaces that did not reflect accepted narrative facts.

Second run:

- Run root: `C:\Temp\boe-live-e2e-1174-final-20260621-060857`
- Commit under test at launch: `bbb2c2d6373c3c7a01df87209c27a98da8553c9c`
- Agent Console: `http://127.0.0.1:52884`
- 30 Mortal World commands returned without hangs or classifier issue markers.
- Two Codex-GM turns completed; turn 2 required validator repair, then returned to player input.
- New clues introduced through a journal-only NPC were visible through `/нпс`; the follow-up fix added journal detail actions so this state is no longer a dead-end table.
- The live client, daemon, and bridge processes were stopped after artifact capture.

## Residual Risks

- The GM bridge path is playable but slow: bootstrap and turn resolution can take several minutes, and validator repair can add another wait.
- This score excludes browser UI and QTE live-frame quality by design.
- A full start-to-afterlife reward loop was not repeated in the final short run; the final run focused on Mortal World command consistency after #1181-#1183.

## 9/10 Conditions

The 9/10 conditions are met as of this report:

- #1181, #1182, and #1183 are fixed and covered by focused tests.
- The second short live test confirmed ordinary command output remains understandable after multiple GM turns.
- New second-run regressions were covered by RED/GREEN tests and broad non-browser verification.
