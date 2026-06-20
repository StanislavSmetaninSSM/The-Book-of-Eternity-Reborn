# Data Model: Console Client Live Player-Readiness Pass

## Playtest Run

- **Purpose**: Records one disposable live test attempt.
- **Fields**: `issue`, `commit`, `runRoot`, `seedSession`, `sandboxSession`, `gmBridgeCommand`, `agentConsoleUrl`, `startedAt`, `endedAt`, `result`.

## Observation Artifact

- **Purpose**: Captures what the player or harness observed.
- **Fields**: `step`, `kind`, `path`, `trigger`, `playerVisible`.

## Console Defect

- **Purpose**: Tracks a player-facing problem found during the pass.
- **Fields**: `severity`, `surface`, `trigger`, `expected`, `actual`, `artifacts`, `resolution`.

## Repair Evidence

- **Purpose**: Proves a fix or validates a no-code outcome.
- **Fields**: `defect`, `redTest`, `fixCommit`, `focusedVerification`, `broadVerification`, `liveRerun`.

## State Transitions

1. Planned -> Running when sandbox, client, bridge, and Agent Console are launched.
2. Running -> Blocked when the harness cannot continue before meaningful player output.
3. Running -> DefectsFound when P0/P1/P2 issues are recorded.
4. DefectsFound -> Repairing when an in-scope fix begins.
5. Repairing -> Verified when tests and affected live steps pass.
6. Running or Verified -> Completed when final issue evidence is posted.
