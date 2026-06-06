# Feature Specification: Browser NPC Social Conversation

**Feature Branch**: `fix/807-browser-npc-social`

**Created**: 2026-06-06

**Status**: Draft

**Input**: GitHub issue #807 requests browser parity for the console NPC conversation action: console writes `ActorSocialInteractionRequestState.WriteNpcRequestAsync`; browser currently lacks a prompt form and `BrowserMortalWorldWriteService` handler.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**:
  - #807: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/807
  - Parent parity epic #817: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817
- **Issue type**: enhancement, browser-client parity child.
- **Spec Kit justification**: This is browser/console parity and player-facing UX work that crosses C# command metadata, prompt-session writes, frontend/browser rendering, tests, and a Mortal pending request contract already visible to the GM. Durable artifacts are required by `AGENTS.md` and the constitution.
- **Contract scope**: player-facing browser, C# command protocol, prompt-session write handler, Mortal World pending NPC social request state, GM-facing reminder/docs/examples only if the pending request payload shape changes.
- **Out of scope**:
  - Guardian social interactions and lore (#808).
  - Shining Abode resident interactions (#809).
  - Trade, inventory, relic, archive, or afterlife political actions (#810-#816).
  - React-side gameplay logic; browser must use shared C# command/prompt-session services.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start a Mortal NPC conversation from browser (Priority: P1)

A player in the browser can choose a known Mortal World NPC and supply a short conversation topic, then submit a form that creates the same pending NPC social request the console creates for `Поговорить`.

**Why this priority**: It directly satisfies #807 and closes one child gap in #817 without expanding into other social surfaces.

**Independent Test**: Execute the browser command/prompt-session flow in C# tests with fixture NPC state; submitting the form writes `game_state/control/pending_npc_social_interactions.json` through `ActorSocialInteractionRequestState.WriteNpcRequestAsync` and returns a Russian player-facing success result.

**Acceptance Scenarios**:

1. **Given** `game_state/npcs/npc_core.json` contains a Mortal NPC with `npcId` and `name`, **When** the browser command for NPC conversation is opened, **Then** the result is `RequiresInput` with a selectable NPC prompt and a conversation topic prompt.
2. **Given** a live prompt session for NPC conversation, **When** the player submits a valid NPC and topic, **Then** `pending_npc_social_interactions.json` contains one `talk` request for that NPC with the current turn and player topic, and the browser result explains that the conversation was sent to the GM.
3. **Given** a pending `talk` request already exists for the same NPC, **When** the player tries to submit another talk request, **Then** the browser keeps the form/result player-facing and tells the player to wait for the GM instead of silently overwriting or duplicating the request.

---

### User Story 2 - Keep default browser UI player-facing (Priority: P2)

The browser action/result surfaces for this conversation must use Russian in-world copy and avoid raw command/API/file-path/DTO/debug wording outside advanced mode.

**Why this priority**: Previous browser parity work repeatedly regressed by exposing raw local-write or pending-file diagnostics in default UI.

**Independent Test**: Focused source/contract tests assert action metadata/help/result copy contains player-facing labels and excludes raw protocol wording in default blocks/notifications.

**Acceptance Scenarios**:

1. **Given** malformed or blocked pending NPC social state, **When** the browser flow reports the problem, **Then** the default message is a Russian player-facing blocker, not raw `.json`, `pending_`, `requestId`, API, DTO, rollback, or debug text.
2. **Given** command metadata and `/help` are rendered in the browser, **When** the player sees the NPC conversation action, **Then** labels/descriptions are Russian and do not expose raw slash-command framing except where the current minimalist command input/help pattern already intentionally lists commands.

---

### User Story 3 - Preserve GM contract authority (Priority: P3)

If the implementation stores a player topic in the pending NPC social request, GM-facing reminders and examples must teach the GM to read and close that request through `npcInteractionJournalUpdates` without changing afterlife-only contracts.

**Why this priority**: The GM must resolve pending NPC conversations; code-only contract drift would break the product behavior.

**Independent Test**: Documentation/source-guard tests or focused existing contract tests cover the pending NPC social request fields and reminder text; examples/manifests are updated if the payload shape changes.

**Acceptance Scenarios**:

1. **Given** a pending NPC social request includes a player topic, **When** the GM reminder fragment is built, **Then** it mentions the topic and still requires closure through `npcInteractionJournalUpdates` with `requestId`, `npcId`, `interactionType`, status, title, summary, turn, and timestamp.
2. **Given** afterlife contract docs/examples are checked, **When** this Mortal-only feature is reviewed, **Then** no afterlife pending/control contract is added or renamed; existing afterlife preservation semantics remain intact.

### Edge Cases

- No NPCs are available: the browser command should complete or block with a muted player-facing explanation rather than opening an empty destructive form.
- Command includes an NPC argument: the form should pre-scope/select that NPC when it can be resolved, while still allowing manual selection if appropriate.
- NPC state uses `npcId` or `id`: matching should follow existing code patterns and be stable-id-first.
- Topic is blank or whitespace: submission should be rejected with a Russian form validation message if the topic is required; if implementation chooses an optional topic, the spec/task must document why and preserve console parity.
- Pending state is malformed or already has a request for the same NPC/talk: the browser should not overwrite or duplicate silently, and default UI must not leak raw diagnostics.
- Active GM turn/local write lock exists: the prompt session must use existing `ExplorerWebPromptSessionService` and local write coordination behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shared command catalog MUST expose a browser-supported Mortal World NPC conversation action with aliases suitable for browser command input and action metadata.
- **FR-002**: The command result builder MUST create a prompt form that allows choosing a known NPC and entering the conversation topic/purpose.
- **FR-003**: `BrowserMortalWorldWriteService` MUST handle the NPC conversation command by validating the NPC, validating the topic, checking duplicate pending talk requests, and writing through `ActorSocialInteractionRequestState.WriteNpcRequestAsync`.
- **FR-004**: The written pending request MUST preserve console parity fields (`requestId`, `npcId`, `npcName`, `interactionType=talk`, `createdAtTurn`, `createdAtUtc`) and MUST store the browser-supplied topic if the topic is not already represented by an existing authoritative field.
- **FR-005**: The browser result MUST return player-facing Russian success, pending, blocked, and validation messages; default UI MUST NOT expose raw `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, or file-path diagnostics.
- **FR-006**: `/help`, browser command coverage/action metadata, and contract fixtures MUST stay synchronized with the new browser-supported command/status.
- **FR-007**: If the pending NPC social request payload shape changes, GM-facing reminder text, examples/manifests, and documentation/source-guard tests MUST be updated in the same change.
- **FR-008**: React/TypeScript changes, if any, MUST remain presentation-only and submit through existing command/prompt-session APIs; no NPC social gameplay rules may be implemented in React.

### Key Entities *(include if feature involves data)*

- **Pending NPC Social Interaction Request**: Client-authored Mortal World pending request under `game_state/control/pending_npc_social_interactions.json`; represents one NPC talk request awaiting GM closure.
- **NPC conversation prompt session**: Browser prompt-session result with NPC selection, topic text, and existing local write owner/lock semantics.
- **NPC source state**: `game_state/npcs/npc_core.json` entries used only to resolve/select stable NPC identities and display names.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused C# tests prove the browser command opens a prompt and successful submission writes exactly one valid pending NPC talk request for the selected NPC.
- **SC-002**: Focused C# tests prove duplicate/malformed/missing-NPC cases return Russian player-facing messages without raw diagnostic leakage.
- **SC-003**: Browser frontend verification (`npm run verify`) passes after any fixture/type updates.
- **SC-004**: If contract/docs changed, the relevant documentation coverage/example tests pass with non-zero counts.
- **SC-005**: #807 can be closed while #808-#817 remain open where their acceptance criteria are not satisfied by this slice.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ActorSocialInteractionRequestStateTests|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~CommandResult" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Run `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests` if pending request fields, GM reminder text, contract docs, examples, or manifests change; otherwise document that the existing Mortal pending contract was reused without GM-authored contract drift.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Manual/player-facing verification**: Inspect command result/action metadata for Russian player-facing copy and no raw diagnostic wording in default result blocks/notifications.

## Assumptions

- Existing `ActorSocialInteractionRequestState` is the authoritative pending request service for both console and browser NPC talk requests.
- The browser command name may be newly introduced if no existing command maps to the console-only `Поговорить` action; command naming should follow existing English/Russian alias patterns.
- The current browser shell remains minimalist command/composer plus `/help`; no card-heavy UI redesign is part of this issue.
- Existing afterlife preservation semantics for Mortal pending requests must remain unchanged.
