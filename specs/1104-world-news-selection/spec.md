# Feature Specification: World News Selectable Details

**Feature Branch**: `fix/1104-world-news-selection`

**Created**: 2026-06-18

**Status**: Draft for autonomous implementation

**Input**: User report with screenshots: `/новости_мира` currently renders a long, noisy page with summary tables, raw JSON dumps, and action commands. The desired behavior is a compact summary first, with detailed event/flag/progress records opened through a selector/menu.

## Source Issues & Scope

- **Source GitHub issue(s)**: [#1104](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1104)
- **Issue type**: Bug / player-facing console UX.
- **Spec Kit justification**: This changes a player-facing console command flow and shared console/browser command result contract. It must preserve detail authority while removing debug-style output from the default view.
- **Contract scope**: Player-facing, console, browser command-service parity. No GM-authored state schema, validation, afterlife, or save-format change is intended.
- **Out of scope**: New world-news data fields, GM prompt changes, browser visual redesign beyond shared command-result content, and changes to mortal world state files.

## User Scenarios & Testing

### User Story 1 - Read a Compact World Summary (Priority: P1)

As a player, I run `/новости_мира` and see only the high-level state of the world plus clear choices for what I can inspect next.

**Why this priority**: The current default output is the reported broken experience: it overwhelms the player before they select anything.

**Independent Test**: Execute `/новости_мира` against rich world-news test data and verify the result contains a summary and selectable actions, but not raw JSON dumps or full event/flag/progression detail tables.

**Acceptance Scenarios**:

1. **Given** world events, flags, and progression exist, **When** the player runs `/новости_мира`, **Then** the output shows a compact summary with counts and a choice/action list for detailed records.
2. **Given** the same state, **When** the player reads the overview, **Then** raw JSON keys such as `worldEventsLog`, `worldStateFlags`, and `updateWorldProgressionTracker` are not visible.
3. **Given** detail records exist, **When** the overview returns actions, **Then** each action has a player-facing label and command for opening exactly one event, flag, or progress record.

---

### User Story 2 - Inspect One World-News Record (Priority: P1)

As a player, I can select one world event, flag, or progression entry and read its details without scanning unrelated records.

**Why this priority**: The overview only works if the existing drilldowns remain stable and readable.

**Independent Test**: Execute detail commands for one event, one flag, and one progression entry and verify each detail view contains readable fields and no raw JSON/debug leakage.

**Acceptance Scenarios**:

1. **Given** the player selects an event, **When** the detail command runs, **Then** only that event's readable details are shown.
2. **Given** the player selects a flag, **When** the detail command runs, **Then** only that flag's readable details are shown.
3. **Given** the player selects a progression record, **When** the detail command runs, **Then** only that progression record's readable details are shown.

---

### Edge Cases

- If no world-news files exist, the overview should show a concise empty-state message.
- If some world-news files are malformed, the overview should show warnings without dumping raw JSON.
- If a selector is unknown, the detail command should show a player-facing not-found warning and a way back to overview.
- Dynamic GM-authored text must remain escaped/sanitized in console and browser command-result rendering.

## Requirements

### Functional Requirements

- **FR-001**: `/новости_мира` overview MUST render a compact summary of world-news sections and not render raw JSON blocks by default.
- **FR-002**: `/новости_мира` overview MUST expose selectable actions for event, flag, and progression details when those records exist.
- **FR-003**: `/новости_мира` overview MUST NOT render the full event, flag, and progression detail tables in the default view.
- **FR-004**: Detail commands MUST keep existing event, flag, and progression detail behavior and must remain free of raw JSON/debug leakage.
- **FR-005**: Console and browser command-service results MUST preserve semantic parity: the same overview/detail commands and actions are available on both surfaces.
- **FR-006**: Malformed or missing files MUST remain readable as warnings or empty state rather than causing hidden console errors.

### Key Entities

- **World News Overview**: Summary of counts/statuses for mortal world events, location threats, NPC activities, faction projects, world flags, and progression.
- **World News Detail Action**: Player-facing action that opens one canonical detail record by stable selector.
- **World News Detail Record**: One event, flag, or progression entry rendered through existing readable detail panels.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The overview command renders zero `UiRawJsonBlock` blocks when rich world-news data exists.
- **SC-002**: The overview command renders no more than one world-news summary table plus selectable actions for event/flag/progression drilldowns.
- **SC-003**: Existing event, flag, and progression detail commands still pass focused tests and expose readable fields.
- **SC-004**: Focused console and browser world-news command tests pass with zero failures.

## Verification Plan

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Not required; this is client-owned rendering of existing state, with no GM-authored schema or prompt change.
- **Frontend verification**: Not required unless shared DTO shape changes. The intended change is in shared C# command-result content.
- **Manual/player-facing verification**: Run `/новости_мира` on the test game session and confirm the default view is summary-first and details open through choices.

## Assumptions

- The existing `/новости_мира событие|флаг|прогресс <selector>` commands are the canonical detail authority and should be preserved.
- Location threats, NPC activities, and faction projects may remain summary-only in this issue because the user explicitly requested details for news/flags and similar selectable records; additional drilldowns can be tracked separately if needed.
- The existing command action list is the selector mechanism for console/browser parity; no new interactive session protocol is required.
