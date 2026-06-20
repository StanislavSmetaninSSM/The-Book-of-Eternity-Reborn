# Research: Console Client Live Player-Readiness Pass

## Decision: Use Existing Agent Console As Player Boundary

**Rationale**: The existing runbooks define Agent Console snapshots, events, text input, and actions as the player-observable surface. This keeps the playtest honest: the player side sees the console UI and not hidden JSON or schemas.

**Alternatives considered**:

- Direct process stdout scraping: less stable and loses structured options/actions.
- Inspecting `game_session` during play: useful for debugging, but violates the player-readiness goal.

## Decision: Use Disposable Seed Session For Every Run

**Rationale**: The playtest must be reproducible and must not mutate the user's working `game_session`. A copied seed plus run metadata allows failures to be replayed or triaged.

**Alternatives considered**:

- Use the current developer session: faster, but risks corrupting working test data.
- Hand-edit a custom scenario before play: useful for targeted tests, but not a neutral player-readiness run.

## Decision: Fix Only Narrow Console Blockers In This Issue

**Rationale**: The live pass can uncover design-level issues. Narrow defects such as malformed markup, wrong lifecycle message, dead-end selector, or obviously leaked internal text can be repaired with tests. Larger redesigns should become follow-up issues with artifacts.

**Alternatives considered**:

- Fix everything found immediately: risks broad unplanned changes and weak verification.
- Audit only, no fixes: wastes the user's requested autonomous time when blockers are small and testable.

## Decision: Keep Browser Work Out Of Scope

**Rationale**: Browser command-output parity is already tracked in GLM-labelled issues. This feature focuses on the console client as the current primary playable client.

**Alternatives considered**:

- Shared renderer changes touching browser: allowed only if a console defect cannot be repaired safely without a shared change; otherwise defer to GLM tasks.
