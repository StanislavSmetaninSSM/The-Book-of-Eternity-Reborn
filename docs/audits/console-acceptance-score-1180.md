# Console Acceptance Score #1180

Tracked issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1180

Scope: console client polish pass 2 for non-browser, non-QTE surfaces.

Branch: `work/1174-console-polish-9`

Date: 2026-06-20

## Score

Current console-client score: **8/10**.

Rationale:

- Ordinary Mortal World command rendering is stable enough for play and inspection.
- The dry command sweep rejects raw JSON, file paths, DTO/API wording, contract markers, `debug`, and `null` leakage on default player-facing command output.
- The live Codex-GM console run completed two playable turns without terminal hangs.
- Several high-visibility player-facing leaks found in the live run were fixed: raw map realm/current ids, local map HTML path, `[Unknown]` route state, and mod file names.
- The remaining blockers to 9/10 are consistency and testability, not basic terminal rendering.

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
git diff --check
```

Result: exit code 0. Git emitted CRLF normalization warnings only.

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

## Residual Risks

- #1181: accepted GM narrative facts can fail to persist into `/нпс`, `/хроника`, and `/квесты`.
- #1182: Agent Console does not fully expose command drilldowns and `/опции` menu state for autonomous tests.
- #1183: Saref and memory-scene commands need reusable afterlife output coverage.

## 9/10 Conditions

Raise the console score to 9/10 when:

- #1181 is fixed and a live turn that introduces NPCs/events/quests updates the corresponding command surfaces.
- #1182 is fixed enough that an unattended Agent Console test can enter and exit representative drilldowns.
- #1183 or equivalent focused coverage proves Saref and memory-scene command output follows the shared player-facing quality policy.
- A second short live test confirms command outputs are not just non-crashing, but understandable and consistent after multiple GM turns.
