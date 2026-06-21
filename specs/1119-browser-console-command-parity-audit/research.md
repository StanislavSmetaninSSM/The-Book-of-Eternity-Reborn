# Research: Browser Console Command Parity Audit

## Decision: Browser command coverage is the command inventory authority

**Rationale**: `BrowserCommandCoverageService` feeds `/api/explorer/command-coverage` and already carries migration/audit metadata that ordinary command help does not expose.

**Alternatives considered**: Console help parsing and manual command lists were rejected because they can omit browser-only blocked/advanced-only decisions.

## Decision: Audit semantic parity instead of pixel-perfect console rendering

**Rationale**: Browser UI should use browser-native layout, but must not lose player-facing data that the console exposes.

**Alternatives considered**: Rendering Spectre output or ANSI snapshots in the browser was rejected by #1118 non-goals.

## Decision: Protect audit completeness with a source guard

**Rationale**: A markdown audit can become stale quickly if new commands are added. A focused source guard makes missing command rows visible in normal verification.

**Alternatives considered**: Manual review only was rejected because #1119 acceptance requires no unclassified browser-executable player-facing command.
