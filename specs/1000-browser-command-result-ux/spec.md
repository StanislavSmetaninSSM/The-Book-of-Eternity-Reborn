# Feature Specification: Browser Command Result UX Audit and Fixes

**Feature Branch**: `1000-browser-command-result-ux` *(spec path only; current worktree is dirty, so branch switching was intentionally avoided)*

**Created**: 2026-06-17

**Status**: Draft

**Input**: User description: "Audit the browser client because console command data is detailed and useful, while browser command data is technically rendered but useless for players. Use Browser Act, understand what is wrong, create tracked tasks, label active tasks `codex-agent in-progress`, and start fixing."

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1087 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1087
- **Issue type**: audit / bug / UX hardening
- **Spec Kit justification**: The work changes player-facing browser UX, console/browser parity expectations, and command-result presentation across multiple commands.
- **Contract scope**: player-facing, browser, frontend, console parity; no GM-authored state contract changes are planned for the first fix.
- **Out of scope**: Changing canonical game-state schemas, GM prompts, or command semantics. If a command lacks canonical detail data, create a follow-up issue instead of inventing browser-only data.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read Command Details Without JSON (Priority: P1)

A player using the browser client can run a common command and see meaningful in-world details without opening raw JSON or interpreting technical keys.

**Why this priority**: This is the current playability blocker. The browser technically renders data, but important commands expose counts, raw payloads, or technical labels instead of useful player-facing information.

**Independent Test**: Execute affected commands through the browser command input and verify that the first visible result contains readable Russian player-facing data, not API/debug terms.

**Acceptance Scenarios**:

1. **Given** a mortal-world session with rich faction data, **When** the player opens a faction detail from `/factions` or `/фракции`, **Then** the result shows description, reputation, level, resources, ranks, or relationships using readable labels.
2. **Given** a command result includes implementation fields such as image prompts, color tokens, booleans, or raw field names, **When** default browser mode renders it, **Then** those implementation details are hidden or translated into player-facing text.

---

### User Story 2 - Preserve Console/Browser Parity (Priority: P2)

A player can use the browser client as a first-class client and receive the same depth of actionable information that the console client already exposes.

**Why this priority**: The console client is the current quality bar; browser output should not require JSON literacy or external inspection.

**Independent Test**: Compare console and browser output for the same seeded commands and confirm the browser exposes equivalent detail through cards, panels, tables, drilldowns, or readable sections.

**Acceptance Scenarios**:

1. **Given** an inventory item has description, bonuses, structured bonuses, combat effects, or document text, **When** the player inspects inventory in the browser, **Then** the browser provides a readable route to that item detail.
2. **Given** NPC data includes thoughts, personal quests, skills, and relationships, **When** the player opens NPC command output in the browser, **Then** summary and detail navigation are both available without burying the player in counts.

---

### User Story 3 - Keep Advanced Data Advanced (Priority: P3)

Advanced/raw data remains available for debugging, but it does not pollute normal player mode.

**Why this priority**: Debug access is useful for development but harmful when it is the default player experience.

**Independent Test**: Run commands with advanced mode off and on; verify raw JSON and API/debug labels only appear in advanced mode.

**Acceptance Scenarios**:

1. **Given** advanced mode is disabled, **When** the player views command results, **Then** raw JSON blocks and technical field names are not visible.
2. **Given** advanced mode is enabled, **When** the developer inspects the same result, **Then** raw data can still be accessed for debugging.

### User Story 4 - Systematic Default Command Hygiene (Priority: P1)

A player can run any browser-executable command from the default player command surface and receive either useful output, a guided form, or a clear in-world block message without raw JSON, file paths, DTO/API/protocol wording, or English implementation labels.

**Why this priority**: Fixing one command is not enough; browser playability requires a broad safety net across the command catalog.

**Independent Test**: Enumerate browser-executable `player-default` commands from `ExplorerCommandCatalog` and assert default output for 50 read-only commands and 48 local-turn commands is player-facing.

**Acceptance Scenarios**:

1. **Given** a browser-executable read-only command, **When** default browser mode renders it, **Then** the output is completed and contains readable player-facing blocks with no raw JSON or technical markers.
2. **Given** a browser-executable local-turn command, **When** default browser mode renders it, **Then** the output either shows a form, pending status, completed status, or clean blocked status, all without JSON/path/DTO/API/protocol leakage.

### Edge Cases

- Detail payloads may contain partial or missing data; browser rendering must degrade to concise "no data" copy, not raw object dumps.
- Some canonical fields are English or machine-oriented; default browser output must translate or suppress them.
- Summary rows can be useful, but they must not consume the primary viewport while hiding actual player-facing detail.
- Browser fixes must not remove console detail or change canonical game-state authority.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The browser client MUST render default command results as readable player-facing UI, not raw or semi-raw object descriptions.
- **FR-002**: The browser client MUST hide raw JSON and implementation/debug terms in default mode.
- **FR-003**: The browser client MUST provide readable detail paths for entity-rich commands such as factions, NPCs, inventory, books, effects, and afterlife data when canonical details exist.
- **FR-004**: The browser output MUST preserve or improve parity with console detail depth for the same command and state data.
- **FR-005**: Automated regression tests MUST cover each fixed command-result presentation defect before production code changes.
- **FR-006**: Browser Act evidence MUST be captured for at least one before/after command flow fixed by this work.
- **FR-007**: Automated tests MUST enumerate browser-executable player-default commands and reject default output containing raw JSON, state file paths, DTO/API/protocol wording, unlocalized `Realm` labels, or generic `detail` labels.

### Key Entities

- **Command Result Block**: Structured UI block returned by the local web command API and rendered by the browser client.
- **Entity Detail Payload**: Canonical JSON for an entity such as faction, NPC, item, book, effect, or afterlife record.
- **Player-Facing Projection**: Curated representation of canonical data using Russian labels and in-world copy.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The fixed command detail output has no default-mode occurrences of raw JSON, `image_prompt`, technical color tokens, or repeated generic `detail` labels.
- **SC-002**: At least one focused regression test fails before implementation and passes after implementation for every fixed defect.
- **SC-003**: Browser Act screenshots and markdown extraction show player-facing readable detail for the fixed command flow.
- **SC-004**: The implementation does not require changes to canonical state schemas or GM-authored contracts for the first fix.
- **SC-005**: Default-mode command hygiene gates pass for at least 50 read-only commands and 48 local-turn commands.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExplorerWebCommandServiceTests"`
- **Documentation/contract verification**: N/A for client-owned presentation fixes unless later code changes alter GM-authored data contracts.
- **Frontend verification**: Browser Act audit against `http://127.0.0.1:5173/`; run frontend verification only if React/CSS files are changed.
- **Manual/player-facing verification**: Run `/фракции`, open a faction detail, then inspect `/статус`, `/инв`, `/gacha`, and `/craft` for remaining follow-up work.

## Assumptions

- The console client remains the practical parity reference for player-facing command detail depth.
- The first implementation slice should fix the most severe observed browser defect rather than redesign every command result at once.
- The current dirty worktree contains unrelated user/agent work and must not be reverted.
