# Mortal World Console Live #1249/#1290 - 2026-06-27

Issue: #1249 / #1290  
Spec: `specs/1285-rlm-gm-harness/`

## Run

Run root: `C:\Temp\boe-rlm-live-turn-20260627-232206`

Setup:
- Disposable copy of the Mortal World test `game_session`.
- `system_guardians` copied next to `game_session`.
- Console client launched through Agent Console.
- Main bridge command: `codex --dangerously-bypass-approvals-and-sandbox`.
- GM daemon timeout: 900 seconds.

Player action:

```text
Осмотреть письмо и печать, сравнить знак с семейным архивом и проверить, не оставил ли ночной посланник следов в комнате.
```

## Result

The run returned to a playable Mortal World prompt after one player turn.

Player-facing result:
- The scene response was readable and continued the letter/seal investigation.
- The prompt exposed four next actions: go to the North Gate, question a trusted servant, inspect the window/cornice, or hide the letter and investigate the archive.
- Agent Console snapshot `snapshot.poll.latest.json` showed `awaitingInput=true`.

Timing:
- First terminal completion took 283.8 seconds.
- Validation then requested three repairs for `location_last_events_timestamp_invalid`.
- Playable prompt returned at roughly 9.8 minutes from the player action.

## Harness Findings

### False accepted turn trajectory

The first ledger record was:

```text
kind=turn
validation.status=accepted
repair.status=none
durationSeconds=283.8
```

Immediately after it, the daemon processed three correlated repair records for
the same `sessionId/requestId/turnNumber` with issue
`location_last_events_timestamp_invalid`.

Harness conclusion:
- Terminal success is not enough to call a turn accepted for RLM/rubric
  purposes.
- If a correlated `validation_repair_request.json` exists or appears during the
  short post-terminal validation window, the ordinary turn trajectory must be
  marked rejected and must carry the repair issue kinds and repair packet refs.

Implemented follow-up:
- T051 adds correlated repair detection for terminal-success turns.
- The daemon now records such turns as `validation.status=rejected`,
  `repair.status=requested`, and `rubric.validTurn=false`.

### Rejected repairs were promoted as lessons

The live run produced repeated rejected repair attempts. The experience-memory
selection was still based on rejected trajectory records, which makes the next
context pack treat failures as accepted fixes.

Harness conclusion:
- RLM-style lesson memory must learn from accepted prior repair outcomes, not
  from raw rejected repair attempts.
- Failed attempts remain useful audit data, but they should not become
  prescriptive GM guidance.

Implemented follow-up:
- T050 changes lesson selection to require issue kinds plus
  `validation.status=accepted` and an accepted/completed/fixed/success repair
  status.
- `GM_EXPERIENCE_LESSONS` now states that lessons come only from accepted prior
  repair outcomes and remain subordinate to validators/templates.

### Repair friction

The same timestamp validation issue needed three repair attempts. The final
state became playable, but the loop shows the GM still needs easier repair
packets or stronger generated examples for location timestamp fields.

Follow-up candidate:
- Improve the location timestamp repair packet so it includes exact valid
  examples for `lastEventsDescription` and any related location event fields.

### Bridge readiness friction

Between repair attempts the daemon repeatedly reported that the GM bridge was
not ready for a new prompt while Codex was still working. The retry loop did
eventually dispatch repairs, but it added avoidable delay.

Follow-up candidate:
- Add a live-test launch/runbook option for lower-effort GM model settings or
  a smaller smoke-test prompt, then compare quality and latency.

### Misleading daemon status counter

`gm_daemon_status.json` reported `322 turns` after one player turn. This appears
to be an internal loop/status counter rather than a player-turn counter.

Follow-up candidate:
- Split daemon heartbeat/processing counters from player turn count so live
  audits do not misread daemon status.

## Verification

Automated checks after T050/T051:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~GmTurnHelperContractTests.DaemonContextPack_RendersBoundedRelevantExperienceLessons|FullyQualifiedName~GmTurnHelperContractTests.DaemonTrajectoryLedger_MarksTurnRejectedWhenCorrelatedRepairRequestExists"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmTurnHelperContractTests|GmBridgeDiagnosticsContractTests"
```

Result:
- 2 targeted tests passed.
- 69 daemon/bridge contract tests passed.

## Post-T050/T051 Live Retest

Run root: `C:\Temp\boe-rlm-live-turn-postfix-20260628-001625`

Player action:

```text
Осмотреть письмо и печать, сверить знак с семейным архивом, а затем проверить окно и подоконник на следы ночного посланника.
```

Confirmed harness improvement:
- The ordinary turn trajectory was recorded with
  `validation.status=rejected`, `repair.status=requested`, and
  `rubric.validTurn=false` when a correlated validation repair request appeared
  after terminal success.
- The record carried `mortal_relevant_actor_missing_persistence`, so T051
  worked in the live path and did not promote the terminal-success file into a
  false accepted turn.

New harness blocker:
- After the repair path, the console client crashed while deleting
  `input/turn_request.json`.
- The error was a transient file-access race: another local process briefly held
  the file while `CleanupAcceptedTurnTerminalArtifactsAsync()` called
  `FileSystemManager.DeleteFile()`.

Implemented follow-up:
- T052 gives `FileSystemManager.DeleteFile()` the same transient retry behavior
  already used by session file reads and atomic writes.
- The regression test holds `input/turn_request.json` open briefly and verifies
  delete waits, succeeds, and removes the file.

Automated checks after T052:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~FileSystemManagerTests.DeleteFile_WhenTargetIsBrieflyLocked_RetriesUntilDeleteSucceeds"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~FileSystemManagerTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmTurnHelperContractTests|GmBridgeDiagnosticsContractTests"
```

Result:
- The new regression test failed before the fix with the same `IOException`
  class seen in the live run.
- 1 targeted delete-lock test passed after the fix.
- 3 `FileSystemManagerTests` passed.
- 69 daemon/bridge contract tests passed.

## Next Run

Repeat a short Mortal World Agent Console live turn after rebuilding/running
T052. Expected evidence:
- No false accepted ordinary-turn trajectory when validation repair follows.
- Rejected repair attempts do not appear in `GM_EXPERIENCE_LESSONS` as accepted
  guidance.
- Accepted-turn cleanup does not crash if bridge/daemon briefly holds
  `input/turn_request.json`.
- Playable prompt returns with fewer manual harness ambiguities recorded.

## T052 Live Verification Run

Run root: `C:\Temp\boe-rlm-live-turn-t052-20260628-010111`

Setup:
- Disposable copy of `FileSystemExample/game_session`.
- `BookOfEternityClient/system_guardians` copied next to `game_session`.
- Console client launched through Agent Console at `http://127.0.0.1:37421`.
- Main bridge command: `codex --dangerously-bypass-approvals-and-sandbox`.
- Daemon launched with `-AutoPaste -TurnTimeout 900`.

Player action:

```text
Осмотреть письмо и печать, сверить знак с семейным архивом, затем проверить окно и подоконник на следы ночного посланника и тихо прислушаться к коридору.
```

Result:
- The turn completed in 364.5 seconds.
- `gm_trajectory_ledger.jsonl` recorded `validation.status=accepted`,
  `repair.attempts=0`, `repair.status=none`, and `rubric.validTurn=true`.
- `input/turn_request.json`, `ready/turn_complete.json`, and the pending-turn
  snapshot files were cleaned; the console client did not crash in accepted-turn
  cleanup after T052.
- Agent Console returned to a playable `game-loop` text prompt for turn 1 with
  three player-facing options.
- A post-turn `/где_я` command returned location details and then returned to
  the game prompt after an Agent Console Enter key.

Player-facing assessment:
- The narrative was readable and suitably grounded: the seal, archive clue,
  window trace, and corridor pause were understandable without debug terms.
- The accepted output persisted the local clue into the location's latest event;
  `/где_я` showed the updated time `08:10` and the turn-1 location event.
- A small console polish gap remains: `/где_я` still exposes the raw location
  type `indoor` in player-facing output. This is not a harness blocker, but it
  belongs in the broader console command polish sweep.

Harness assessment:
- T050/T051/T052 are live-verified for this path: no rejected lesson promotion,
  no false accepted trajectory, and no accepted-turn cleanup crash.
- Codex GM stayed within helper-driven session operations; the ledger recorded
  `implementationSourceRead=false`.
- The remaining recurring cost is latency: one ordinary accepted turn took just
  over six minutes on `gpt-5.5 xhigh`.

Run teardown:
- The bridge shutdown request returned an empty response after closing the
  bridge process.
- The remaining console client and daemon PIDs for this run were stopped
  explicitly after artifact capture.

### Follow-up: daemon launch quoting friction

During setup, the first daemon launch attempt used a hand-rolled
`Start-Process -ArgumentList` array. Windows PowerShell split the repository
path at `E:\Games\The`, so `-File` failed before the daemon started.

Harness conclusion:
- Live-test agents should not have to hand-roll daemon `Start-Process` calls
  for repository paths with spaces.
- This belongs in launcher tooling, not only in prompt/runbook wording.

Implemented follow-up:
- T053 adds `bookofeternity.ps1 start-daemon`.
- The launcher action starts `game_master_daemon.ps1` through an encoded
  PowerShell host command, returns JSON with `daemonPid`, `logFile`, and
  launch settings, and keeps the daemon hidden by default.
- `docs/e2e/gm-workers-live-regression-runbook.md` now uses
  `bookofeternity.ps1 start-daemon` and preserves `daemon.start.json`.

Verification:
- Source/runbook guard tests failed before implementation and passed after it.
- A runtime smoke launched the daemon through `start-daemon --timeout 1`,
  confirmed the returned PID was alive and the log file existed, then stopped
  that daemon process.
