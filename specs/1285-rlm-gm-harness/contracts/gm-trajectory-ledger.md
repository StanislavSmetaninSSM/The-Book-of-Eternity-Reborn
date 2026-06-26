# Contract: GM Trajectory Ledger

## Purpose

Provide a compact session-owned record of what happened during a GM turn, repair loop, worker delegation, or rollback-sensitive flow.

## Required properties

- Records are written outside repo source artifacts, under the active game session/control/audit area.
- Records are append-friendly and survive interruption.
- Records reference detailed logs by relative path/hash instead of embedding full transcripts.
- Records are safe for later lesson extraction.

## Minimum record shape

```json
{
  "recordId": "gmtraj_...",
  "kind": "turn|repair|worker|rollback|terminal",
  "sessionId": "session_...",
  "turnId": "turn_or_request_id",
  "realm": "MortalWorld|ChaosSea|ShiningAbode|Unknown",
  "mode": "ordinary|validation_repair|terminal_protocol|worker_proposal",
  "contextPackPath": "game_state/control/gm_context_pack",
  "templateVersions": {
    "turnOutput": "v1"
  },
  "dispatch": {
    "attempts": 1,
    "busyRetries": 0,
    "timeout": false,
    "status": "completed"
  },
  "validation": {
    "status": "accepted|rejected|not_run",
    "issueKinds": [],
    "repairPacketRefs": []
  },
  "repair": {
    "attempts": 0,
    "status": "none|completed|failed|interrupted"
  },
  "workerEvents": [],
  "rollbackEvents": [],
  "rubric": {
    "validTurn": true,
    "playerFacingOutputPresent": true,
    "implementationSourceRead": false,
    "rawWrongRealmWrite": false,
    "manualReasoningNeeded": false,
    "missingHarnessTool": null
  },
  "createdAt": "2026-06-26T00:00:00Z"
}
```

## Non-goals

- This is not a replacement for detailed logs.
- This is not a security boundary.
- This is not a player-facing artifact.
