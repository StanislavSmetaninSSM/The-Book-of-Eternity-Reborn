# Feature Specification: Browser Guardian Social Conversation and Lore

**Feature Branch**: `fix/808-browser-guardian-social`

**Created**: 2026-06-06

**Status**: Draft

**Input**: GitHub issue #808 requests browser parity for the console Guardian social actions in `ExplorerMode.Afterlife.GuardiansProjectsTrade.cs`: choosing a Guardian and either talking to them or asking for lore/knowledge writes `ActorSocialInteractionRequestState.WriteGuardianRequestAsync`; the browser currently has no prompt form or `BrowserAfterlifeWriteService` handler for this surface.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**:
  - #808: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/808
  - Parent parity epic #817: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817
- **Issue type**: enhancement, browser-client parity child.
- **Spec Kit justification**: This is browser/console parity, player-facing UX, and afterlife/Chaos Sea pending-control work. It spans C# command metadata, prompt-session writes, browser contract fixtures, tests, and GM-facing contract documentation checks, so durable Spec Kit artifacts are required by `AGENTS.md` and the constitution.
- **Contract scope**: player-facing browser, C# command protocol, `BrowserAfterlifeWriteService`, prompt-session write handler, existing `game_state/control/pending_guardian_social_interactions.json` pending request contract, GM-facing docs/examples if the contract shape or closure guidance changes.
- **Out of scope**:
  - Mortal NPC social interactions (#807, already closed).
  - Shining Abode resident interactions (#809).
  - Guardian trade (#805), resident transfer/history (#809), Shining politics/actions (#810-#811), incarnation gates (#812), relic forging (#813), storage/transport (#814), Ink Feathers (#815), and afterlife archive (#816).
  - React-side gameplay rules; the browser must use shared C# command/prompt-session services.
  - Closing umbrella #817.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start a Guardian talk/lore request from browser (Priority: P1)

A player in the browser can open a Guardian social prompt, choose a known Guardian, choose either ordinary conversation or lore/knowledge request, and submit a form that creates the same pending Guardian social request the console creates.

**Why this priority**: It directly satisfies #808 and closes one child gap in #817 without expanding into other afterlife action families.

**Independent Test**: Execute the browser command/prompt-session flow in C# tests with seeded Guardian state; submitting the form writes one `PendingGuardianSocialInteractionRequest` through `ActorSocialInteractionRequestState.WriteGuardianRequestAsync` with the selected `guardianId`, `guardianName`, `interactionType=talk|lore`, current turn, and player-facing success result.

**Acceptance Scenarios**:

1. **Given** `game_state/meta/guardians.json` contains an active or known Guardian, **When** the browser Guardian social command opens, **Then** the result is `RequiresInput` with a Guardian selection control, an interaction-type choice for conversation vs lore/knowledge, and player-facing Russian prompt copy.
2. **Given** a live prompt session for Guardian social interaction, **When** the player submits a valid Guardian and `talk`, **Then** `pending_guardian_social_interactions.json` contains one `talk` request for that Guardian and the browser result explains that the conversation was sent to the GM.
3. **Given** a live prompt session for Guardian social interaction, **When** the player submits a valid Guardian and `lore`, **Then** `pending_guardian_social_interactions.json` contains one `lore` request for that Guardian and the browser result explains that the lore/knowledge request was sent to the GM.
4. **Given** a pending request already exists for the same Guardian and interaction type, **When** the player submits another matching request, **Then** the browser keeps the message player-facing and tells the player to wait for the GM instead of silently overwriting or duplicating the request.

---

### User Story 2 - Preserve realm and default-player safety (Priority: P1)

Guardian social requests are afterlife/Chaos Sea-capable and must not be opened or written from Mortal World or other invalid contexts through direct command input or stale prompt-session submission.

**Why this priority**: #807 showed that menu-level availability gates are not enough; direct command and prompt submit/write paths must enforce realm safety for mutating local actions.

**Independent Test**: Focused C# tests prove a direct Guardian social command in Mortal World does not open `RequiresInput`, and a prompt opened in Chaos Sea cannot write if the realm changes to Mortal World before submit.

**Acceptance Scenarios**:

1. **Given** current realm is Mortal World, **When** the browser executes the Guardian social command directly, **Then** it returns a Russian player-facing blocker and does not open a prompt.
2. **Given** a prompt was opened in Chaos Sea, **When** the realm changes to Mortal World before submit, **Then** submission returns a Russian player-facing blocker and does not write `pending_guardian_social_interactions.json`.
3. **Given** malformed or blocked pending Guardian social state, **When** the browser reports the problem, **Then** default UI does not leak raw `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, or `game_state/` wording.

---

### User Story 3 - Keep browser metadata and GM contract guidance synchronized (Priority: P2)

The new Guardian social browser action must be discoverable through the current minimalist browser command/action metadata, and GM-facing contract guidance must stay accurate for the existing pending Guardian social contract.

**Why this priority**: The browser UI needs player-facing action metadata, and the GM must resolve pending Guardian social requests through `guardianSocialJournalUpdates`.

**Independent Test**: Browser command coverage/action fixture tests and documentation/source-guard tests confirm the command is represented, player-facing, and contract guidance remains aligned.

**Acceptance Scenarios**:

1. **Given** browser command coverage and game-screen fixtures are generated/checked, **When** the Guardian social command is present, **Then** it is marked as browser-supported mutating parity with Russian labels/descriptions and no raw slash-command leakage in default player UI beyond the intentional `/help`/composer surface.
2. **Given** the implementation reuses the existing `PendingGuardianSocialInteractionRequest` shape, **When** docs are reviewed, **Then** `CLI_API_Specification.md` / `Afterlife_Contract_Matrix.md` / daemon prompt guidance already cover `[GUARDIAN_SOCIAL_TALK_REQUEST]`, `[GUARDIAN_SOCIAL_LORE_REQUEST]`, `pending_guardian_social_interactions.json`, and `guardianSocialJournalUpdates`, or the PR updates the missing guidance/examples in the same change.
3. **Given** the implementation changes any pending/control field, response mode, receipt, validation rule, or GM closure guidance, **When** the PR is ready, **Then** GM-facing docs/examples/manifests and documentation/source-guard tests are updated in the same change.

### Edge Cases

- No Guardians are available: the browser command should complete or block with a muted player-facing explanation rather than opening an empty destructive form.
- Command includes a Guardian argument: the form should pre-scope/select that Guardian when it can be resolved, while still allowing manual selection if appropriate.
- Guardian state uses `guardianId`, `id`, or active-guardian mirrors: matching should follow existing code patterns and prefer stable IDs.
- Interaction type is missing or unsupported: submission should reject with Russian validation copy.
- Pending state is malformed or a matching request already exists: the browser should not overwrite/duplicate silently, and default UI must not leak raw diagnostics.
- Active GM turn/local write lock exists: the prompt session must use existing `ExplorerWebPromptSessionService`, `BrowserAfterlifeWriteService`, and local write coordination behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shared command catalog MUST expose a browser-supported Guardian social action with aliases suitable for browser command input and action metadata. The command may accept a Guardian argument.
- **FR-002**: The command result builder MUST create a prompt form that allows choosing a known Guardian and selecting `talk` or `lore` interaction type.
- **FR-003**: Direct command open MUST be blocked outside the valid afterlife/Chaos Sea context with Russian player-facing copy and without opening a prompt.
- **FR-004**: `BrowserAfterlifeWriteService` MUST handle the Guardian social command by validating realm, Guardian identity, interaction type, duplicate pending requests, and writing through `ActorSocialInteractionRequestState.WriteGuardianRequestAsync`.
- **FR-005**: Prompt submit/write MUST re-check realm before writing so stale prompt sessions cannot create afterlife Guardian social pending requests after a realm switch.
- **FR-006**: The written pending request MUST preserve existing console parity fields: `requestId`, `guardianId`, `guardianName`, `interactionType=talk|lore`, `createdAtTurn`, and `createdAtUtc`.
- **FR-007**: The browser result MUST return player-facing Russian success, pending, blocked, and validation messages; default UI MUST NOT expose raw `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, or file-path diagnostics.
- **FR-008**: `/help`, browser command coverage/action metadata, and contract fixtures MUST stay synchronized with the new browser-supported command/status.
- **FR-009**: If the pending Guardian social request payload shape or GM closure guidance changes, GM-facing prompts, docs, examples/manifests, and documentation/source-guard tests MUST be updated in the same change.
- **FR-010**: React/TypeScript changes, if any, MUST remain presentation-only and submit through existing command/prompt-session APIs; no Guardian social gameplay rules may be implemented in React.

### Key Entities *(include if feature involves data)*

- **Pending Guardian Social Interaction Request**: Client-authored afterlife pending request under `game_state/control/pending_guardian_social_interactions.json`; represents a Guardian `talk` or `lore` request awaiting GM closure.
- **Guardian social prompt session**: Browser prompt-session result with Guardian selection, interaction type, and existing local write owner/lock semantics.
- **Guardian source state**: `game_state/meta/guardians.json` entries and active Guardian mirrors used only to resolve/select stable Guardian identities and display names.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused C# tests prove the browser command opens a prompt and successful submission writes exactly one valid pending Guardian social request for `talk` and for `lore`.
- **SC-002**: Focused C# tests prove duplicate/malformed/missing-Guardian/invalid-realm cases return Russian player-facing messages without raw diagnostic leakage.
- **SC-003**: Browser frontend verification (`npm run verify`) passes after any fixture/type updates.
- **SC-004**: Documentation/contract tests pass when docs/examples/contract guidance change, or the PR records that the existing Guardian social contract was reused without shape drift.
- **SC-005**: #808 can be closed while #809-#817 remain open where their acceptance criteria are not satisfied by this slice.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ActorSocialInteractionRequestStateTests|FullyQualifiedName~ActorSocialInteractionValidationTests|FullyQualifiedName~BrowserGuardianSocialParityTests|FullyQualifiedName~BrowserTradeParityTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~CommandResult" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` if pending request fields, GM reminder text, contract docs, examples, or manifests change; otherwise document that the existing Guardian social contract was reused without GM-authored contract drift.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Static/player-facing verification**: `git diff --check origin/main...HEAD`, added-line static scan, and refined scan of default player-facing additions for raw diagnostics.

## Assumptions

- Existing `ActorSocialInteractionRequestState` is the authoritative pending request service for both console and browser Guardian talk/lore requests.
- The browser command name may be newly introduced if no existing slash command maps to the console-only Guardian panel actions; command naming should follow existing English/Russian alias patterns.
- The current browser shell remains minimalist command/composer plus `/help`; no card-heavy UI redesign is part of this issue.
- Existing GM-facing guidance already documents the Guardian social pending contract, but implementation must verify this and update docs/examples if gaps or shape changes are found.
