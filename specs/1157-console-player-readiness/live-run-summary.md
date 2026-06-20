# Console Player-Readiness Live Run Summary

Source issue: #1157 <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1157>

Branch: `1157-console-player-readiness`

## Scope

This pass audits the real console client as player-facing UI. Browser/frontend parity remains out of scope for #1157.

## Live Runs

| Area | Run root | Result |
| --- | --- | --- |
| QTE practice entry before fix | `C:\Temp\boe-console-qte-entry-1157-20260620-134858` | Failed: entering QTE practice from Agent Console hit Spectre selection prompt unsupported by the headless terminal. |
| QTE practice after fix | `C:\Temp\boe-console-qte-entry-1157-fixed-20260620-140120` | Passed: Agent Console exposed `qte-practice-type`, difficulty menu, and chapter prelude snapshots without Spectre prompt failure. |
| Daren QTE after fix | `C:\Temp\boe-console-daren-entry-1157-fixed-20260620-140155` | Passed: Agent Console exposed Daren menu and first chapter prelude snapshots without Spectre prompt failure. |
| Chaos Sea afterlife audit before fix | `C:\Temp\boe-console-afterlife-1157-chaos-20260620-140406` | Failed: `/spiritual_combat_log` exposed file path, raw JSON panel, and internal fields such as `activeConflict.exchangeLog`, `recentConflicts`, `sideModel`. |
| Chaos Sea afterlife audit after fix | `C:\Temp\boe-console-afterlife-fixed-1157-20260620-142220` | Passed: `/spiritual_combat_log` and `/spiritual_combat_help` had no forbidden JSON/file/field markers and localized core labels. |

## Findings

| Surface | Severity | Actual | Action |
| --- | --- | --- | --- |
| Agent Console -> `Тренировка QTE` | P1 | QTE practice used direct Spectre `SelectionPrompt`, which crashed in Agent Console/headless mode before the player could choose a QTE. | Fixed with Agent Console-aware QTE selection snapshots and regression test. |
| Agent Console -> Daren QTE | P1 | Daren showcase start/retry menus used the same direct Spectre prompt path and were not safely controllable through Agent Console. | Fixed with the shared QTE selection helper and live smoke. |
| `/spiritual_combat_log` in Chaos Sea | P2 | Player output exposed state file path, raw JSON, and internal field names. | Fixed old console path output and added player-facing regression assertions. |
| `/spiritual_combat_help` | P2 | Help mixed player instructions with contract names such as `exchangeLog`, `recentConflicts`, `diceAudit`. | Reworded to player-facing Russian terminology and added guard assertions. |
| `/spiritual_arts` | P2 | The command displayed a raw `afterlifeCombatProfile` JSON audit panel after the readable panel. | Removed the raw audit panel and added a regression assertion. |

## Verification Evidence

Red evidence:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~AgentConsoleLiveControl_QtePracticeMenuPublishesSnapshotAndDoesNotUseSpectrePrompt" -p:BaseOutputPath=TestResults\1157-red-qte-agent-console\` failed to reach `qte-practice-type`.
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~TryProcessCommand_SpiritualCombatLog_ShowsExchangeAndRecentConflictAudit"` failed on `afterlife_spiritual_conflict_state.json` before the console output cleanup.

Focused green evidence:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~AgentConsoleLiveControl_QtePracticeMenuPublishesSnapshotAndDoesNotUseSpectrePrompt"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsole|QtePracticeMode|QteLivePlayability|DarenQteShowcase"` passed 175 tests.
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~TryProcessCommand_SpiritualCombatHelp_ExplainsTacticsPositionAndFairCriticals|FullyQualifiedName~TryProcessCommand_SpiritualCombatLog_ShowsExchangeAndRecentConflictAudit|FullyQualifiedName~TryProcessCommand_SpiritualArts_UsesCanonicalShiningRadianceFields"` passed 3 tests.
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AgentConsole|QtePracticeMode|QteLivePlayability|DarenQteShowcase|SpiritualCombat|SpiritualArts|spiritual_combat|spiritual_arts"` passed 211 tests.
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore` passed 4643 tests.

Live green evidence:

- QTE practice live smoke after fix: `qte-practice-type`, `qte-practice-difficulty`, and `qte-chapter-practice-timingbar-easy-practice-challenge` snapshots appeared; no `Cannot show selection prompt` stderr.
- Daren live smoke after fix: `qte-daren-menu` and `qte-chapter-daren-qte-showcase-approach-manor` snapshots appeared; no `Cannot show selection prompt` stderr.
- Chaos Sea afterlife live smoke after fix: `/spiritual_combat_log` and `/spiritual_combat_help` had no hits for `afterlife_spiritual_conflict_state.json`, `Полный JSON`, `activeConflict`, `recentConflicts`, `exchangeLog`, `sideModel`, `playerOutcome`, `diceAudit`, `rewardAudit`, `direct_duel`, `contested`, or `(clear)`.

## Residual Risk

- QTE mini-games now expose menu and chapter prelude snapshots to Agent Console, but the live moving frame itself is still a console-rendered interaction, not a rich per-frame Agent Console model.
- This pass fixed narrow afterlife combat output leaks found during the reachable Chaos Sea audit. Other afterlife commands may still contain legacy JSON audit panels and should be handled by follow-up command-output passes if they appear in live play.
