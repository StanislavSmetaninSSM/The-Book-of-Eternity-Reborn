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

