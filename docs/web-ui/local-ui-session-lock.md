# Local UI Session Lock

Tracked task: #568  
Parent epic: #559

## Purpose

The console UI and the future local browser UI can point at the same `game_session`. Mutating commands must not run concurrently from two UI owners, because they can rewrite pending turns, rollback baselines, economy state, or local afterlife action files.

Read-only commands may still render while another UI owner holds the lock. Mutating commands must acquire or refresh the local UI session lock first.

## Lock File

Path:

```text
game_state/control/local_ui_session_lock.json
```

Shape:

```json
{
  "schemaVersion": 1,
  "ownerId": "console:MACHINE:12345",
  "ownerKind": "console",
  "ownerLabel": "Console PID 12345",
  "acquiredAtUtc": "2026-05-20T10:00:00Z",
  "heartbeatAtUtc": "2026-05-20T10:00:30Z",
  "leaseSeconds": 120,
  "lastOperation": "/spiritual_action"
}
```

## Ownership Rules

- The same owner may refresh the heartbeat before each mutating command.
- A different owner is blocked while `heartbeatAtUtc + leaseSeconds` is still in the future.
- A different owner may replace the lock after the lease expires.
- A malformed lock blocks mutation while the file timestamp is fresh.
- A malformed lock may be replaced after its file timestamp becomes stale.

## Current Console Coverage

`ExplorerMode` gates known mutating slash commands before dispatch. The first browser host must reuse the same service with a browser-specific owner id/label and must not bypass the lock for writes.

The block message is intentionally player-facing and local: it tells the user that another UI session is editing the save and names the lock path for manual inspection if needed.


## Console settings publication and recovery

The console settings menu uses a detached draft and temporary language/font/audio preview. It does not keep the canonical write lease while waiting for input. Esc/Back keeps its existing save behavior; difficulty, QTE and mod choices keep their immediate save points. Each save prepares and publishes config, GM settings projection and system-mod manifest as one declared set through the common journal, retaining generation, exact config baseline, pending-GM and local-owner gates. The console identifies itself with `ownerKind=console`; payload schemas and the existing GM contract are unchanged.

A blocked or rolled-back save retains the draft for editing/retry and displays a Russian notice. A confirmed baseline/generation conflict or uncertain publication requires reconciliation; the menu offers an explicit reread/discard action and cannot silently exit as if saved. Last accepted runtime language/font/audio effects are restored while unresolved, without claiming they describe current disk state. A refused reread preserves the draft and outcome notice. A durable commit followed by cleanup/runtime failure is reported as saved with follow-up, never as rollback; do not delete journal evidence to dismiss it. Ordinary menu exit/discard and exceptional unwinding restore accepted preview effects.

Startup and MainMenu projection synchronization use the same prepared set. An already synchronized set can be confirmed without publication under the canonical lease, including during a pending turn; a required change still obeys the gates. An unsuccessful fresh-game settings boundary stops request/wait continuation. The older accepted-turn health/reminder and generation-rotation paths remain the separate B3 migration scope.
