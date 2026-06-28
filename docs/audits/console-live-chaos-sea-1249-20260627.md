# Console Live Chaos Sea #1249 - 2026-06-27

Issue: #1249  
Spec: `specs/1285-rlm-gm-harness/`

## Runs

### Run 1

Run root: `C:\Temp\boe-chaos-console-1249-20260627-181715`

Result:
- Three ordinary Chaos Sea turns were accepted by the live Codex GM bridge.
- Turn durations were high: roughly 190-416 seconds on `gpt-5.5 xhigh`.
- After Chaos Sea abode travel, the client wrote a diagnostic-only repair request and previously stayed in a non-playable waiting state.

Harness finding:
- Diagnostic-only repair is client-owned. The daemon must not dispatch it to the GM, and the client must not wait for `validation_repair_ready.json`.

### Run 2

Run root: `C:\Temp\boe-chaos-console-1249-fix-20260627-194044`

Result:
- Reproduced `/обители` -> `Гранитная Пристань` -> confirm travel.
- First attempt found a runbook/preflight problem: the disposable base path lacked `system_guardians`, so validation reported an unknown Eternal Guardian preset `azalia`.
- After copying `system_guardians` next to `game_session`, the travel turn reached the GM bridge.
- Daemon log:
  - turn #4 sent at 20:05:31
  - live GM completed in 406.6 seconds
  - diagnostic-only repair request detected at 20:12:15 and skipped by daemon
- Client returned to the normal game prompt instead of hanging.

Harness finding:
- The fail-closed client path worked, but the first implementation wrote `validation_diagnostic_failure_report.json` before rollback, so rollback erased the report. This was fixed by writing the report after rollback.
- Contract-error screens also looked like `inputKind:none` to Agent Console until manually submitting `/api/agent-console/key`. This was fixed by publishing a key-input Agent Console snapshot for contract validation errors.

### Run 3

Run root: `C:\Temp\boe-chaos-console-1249-live-20260627-203247`

Result:
- Repeated `/обители` -> `Гранитная Пристань` with `system_guardians` present.
- The GM completed the turn, but the diagnostic-only failure report showed guardian authority mismatch:
  - `guardian_materialized_state_outside_authority`
  - `guardian_scope_stale_active_guardian_alias`
  - expected pre-turn Guardian `Азалия`, actual travel target Guardian `Серет`

Harness finding:
- The special `[CHAOS_SEA_TRAVEL]` validator already allows moving to a discovered abode, but the guardian policy kernel still rebuilt authority from the pre-turn active Guardian. The authority kernel needs to project the accepted travel target into same-turn authority.

### Run 4

Run root: `C:\Temp\boe-chaos-console-1249-live-20260627-212042`

Result:
- After projecting the Chaos Sea travel target into guardian authority, the same live turn reached helper completion.
- `Complete-BoeTurn` then failed with a wrong-realm raw mutation error listing `game_state/world/*.json.rollback.*` and `game_state/npcs/*.json.rollback.*` files.

Harness finding:
- The afterlife wrong-realm scanner correctly blocks Mortal World profile mutations, but it treated rollback backup artifacts as newly created canonical profile files. These files are harness artifacts and must be ignored by the raw profile scanner without weakening canonical file checks.

### Run 5

Run root: `C:\Temp\boe-chaos-console-1249-live-20260627-214035`

Result:
- Repeated `/обители` -> `Гранитная Пристань` with the live Codex GM bridge.
- Daemon sent turn #4 at 21:42:23 and completed it in 353.1 seconds.
- No `validation_repair_request.json` or `validation_diagnostic_failure_report.json` remained.
- The client returned to `screenId=game-loop`, `title=Ваш ход`, `awaitingInput=true`.
- Canonical state now has `activeGuardian.guardianId = guardian_seret` and `chaosSeaNavigation.currentAbodeId = abode_seret`.
- `game_state/control/gm_trajectory_ledger.jsonl` recorded `validation.status: accepted`, `terminal.kind: success`, `rubric.validTurn: true`, `rawWrongRealmWrite: false`, and `manualReasoningNeeded: false`.

Harness finding:
- The harness is now capable of completing this Chaos Sea travel flow without manual repair. The remaining concern is turn latency on `gpt-5.5 xhigh`, not validator livelock.

### Run 6

Run root: `C:\Temp\boe-chaos-console-1249-acceptance-20260628-184535`

Scope:
- Agent Console command sweep on `chaos_sea_command_display_fixture.zip`.
- Commands checked: `/status`, `/душа`, `/перья`, `/хранители`, `/обители`, `/проекты_хранителей`, `/политика_хранителей`, `/обитатели_обители`, `/профили_загробья`, `/хроники_посмертия`, `/уведомления_загробья`, `/архив_души`, `/реликвии`, `/духовные_искусства`, `/духовный_конфликт`, `/журнал_духовного_боя`.
- Snapshot evidence: `C:\Temp\boe-chaos-console-1249-acceptance-20260628-184535\command-smoke-results-after-fix2.json`.

Result:
- No hangs or dead-end menus.
- All commands returned to the normal `Ваш ход` prompt through Agent Console.
- Technical-pattern scan returned `technicalHits: []` for every command above.
- Regression checks also confirmed no visible `guardian_azalia`, `abode_azalia`, `notificationId`, `requestId`, `type=`, `guardianId`, `abodeId`, `Memory`, `Passage`, `.json`, `currentRealm`, `conflictId`, `sideModel`, `resolutionState`, `exchangeLog`, `progressionControl`, or `payload` in the previously problematic player-facing snapshots.

Harness/RLM finding:
- The command sweep is now useful as a standing live-test harness: it catches player-facing engineering leaks in real Agent Console snapshots, not just unit-level rendered strings.
- The next useful increment is to promote this ad-hoc pattern scan into a reusable checked runner so future live tests fail fast when a normal player screen leaks internal ids, JSON filenames, raw enum values, or repair-contract terminology.

## Player-Facing / UX Findings

- Fixed during Run 6: `/обители` no longer leaks `guardianId`, `abodeId`, `Memory`, or `Passage`.
- Fixed during Run 6: `/status`, `/душа`, and `/хранители` no longer leak common player-facing afterlife technical fields in the tested Chaos Sea fixture.
- Remaining known polish item from earlier run: abode transition preview should avoid English boolean text such as `Уже открыта игроком: true` if it still appears in the travel confirmation flow.
- Earlier live travel failed validation due afterlife guardian scope issues:
  - `guardian_materialized_state_outside_authority`
  - `guardian_scope_stale_active_guardian_alias`
- The authority projection fix removed this blocker in Run 5.

## Harness Changes Made

- Daemon skips diagnostic-only validation repair requests instead of sending them back to the GM.
- Client diagnostic-only repair now fails closed before waiting for GM repair.
- Diagnostic failure report is preserved after rollback.
- Contract validation error pause is exposed to Agent Console as key input.
- Live runbook now copies `system_guardians` next to disposable `game_session`.
- Guardian policy authority now projects the authorized Chaos Sea travel target into the same-turn authority root.
- The helper wrong-realm raw profile scanner ignores `.rollback.*` backup artifacts while preserving canonical Mortal World profile protections.
- Afterlife player command output now hides common canonical field names and notification ids in normal screens; technical details remain available through explicit audit/debug surfaces.

## Follow-Up

- Continue broader live testing from a clean runroot using the updated runbook.
- Add a reusable Agent Console output-leak scanner for live tests instead of keeping the pattern list as an ad-hoc script.
- Investigate turn latency and whether the GM bridge needs a faster default reasoning mode or a smaller task packet.
