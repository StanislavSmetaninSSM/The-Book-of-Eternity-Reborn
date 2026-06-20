# Console Dry Sweep Report (#1176)

Date: 2026-06-20

Branch: `work/1174-console-polish-9`

Scope: non-QTE console command output over reusable Mortal World, Chaos Sea, and Shining Abode command-display saves.

## Reproducible Sweep Command

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ConsoleCommandOutputQualityClassifierTests|MortalCommandDisplaySaveTests|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests" --logger "console;verbosity=minimal"
```

Observed result:

```text
Passed: 298
Failed: 0
Skipped: 0
Duration: 39 s
```

## What The Sweep Checks

The reusable display-save tests now use `ConsoleCommandOutputQualityClassifier`, a shared test helper that classifies `ExplorerCommandResult` output before console rendering.

Default player-facing output fails the sweep when it:

- returns `Failed` or `Blocked` state;
- has no visible blocks, actions, or prompts;
- includes `UiRawJsonBlock` by default;
- has no readable visible text;
- leaks technical markers such as `game_state/`, `.json`, `DTO`, `API`, `endpoint`, `protocol`, `pending_`, `requestId`, `actionType`, `debug`, `null`, `UiRawJsonBlock`, `JsonObject`, `JsonArray`, or `JsonValue`.

After classification, each result is rendered through `ExplorerCommandResultConsoleRenderer` to catch Spectre.Console rendering/markup exceptions.

## Covered Fixture Sets

| Fixture | Tests | What It Covers |
|---|---:|---|
| Mortal World command display save | Existing parameterized display-save tests | All `MortalWorld` catalog descriptors, practical universal preview commands, world-news enum localization checks. |
| Chaos Sea command display save | Existing parameterized display-save tests | All `ChaosSea` and `AfterlifeCombatAndEntities` catalog descriptors, practical universal preview commands, representative detail targets. |
| Shining Abode command display save | Existing parameterized display-save tests | All `ShiningAbode` and `AfterlifeCombatAndEntities` catalog descriptors, practical universal preview commands, representative detail targets. |
| Classifier unit tests | 5 | Raw JSON, readable output pass case, afterlife contract markers, debug marker, and literal `null` leakage. |

## Current Findings

No blocking dry-sweep failures were found in the reusable Mortal/Chaos/Shining display saves after the classifier consolidation.

Remaining sweep gaps from the matrix:

- Universal commands `math`, `gm`, `debug`, and `system_guardians` are covered by focused/general tests but not all are part of reusable display-save practical preview sets.
- Saref and memory-scene commands are not included in the three reusable display saves and need either a focused fixture sweep or a documented out-of-route note during live E2E.
- LocalTurn commands are treated as preview/prompt surfaces. The current sweep does not attempt to complete mutating actions, which is intentional for non-destructive dry coverage.

## Next Actions

1. Use this classifier as the shared policy for #1177 and #1178 fixes.
2. Add focused dry-sweep tests only when a command surface from the matrix is not covered by existing fixture/general tests and becomes a real blocker for the 9/10 audit.
3. Preserve QTE exclusion. QTE commands and QTE live frames are not part of this report.
