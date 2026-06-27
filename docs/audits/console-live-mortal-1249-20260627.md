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

## Next Run

Repeat a short Mortal World Agent Console live turn after rebuilding/running the
current harness changes. Expected evidence:
- No false accepted ordinary-turn trajectory when validation repair follows.
- Rejected repair attempts do not appear in `GM_EXPERIENCE_LESSONS` as accepted
  guidance.
- Playable prompt returns with fewer manual harness ambiguities recorded.
