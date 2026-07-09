# Console Live Chaos Sea #1249 - 2026-06-27

Issue: #1249  
Spec: `specs/1285-rlm-gm-harness/`

## Runs

### Run 1

Run root: `C:\Temp\boe-chaos-console-1249-20260627-181715`

Result:
- Three ordinary Chaos Sea turns were accepted by the live Codex GM bridge.
- Turn durations were high: roughly 190-416 seconds on the previously pinned Codex model at `xhigh`.
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
- The harness is now capable of completing this Chaos Sea travel flow without manual repair. The remaining concern is turn latency on the previously pinned Codex model at `xhigh`, not validator livelock.

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

### Run 7

Run root: `C:\Temp\boe-chaos-console-1249-live-20260628-193340`

Scope:
- Live Chaos Sea play with the then-current Codex GM bridge at `xhigh` through Agent Console.
- Player-facing check of `/хранители` detail.
- Two ordinary roleplay turns inside an active afterlife spiritual conflict.

Result:
- `/хранители` detail initially leaked `Полный JSON Хранителя` plus raw `guardianId` / `abodeId` internals. This was fixed by removing the player-facing JSON audit panel and adding a regression test.
- First roleplay turn completed after validation repair and returned to `Ваш ход`.
- Second roleplay turn entered a repeated validation repair loop around `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict.exchangeLog[2].actionCostAudit.*.before`.
- The live `validation_repair_request.json` had `revalidationAttempt = 4`, `afterlife_conflict_action_cost_sequence_mismatch`, and `harnessRepairPackets: []`, so the GM had only generic repair prose and kept trying incompatible arithmetic fixes.

Harness/RLM finding:
- This is a harness feedback case, not a prompt-only issue. The validator knew the exact fields and expected/actual values, but the repair request did not expose them as an executable repair packet.
- Added `afterlife_spiritual_conflict_action_cost_repair` packets for afterlife spiritual conflict action-cost/action-economy/dice/strain repair errors. The packet names the target conflict file, lists exact `exchangeLog[n].actionCostAudit.<side>.<field>` expected/actual values, tells the GM to recompute dependent `after` and final `activeConflict.actionEconomy.<side>.current`, and forbids new exchanges, rerolls, and pending snapshot edits.

### Run 8

Run root: `C:\Temp\boe-chaos-console-1249-high-20260628-213344`

Scope:
- Live Chaos Sea play with the then-current Codex GM bridge launched with an explicit model and `high` reasoning effort.
- Verify the bridge can run with explicit model/reasoning settings instead of the previous implicit `xhigh` default.
- Re-test an ordinary spiritual-conflict roleplay turn after the action-cost repair-packet change.

Result:
- Bridge diagnostics showed the Codex CLI screen with the then-current model pinned at `high`.
- Codex still surfaced the trust-directory/update startup screen, but the bridge boot guards waited until it reached the normal prompt before marking the bridge ready.
- The daemon sent turn #4 at 21:36:24 and the live GM completed it in 268.3 seconds.
- `game_state/control/gm_trajectory_ledger.jsonl` recorded `validation.status: accepted`, `repair.attempts: 0`, `terminal.kind: success`, `rubric.validTurn: true`, and `rubric.manualReasoningNeeded: false`.
- The client returned to `screenId=game-loop`, `title=Ваш ход`, `awaitingInput=true`.
- Player-facing narrative was present and no `validation_repair_request.json` remained.

Harness/RLM finding:
- An explicit model at `high` was a better default for those live tests than relying on the Codex CLI implicit `xhigh` configuration: this run stayed valid while reducing latency against earlier `xhigh` Chaos Sea runs that were roughly 353-416 seconds.
- Keeping the model and reasoning effort in launch commands makes live-test results reproducible and matches the Codex configuration contract where `model` and `model_reasoning_effort` are separate settings.

## Player-Facing / UX Findings

- Fixed during Run 6: `/обители` no longer leaks `guardianId`, `abodeId`, `Memory`, or `Passage`.
- Fixed during Run 7: `/хранители` detail no longer shows `Полный JSON Хранителя` or raw Guardian/Abode ids in normal player output.
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
- Validation repair now emits an executable `afterlife_spiritual_conflict_action_cost_repair` harness packet for afterlife action-cost/action-economy repair loops.
- Main GM and hidden worker Codex launch defaults now pin their model and `high` reasoning explicitly instead of inheriting the CLI's implicit reasoning setting.
- Worker launch-command parsing now preserves escaped quotes (`\"`) through both the C# worker pool and the PowerShell worker runner, so nested Codex config arguments such as `model_reasoning_effort="high"` reach the worker process intact.

## Follow-Up

- Continue broader live testing from a clean runroot using the updated runbook.
- Add a reusable Agent Console output-leak scanner for live tests instead of keeping the pattern list as an ad-hoc script.
- Continue comparing live-test latency and correctness with the explicit `high` default; if `high` still stalls on larger turns, reduce task-packet size before changing prompts.
