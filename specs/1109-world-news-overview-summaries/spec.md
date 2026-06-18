# Feature Specification: World News Overview Summaries

**Feature Branch**: `fix/1109-world-news-overview-summaries`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "При вводе команды новостей, краткая сводка должна быть полезной - название, краткое описание и т.д. чтобы пользователь знал, что он выбирает. При выборе новости, данные должны быть абсолютно подробны"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1109](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1109)
- **Issue type**: bug / UX hardening
- **Spec Kit justification**: Player-facing console/browser command output and shared DTO behavior are affected; the change tightens summary/detail authority for a command.
- **Contract scope**: player-facing, console, browser, shared command DTO
- **Out of scope**: GM prompts, state schema, validators, and examples. Existing GM-authored fields are reused; no new authoring contract is introduced.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose From Useful Summary (Priority: P1)

A player runs `/новости_мира` and sees a concise but useful list of current world news entries with titles and short context, rather than only section counters.

**Why this priority**: Without useful overview rows the player cannot decide what to inspect.

**Independent Test**: Execute `/новости_мира` against rich test data and verify event, flag, and progression titles plus short descriptions are visible without raw JSON or technical paths.

**Acceptance Scenarios**:

1. **Given** world events, flags, and progression records exist, **When** the player runs `/новости_мира`, **Then** the output includes each selectable entry's title and a short player-facing summary/context.
2. **Given** several selectable records exist, **When** the action selector opens, **Then** choices contain enough title/context to distinguish records.

---

### User Story 2 - Inspect Complete Detail (Priority: P2)

A player selects a world news entry and sees all meaningful player-facing fields for that entry.

**Why this priority**: The overview is only useful if it leads to complete details.

**Independent Test**: Execute detail commands for event, flag, and progression records and verify scalar, array, and nested object fields appear while technical fields stay hidden.

**Acceptance Scenarios**:

1. **Given** a selected event has extra scalar, array, and nested fields, **When** the player opens the detail, **Then** those fields are displayed with readable labels.
2. **Given** a selected record has technical ids/raw/debug/path/url fields, **When** the player opens the detail, **Then** those fields are not shown as player-facing content.

### Edge Cases

- Empty sections should still report empty/damaged state clearly.
- Long descriptions should remain concise in overview and full in detail.
- Technical identifiers and file paths must remain hidden in both overview and detail.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/новости_мира` overview MUST include a per-entry summary for world events, world flags, and world progression records.
- **FR-002**: Each overview row MUST include a player-facing title and a concise summary/context when the source data contains one.
- **FR-003**: Overview action labels MUST contain enough title/context for a player to distinguish selectable records.
- **FR-004**: Detail views MUST show all meaningful non-technical player-facing fields, including scalar values, arrays, and nested object values.
- **FR-005**: Overview and detail views MUST NOT expose raw JSON blocks, debug fields, technical ids, or file/path/url fields.
- **FR-006**: Console navigation back from detail to overview selector MUST remain available.

### Key Entities

- **World Event**: A current or historical world occurrence with title, time/location/status, summary, description, participants, consequences, and extra GM-authored facts.
- **World Flag**: A persistent world-state flag with display name, scope, state/value, description, consequence, and extra facts.
- **World Progression Record**: A tracker/progression entry with title, stage/status, description, reason, consequence, next step, and extra facts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player can identify every selectable news entry from `/новости_мира` output without opening detail first.
- **SC-002**: Detail output for seeded rich records includes at least one extra scalar, one extra array value, and one nested object value where present.
- **SC-003**: Focused automated tests cover overview summaries, detail depth, technical-field filtering, and console back navigation.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true -p:UseSharedCompilation=false --filter "WorldNewsOverview|WorldNewsEventDetail|WorldNewsFlagDetail|WorldNewsProgressionDetail|WorldNews_ConsoleSelection" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: N/A; client-owned rendering over existing state.
- **Frontend verification**: N/A unless shared DTO changes require frontend tests.
- **Manual/player-facing verification**: Run `/новости_мира`, inspect concise entry rows, open an entry, and return to the list.

## Assumptions

- Existing world news JSON files already contain enough title/summary/description fields for the client to render useful summaries.
- No new GM-authored field names are required for this fix.
- Shared command DTO changes should benefit both console and browser surfaces that consume the command result.
