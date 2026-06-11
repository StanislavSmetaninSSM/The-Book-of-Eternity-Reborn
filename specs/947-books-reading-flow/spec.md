# Feature Specification: /books document selection and reading flow

**Feature Branch**: `work/947-books-reading-flow`

**Created**: 2026-06-11

**Status**: Draft for autonomous implementation

**Input**: GitHub issue #947 — make `/книги` / `/books` a document shelf with selected-document reading views.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #947 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/947
- **Related issue(s)**: #858 readable inventory document authority; #940 display QA context; #946 NPC detail drill-down; #948 mortal read-only drill-down audit.
- **Issue type**: player-facing Mortal World console/browser UX, read-only command-result parity, and readable-document authority presentation.
- **Spec Kit justification**: The issue changes the player-facing `/books` command flow, spans console and browser surfaces, and depends on readable-document authority across embedded item text, sidecar text updates, item journals, and unreadable reasons. It is not a tiny one-file fix.
- **Contract scope**: read-only Mortal World document inspection and player-facing selected-document presentation over existing `ReadableInventoryDocumentAuthority`. It must not add item mutation, local-turn writes, afterlife pending/control contracts, or React-side gameplay rules.
- **GM/docs scope**: This surface reflects GM-authored inventory/document content. If command behavior, supported readable fields, validation authority, prompts, rules, examples, or manifests change, update the closest GM-facing docs/examples/tests in the same branch or create a tracked follow-up before closure.
- **Out of scope**: changing readable-document validation invariants except where required for selection/detail presentation; broad #948 mortal drill-down audit; afterlife detail drill-down audit (#949); NPC drill-down (#946); adding document edit/write actions; cloud/remote services.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Console player first sees a document shelf (Priority: P1)

A Mortal World player runs `/книги` or `/books` and sees a concise list of available readable and known-unreadable documents instead of a giant combined text dump.

**Independent Test**: A focused console test seeds multiple documents with long text, sidecar entries, item journals, and a sealed document, runs `/книги`, and asserts the first view is a shelf/list with titles, sources/status hints, and previews/counts without dumping all long body text.

**Acceptance Scenarios**:

1. **Given** multiple long readable documents, **When** the player opens `/книги`, **Then** the first view lists documents and does not combine all full bodies into one panel/table.
2. **Given** a document resolves from embedded `textContent`, **When** the shelf renders, **Then** the row shows the document title/name, source item identity when helpful, access status, and a short preview or entry/page count.
3. **Given** a document resolves from `item_text_updates.json` or `item_journals.json`, **When** the shelf renders, **Then** it appears as its own selectable row rather than being hidden or dumped into another row.
4. **Given** a sealed/unreadable document, **When** the shelf renders, **Then** it stays visible with player-facing access status and no raw implementation details.

---

### User Story 2 - Console player can open one selected document (Priority: P1)

A player selects a specific document from the shelf and receives a dedicated reading view for that one document with clean long-text handling and back navigation.

**Independent Test**: A focused console flow test selects one seeded document and asserts only that document's content appears in the reading panel, other documents' full bodies are not present, and back navigation returns to the shelf/list.

**Acceptance Scenarios**:

1. **Given** a shelf with at least two readable documents, **When** the player selects one document, **Then** the reading panel shows only that document's title and content.
2. **Given** a selected document has multiple paragraphs/pages/entries, **When** the reading panel renders, **Then** entries are readable with clear separation and no raw JSON or file paths.
3. **Given** the player uses back navigation, **When** the reading panel exits, **Then** the player returns to the document shelf or the prior Explorer flow consistently with existing console navigation patterns.
4. **Given** the selected document is unreadable, **When** the reading panel opens, **Then** the unavailable reason is shown in-world/player-facing text without dumping blank body panels.

---

### User Story 3 - Browser `/books` exposes equivalent list/detail authority (Priority: P1)

A browser player using `/books` or `/книги` receives a list/detail model or equivalent read-only command/action flow rather than a table cell containing all document bodies.

**Independent Test**: Browser command-result tests assert `/books` returns document shelf data with selectable/detail affordances or focused detail blocks, uses player-facing Russian copy, hides raw paths/API/DTO/debug terms, and can open or represent a single selected document without React inventing gameplay rules.

**Acceptance Scenarios**:

1. **Given** the same seeded document set, **When** the browser command pipeline executes `/books`, **Then** the result exposes document-level summaries rather than one giant combined `Запись` cell.
2. **Given** a selected document can be opened through an existing command/action pattern, **When** the browser submits that selection, **Then** C# command/result authority returns only the selected document's content.
3. **Given** full browser interactivity is larger than this issue, **When** #947 closes, **Then** a dedicated linked follow-up records the exact browser gap and #947 still ships a non-dumping player-facing summary.

---

### User Story 4 - Existing readable-document authority remains intact (Priority: P1)

The selection/detail flow continues to resolve embedded item text, sidecar text updates, item journals, and unreadable reasons through `ReadableInventoryDocumentAuthority` without weakening validation or identity matching.

**Independent Test**: Focused tests cover stable-id matching before name fallback for embedded text, `item_text_updates.json`, `item_journals.json`, and unreadable documents, then rerun existing validation/Explorer command tests.

**Acceptance Scenarios**:

1. **Given** sidecar text matches an item by stable id, **When** the shelf/detail model is built, **Then** the sidecar content attaches to the intended document even if the sidecar name differs.
2. **Given** a sidecar entry has no matching inventory document, **When** the shelf renders, **Then** it appears as a standalone readable record if existing authority treats it as readable.
3. **Given** a readable-looking inventory item lacks text authority and has an explicit unreadable reason, **When** the shelf/detail renders, **Then** that reason is visible and validation semantics remain unchanged.
4. **Given** a document-like item lacks readable authority and lacks an unreadable reason, **When** validation runs, **Then** existing readable-document authority validation still reports the missing detail authority issue.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/книги` and `/books` MUST first present a document shelf/list rather than dumping every full document body in one combined output.
- **FR-002**: Each shelf row MUST include a player-facing document title/name, access status, source/context hint when available, and a short preview or count/status hint.
- **FR-003**: The player MUST be able to open a specific document detail/reading view through the console flow and through the browser command-result/action model or a precisely scoped linked follow-up if full browser interactivity is deferred.
- **FR-004**: A selected document detail view MUST show only that document's content or unavailable reason; it MUST NOT include other documents' full bodies.
- **FR-005**: Long text MUST be split into readable paragraphs/pages/entries or otherwise rendered without forcing a single giant table cell.
- **FR-006**: Unreadable/sealed/locked documents MUST remain visible when they are player-known and MUST show a player-facing reason without raw JSON, file paths, API, DTO, endpoint, or debug language in default mode.
- **FR-007**: The implementation MUST continue to resolve embedded `textContent`, `game_state/inventory/item_text_updates.json`, and `game_state/npcs/item_journals.json` through `ReadableInventoryDocumentAuthority` or an equivalent shared C# authority layer, not duplicated React logic.
- **FR-008**: Stable identity matching (`existedId`, `itemId`, `id`) MUST remain preferred over name fallback for sidecar attachment.
- **FR-009**: Existing readable-document authority validation MUST remain intact; do not hide invalid document-like items just to make the shelf cleaner.
- **FR-010**: Console navigation MUST follow existing ExplorerMode patterns for selected lists/details and back behavior.
- **FR-011**: Browser UI/React changes, if any, MUST remain presentation-only over typed C# command-result/action metadata and MUST preserve advanced/debug separation.
- **FR-012**: Focused tests MUST cover embedded `textContent`, sidecar `item_text_updates`, item journals, unreadable documents, and the no-giant-combined-output regression.
- **FR-013**: If GM-facing authoring guidance or examples for readable inventory documents change, the corresponding docs/examples/source guards MUST be updated in the same branch.
- **FR-014**: No afterlife/Chaos Sea/Shining Abode pending/control/runtime contract is changed by this issue.

### Data Entities

- **Readable document shelf item**: a read-only player-facing summary with stable selection identity, display title, source/context hint, access status, preview/count, and availability reason when unreadable.
- **Readable document detail**: a read-only focused view for one shelf item containing title, source/access context, content entries/paragraphs/pages, and unavailable reason when no readable body is available.
- **Readable authority source**: embedded inventory item `textContent`, sidecar `item_text_updates.json`, item-journal entries, and unreadable reason fields resolved by `ReadableInventoryDocumentAuthority`.
- **Browser command result/action metadata**: typed C# `ExplorerCommandResult` / `UiBlock` / action metadata consumed by the browser frontend; React must not perform document authority resolution.

## Success Criteria *(mandatory)*

- **SC-001**: RED tests prove current `/books` output dumps/combines full document content and lacks a selected-document detail model.
- **SC-002**: GREEN tests prove the shelf/list first view hides other documents' full bodies while still showing title/status/preview/count for embedded text, sidecar updates, journals, and unreadable documents.
- **SC-003**: GREEN tests prove selecting/opening one document displays only that document's content or unreadable reason.
- **SC-004**: Browser command-result evidence exists for equivalent list/detail authority or a linked follow-up documents an intentionally deferred browser interaction gap.
- **SC-005**: Existing readable-document validation, Explorer command, prompt escaping/source guard, and browser command tests remain green.
- **SC-006**: `git diff --check`, focused C# tests, relevant build gates, Spec Kit prerequisite check, and static scan pass before PR.

## Verification Plan *(mandatory)*

- **Baseline before implementation**: run a focused C# slice covering `/books`, readable-document authority validation, Explorer command rendering, and browser command result surfaces before Codex starts, then record exact counts in `tasks.md` and the Codex prompt.
- **Focused test slice**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Books|FullyQualifiedName~ReadableInventoryDocument|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`
- **Validation slice**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"`
- **Build**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`; build tests project when tests change.
- **Frontend**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if React/frontend files change.
- **Spec Kit**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from the feature branch.
- **Diff/static hygiene**: `git diff --check origin/main...HEAD`; added-line static security scan excluding `specs/**`, scratch plans, and `TestResults/**` false positives.

## Assumptions

- The first implementation slice should prefer a shared C# shelf/detail projection over duplicating row/detail construction separately in console and browser.
- The selected document identity may be implemented with a stable generated selector derived from authority identity/name; it must be deterministic within a command result and safe for player-facing selection.
- If an existing generic command/action detail pattern can support browser selection, reuse it. If not, add the smallest read-only C# command-result/action metadata needed or create a linked follow-up if full React UI work would exceed #947.
- Documentation updates are expected if the supported GM-authored document contract or examples change; if the branch only changes client-owned presentation over an existing documented contract, document why no GM prompt update was required.
