# Final Sol/Max Review Findings

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500

Exact comparison base: `9a1490146b7cecad6101af1f166bde614050a6a3`

## Critical

### 1. `/incarnate` mutates an unbound replacement session

`GameEngine.MainMenu.cs` creates the rollback snapshot and clears/writes setup
state before the durable session generation is bound. A concurrent Load can
replace G1 with G2 before `ProcessPlayerTurn` captures a generation, causing
the old flow to mutate G2 and accept it as its own generation. Snapshot
capture/restore also performs separate unguarded reads and writes.

Required remediation:

- bind one immutable session operation before the first canonical read or
  staging operation;
- carry the same token through `ProcessPlayerTurn`;
- make snapshot capture and restore generation-bound and atomic;
- add deterministic Load barriers around every exposed boundary.

## Important

### 1. Failed or timed-out worker proposals remain applyable

`GmWorkerBridgePool` imports and returns proposals after timeout or non-zero
exit, while `GmWorkerValidationRepairDelegator` applies any non-null proposal.
Timeout, cancellation, non-zero exit, or host failure must make output
diagnostic-only. A workspace must not be imported when process termination is
not confirmed.

### 2. Load leaves the game loop on the old session ID and turn

Browser Load replaces canonical state, but runtime refresh does not call
`_gameLoop.SetSession` or reset session-local response, image, pending, and
transition state. The next turn can address G2 with G1 identifiers.

Required remediation:

- centralize runtime rebinding after replacement;
- derive and set the replacement session and turn;
- clear session-local transient state;
- return to the menu when the replacement has no active game session.

### 3. Rollback cleanup can permanently poison canonical writers

Recovery deletes the transaction root before deleting the active journal. If
journal deletion fails after root deletion, subsequent recovery sees an
uncommitted journal whose manifest no longer exists.

Required remediation:

- persist a durable rolled-back phase or use a cleanup order that remains
  recoverable after partial cleanup;
- add fault-injection coverage for successful root deletion followed by
  journal deletion failure.

### 4. Fresh Mortal bootstrap bypasses materialization and is setting-specific

`MortalBootstrapStateBuilder` infers mechanics and actors from prose keywords,
creates a genre-specific teacher with incomplete actor data, and writes it
before the baseline snapshot. This makes the new actor appear legacy and
exempt from the materialization envelope.

Required remediation:

- remove prose-driven mechanical generation;
- consume explicit structured authority only;
- emit a complete setting-neutral client-owned envelope when the client owns
  bootstrap materialization;
- ensure the pre-turn baseline cannot grandfather actors created during the
  same bootstrap;
- make source guards catch helper-based keyword inference such as
  `ContainsAny`.

### 5. Worker runtime override may be inside replaceable canonical state

`BOE_WORKER_RUNTIME_BASE_PATH` currently accepts any absolute path, including
`game_session` and aliases through junctions or symlinks.

Required remediation:

- reject a runtime root equal to or nested under replaceable/canonical roots;
- resolve and reject reparse-point aliases before creating a workspace.

### 6. Terminal polling has a verify/read TOCTOU window

Generation is checked under a lease, then the lease is released before ready
files are read. After a `Completed` result, terminal artifacts are read without
an immediate durable generation check.

Required remediation:

- inspect terminal signals in the same generation lease, or verify the
  generation immediately after completion and before any terminal artifact
  read;
- add a deterministic Load barrier in the exact verify/read window.
