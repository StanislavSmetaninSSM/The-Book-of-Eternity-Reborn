# Feature Specification: Browser Inventory and Document Detail Paths

**Feature Branch**: `work/1089-browser-inventory-details`

**Created**: 2026-06-19

**Status**: Draft for autonomous implementation

**Input**: GitHub issue #1089 — Browser: добавить полноценные detail-пути для предметов, документов и книг

## Source Issues & Scope

- **Source GitHub issue(s)**: [#1089](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1089)
- **Issue type**: task / browser UX parity follow-up from #1087
- **Spec Kit justification**: The issue changes player-facing Browser Client detail flows for inventory, documents, books, and structured item mechanics. It spans C# command-result DTO rendering, browser/frontend presentation, tests, and durable handoff criteria.
- **Contract scope**: player-facing browser UX; C# command-result rendering; frontend rendering of existing `ExplorerCommandResult` blocks/actions; readable-document authority presentation. No Afterlife runtime contract changes.
- **Out of scope**: inventory mutation/write parity (drop, split, merge, storage/transport), new GM-authored JSON schema, new validation rules, React-side gameplay rules, and the broad BG3 visual redesign PR #1116. If implementation discovers unsupported readable-document authority or schema needs, create/link a follow-up instead of silently expanding this task.

## User Scenarios & Testing

### User Story 1 - Inspect a specific inventory item from the browser (Priority: P1)

A player opens `/инв` in the Browser Client and can move from the summary table to a readable detail view for a selected item.

**Why this priority**: This is the core gap named in #1089. The browser should not force players to infer mechanics from a short table row or raw JSON.

**Independent Test**: Seed browser inventory data with a named item that has description, ordinary bonuses, structured bonuses, combat effects, and special properties. Run the browser command-result service for `/инв` and the selected detail path; assert the summary exposes player-facing actions and the selected detail renders full Russian labels and values.

**Acceptance Scenarios**:

1. **Given** a browser session with a runic glove in inventory, **When** the player opens `/инв`, **Then** the inventory summary includes a player-facing action/link/command to inspect that glove.
2. **Given** the same session, **When** the player opens the glove detail, **Then** the output includes the description, ordinary effects, structured bonus target/value/type, combat effect, special properties, and equipped/status context using Russian labels.
3. **Given** the detail output, **When** it is rendered in default browser mode, **Then** it does not include raw JSON, `game_state/` paths, DTO/API/protocol/debug language, or raw internal ids as the main player-facing content.

---

### User Story 2 - Read a specific document/book from the browser (Priority: P1)

A player opens the browser books/documents surface and can select one document or book to read instead of receiving a long mixed dump.

**Why this priority**: #1089 explicitly asks for documents/books and their relation to `/книги` / readable documents.

**Independent Test**: Seed readable inventory documents via existing embedded text and sidecar authority, run `/книги` or the Browser books command, then run a selected document detail path. Assert list → selection → reading renders only the selected document content with Russian labels and an unavailable/unreadable reason when appropriate.

**Acceptance Scenarios**:

1. **Given** multiple readable documents/books, **When** the player opens `/книги`, **Then** the browser shows a shelf/list with stable selectors, titles, short previews/status, and player-facing inspect/read actions.
2. **Given** a selected document, **When** the player opens its detail, **Then** only that document's text/pages/entries are rendered, not every document in one table cell.
3. **Given** a document-looking item with no readable authority but an explicit unreadable/sealed reason, **When** it appears in the list or detail view, **Then** the player sees the reason instead of raw missing-file diagnostics.

---

### User Story 3 - Preserve command/result routing and advanced diagnostics boundaries (Priority: P2)

Browser detail paths are backed by shared C# command/result authority and the existing React command-result surface, not by separate React gameplay logic.

**Why this priority**: The Browser Client must stay presentation-only and aligned with console/client behavior while keeping advanced diagnostics explicit.

**Independent Test**: Source guards and frontend tests assert `CommandResultView` continues using `executeCommand` / prompt-session APIs, safe blocks are preserved in default mode, and raw diagnostic blocks remain hidden unless advanced mode is active.

**Acceptance Scenarios**:

1. **Given** a detail action in an inventory/books command result, **When** the player clicks it in React, **Then** it executes through the shared command-result path rather than a React-only item mutation handler.
2. **Given** raw/debug metadata exists for advanced inspection, **When** default mode renders the result, **Then** the raw diagnostics are absent while useful safe blocks remain visible.

### Edge Cases

- A selected item/document is missing, hidden, or not visible to the player: show a Russian unavailable/not found state without raw path or JSON details.
- A structured bonus has incomplete authority: show only safe narrative/display text or a player-facing unresolved reason; do not invent mechanics from descriptions alone.
- A document selector is numeric: stable id matching wins before shelf index fallback, so id `"2"` can be selected even if it is not row 2.
- Advanced mode may expose explicit diagnostics, but default mode must not.
- If implementation touches GM-authored readable-document authority or validation invariants, GM docs/examples or a tracked follow-up are required before closure.

## Requirements

### Functional Requirements

- **FR-001**: Browser inventory summary MUST expose player-facing read-only detail actions/commands for inventory items when detail data exists.
- **FR-002**: Browser item detail MUST render description, item category/status, ordinary bonuses/effects, structured bonuses with Russian labels and complete target/value/value-type metadata, combat effects, and special properties when present.
- **FR-003**: Browser books/documents summary MUST expose a list/shelf with stable selectors and read actions for individual readable documents/books.
- **FR-004**: Browser document/book detail MUST render only the selected document/book content and must preserve readable-document authority rules from the existing C# client.
- **FR-005**: Default player-facing browser output MUST NOT expose raw JSON, local file paths, DTO/API/endpoint/protocol/debug wording, internal-only ids, or acceptance/spec language.
- **FR-006**: React/frontend code MUST remain presentation-only: it may render `ExplorerCommandResult` blocks/actions and submit existing command/prompt-session APIs, but it MUST NOT implement inventory/book gameplay rules.
- **FR-007**: Existing console semantics and browser command entry via `/инв`, `/inventory`, `/книги`, `/books`, `/читать` MUST remain available.
- **FR-008**: The change MUST include focused regression/source-guard coverage for item detail, document/book detail, raw-output filtering, and command-result action routing.

### Key Entities

- **Inventory item detail**: Player-visible projection of a canonical inventory item, including display name, type/status, description, bonuses/effects, structured mechanical metadata, combat effects, and properties.
- **Readable document shelf entry**: Player-visible list row for an embedded/sidecar readable document or explicit unreadable reason, with deterministic selector, title, preview/status, and action.
- **Selected document detail**: Player-visible detail/read view for exactly one document/book and its authority-derived text entries/pages.
- **Command-result action**: Existing C# `ExplorerCommandResult` action metadata that React renders and executes through shared command APIs.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Focused C# tests prove `/инв` includes at least one item detail action and selected item detail renders full seeded item data without raw diagnostics.
- **SC-002**: Focused C# tests prove `/книги`/books list supports selected document detail and does not dump all long document bodies into one browser table cell.
- **SC-003**: Frontend/source-guard coverage proves default command-result rendering preserves safe detail blocks and hides raw diagnostics unless advanced mode is active.
- **SC-004**: `npm run verify --prefix BookOfEternityClient.WebFrontend` and focused browser/.NET tests pass with non-zero counts before PR.

## Verification Plan

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Inventory|FullyQualifiedName~Books|FullyQualifiedName~ReadableInventoryDocument|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Documentation tests are not required if the implementation is presentation-only over existing readable-document/item authority. If validation/schema/prompts/examples change, add `ExampleDocumentationValidationTests` / documentation coverage commands.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`; focused frontend tests/source guards around `CommandResultView`, `BlockRenderer`, and action routing when frontend files change.
- **Manual/player-facing verification**: Browser flow: open `/инв`, inspect a runic glove/item detail, open `/книги`, select and read one document/book. Capture a dependency-light visual smoke artifact if the UI surface is materially changed.

## Assumptions

- The first implementation slice should prioritize read-only detail paths and player-facing rendering over inventory mutation parity.
- Existing C# readable-document authority remains the source of truth; no new GM-authored schema is intended for #1089.
- Existing Browser Client command-result view can render the new safe blocks/actions with small presentation/source-guard adjustments, if needed.
- The active open PR #1116 is an unrelated visual redesign and must not be merged or used as a dependency for this issue.
