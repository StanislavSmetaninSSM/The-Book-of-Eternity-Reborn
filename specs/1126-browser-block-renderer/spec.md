# Feature Specification: Browser Block Renderer Rich Command Output

**Feature Branch**: `work/1126-block-renderer`

**Created**: 2026-06-21

**Status**: Draft for implementation

**Input**: GitHub issue #1126 — "[Task] Improve generic browser BlockRenderer for rich ExplorerCommandResult blocks"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1126 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1126
- **Parent issue**: #1118 — browser command output semantic parity epic.
- **Related prior specs/issues**: #1124 Chaos Sea browser parity, #1125 Shining Abode browser parity, and #1116 browser visual redesign PR.
- **Issue type**: Browser Client player-facing command-result renderer hardening.
- **Spec Kit justification**: Required. The issue changes player-facing browser UX and console/browser semantic parity across many commands.
- **Contract scope**: Existing frontend rendering of typed `UiBlock` DTOs from `ExplorerCommandResult`. No new game state, GM prompt, validator, normalizer, pending/control, or command contract is intended unless a missing DTO field is proven by tests.
- **Out of scope**: Console client work, backend command semantics, new afterlife contracts, new QTE mechanics, and broad visual redesign outside command-result rendering.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read rich command output without raw JSON (Priority: P1)

A browser player opening a command result can read the same information carried by typed UI blocks without needing JSON, internal ids, or debug panels.

**Why this priority**: Recent backend parity work produces richer DTOs; the browser must not throw away that structure.

**Independent Test**: Render representative command-result fixtures containing panels, tables, lists, key-value grids, messages, images/maps, actions, and raw JSON blocks; assert readable hierarchy and hidden diagnostics by default.

**Acceptance Scenarios**:

1. **Given** a command result contains nested player-facing blocks, **When** it renders in the browser, **Then** the hierarchy and labels remain visible.
2. **Given** raw JSON or diagnostics exist, **When** advanced mode is disabled, **Then** the browser hides them from the player-facing view.

---

### User Story 2 - Navigate overview and detail actions clearly (Priority: P1)

A browser player can see actionable buttons for detail routes and return/back actions without confusing them with passive text.

**Why this priority**: Many browser parity fixes now depend on overview-to-detail routes; weak actions make those fixes hard to use.

**Independent Test**: Render command results with secondary, primary, danger, and back actions; assert accessible buttons preserve labels, commands, and visual group structure.

**Acceptance Scenarios**:

1. **Given** an overview command returns detail actions, **When** the view renders, **Then** each action is a clear button with the correct command.
2. **Given** a detail command returns a back action, **When** the view renders, **Then** the back action remains easy to find.

---

### User Story 3 - Keep dense data readable on desktop and mobile (Priority: P2)

A browser player can read long tables, nested data, and wrapped text without horizontal page breakage.

**Why this priority**: Mortal, Chaos Sea, and Shining Abode commands can contain dense tabular data; overflow or flattened cells makes the UI technically present but unusable.

**Independent Test**: Render table-heavy and nested fixtures at component level and verify responsive classes/semantics prevent raw overflow and preserve cell content.

**Acceptance Scenarios**:

1. **Given** a table has long values, **When** rendered, **Then** the table is contained in a scrollable/readable region rather than breaking the page.
2. **Given** a block has nested panels or lists, **When** rendered, **Then** nested sections remain visually grouped.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `BlockRenderer` MUST render every known typed block kind used by browser command results: `text`, `panel`, `table`, `list`, `keyValueGrid`, `message`, `image`, `map`, and raw/diagnostic JSON blocks.
- **FR-002**: Nested panels/lists MUST preserve section hierarchy and must not flatten all content into one weak table.
- **FR-003**: Default rendering MUST hide raw JSON and diagnostics unless `advancedEnabled` is true.
- **FR-004**: Action buttons MUST remain visually clear, accessible, and grouped after overview and detail commands.
- **FR-005**: Long tables and nested content MUST remain readable without page-level horizontal overflow on desktop and mobile.
- **FR-006**: Visual updates MUST stay inside the existing dark-fantasy browser design system from PR #1116 and use existing CSS tokens/classes where practical.
- **FR-007**: If a missing DTO field is proven necessary, the implementation MUST add the smallest backend DTO/test change and document why the frontend-only assumption changed.

### Key Entities

- **ExplorerCommandResult**: Browser-facing command result with title, status, blocks, actions, and optional diagnostics.
- **UiBlock**: Typed C# block DTO consumed by the React renderer.
- **BlockRenderer**: React component responsible for converting typed blocks into player-facing browser UI.
- **Advanced diagnostics**: Raw JSON/debug information visible only when advanced mode is enabled.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Frontend component tests cover representative rich block fixtures and fail before any production renderer changes for uncovered gaps.
- **SC-002**: `npm run verify --prefix BookOfEternityClient.WebFrontend` passes before merge.
- **SC-003**: Browser screenshots are captured for one overview command, one detail action, one nested panel, and one table-heavy command or fixture.
- **SC-004**: Default browser output does not show raw JSON in covered tests unless advanced mode is enabled.
- **SC-005**: Visual changes preserve the current dark-fantasy style and do not reintroduce generic/debug-looking command output.

## Verification Plan *(mandatory)*

- **Audit files**: `BlockRenderer.tsx`, `CommandResultView.tsx`, `src/api/contracts.ts`, `src/styles/components.css`, `blockRenderer.test.ts`, and `commandResultViewSections.test.ts`.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Style verification**: `git diff --check`.
- **Manual/browser verification**: Local browser screenshots for overview/detail/nested/table-heavy command-result output.
- **Backend verification**: Not required unless DTO/backend code changes.

## Assumptions

- Existing backend command-result builders already expose sufficient typed block data for #1126.
- This work should improve generic rendering rather than patching each individual browser command.
- Advanced diagnostics are useful for developers but must not be part of the default player view.
