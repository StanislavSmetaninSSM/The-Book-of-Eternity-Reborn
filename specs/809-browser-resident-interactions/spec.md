# Feature Specification: Browser Shining Abode Resident Interactions

**Feature Branch**: `fix/809-browser-residents`

**Created**: 2026-06-06

**Status**: Draft

**Input**: GitHub issue #809 requests browser parity for console resident actions in `ExplorerMode.Afterlife.GuardiansProjectsTrade.cs`: request Shining Abode resident roster, ask a resident to talk, ask a resident to reveal history, and request resident transfer. The console uses `GuardianAbodeResidentRequestState.WriteResidentsRequestAsync`, `WriteInteractionRequestAsync`, and `WriteTransferRequestAsync`; the browser currently has no prompt forms/write handlers for these surfaces.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**:
  - #809: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/809
  - Parent parity epic #817: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817
- **Issue type**: enhancement, browser-client parity child.
- **Spec Kit justification**: This is browser/console parity, player-facing browser UX, and afterlife/Shining Abode pending-control work. It spans C# command metadata, prompt-session forms, browser write handlers, browser contract fixtures, tests, and GM-facing contract documentation checks, so durable Spec Kit artifacts are required by `AGENTS.md` and the constitution.
- **Contract scope**: player-facing browser, shared C# command protocol, prompt-session write handling, existing Shining Abode resident pending files:
  - `game_state/control/pending_guardian_abode_residents_request.json`
  - `game_state/control/pending_guardian_abode_resident_interactions.json`
  - `game_state/control/pending_guardian_abode_resident_transfers.json`
  - GM-facing docs/examples if the existing request shapes, hidden routing tags, closure receipts, or guidance are missing or change.
- **Out of scope**:
  - Mortal NPC social interactions (#807, already closed).
  - Guardian talk/lore (#808, already closed).
  - Guardian/NPC/Shining trade (#805), inventory management (#806), Shining politics/actions (#810-#811), incarnation gates (#812), relic forging (#813), storage/transport (#814), Ink Feathers (#815), and afterlife archive (#816).
  - Direct resident relic grant and personal quest request actions from the console resident detail menu; #809 explicitly names roster, talk, history, and transfer only.
  - React-side gameplay rules; the browser must use shared C# command/prompt-session services.
  - Closing umbrella #817.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Request the resident roster from browser (Priority: P1)

A player in the browser can choose a Guardian/Shining Abode that has no materialized resident roster yet and request that the GM materialize the resident roster using the same pending roster request contract as the console.

**Why this priority**: Resident talk/history/transfer cannot proceed until the browser can either read an existing roster or request one using the established C# contract.

**Independent Test**: Focused C# browser command/prompt-session tests seed a Guardian with a Shining Abode and no resident entries, execute the browser resident roster command, submit the selected Guardian/Abode, and assert one pending residents request is written through `GuardianAbodeResidentRequestState.WriteResidentsRequestAsync` with `guardianId`, `abodeId`, `requestMode`, `currentReputation`, `createdAtTurn`, and player-facing success copy.

**Acceptance Scenarios**:

1. **Given** `guardians.json` contains a Guardian with an `abode`, **When** the browser opens the resident roster command, **Then** the result is `RequiresInput` with a Guardian/Abode selection and Russian player-facing prompt copy.
2. **Given** a valid roster prompt submission, **When** the selected Guardian/Abode has no residents, **Then** `pending_guardian_abode_residents_request.json` contains one matching request and the browser result tells the player the roster request was sent to the GM.
3. **Given** a matching roster request is already pending, **When** the player submits the same Guardian/Abode again, **Then** the browser does not overwrite/duplicate silently and tells the player to wait for the GM.

---

### User Story 2 - Talk/history requests for materialized residents (Priority: P1)

A player in the browser can choose a materialized Shining Abode resident and request either ordinary conversation or past-history reveal through the same interaction pending contract the console uses.

**Why this priority**: It directly satisfies the named Resident Talk and Resident History actions in #809.

**Independent Test**: Focused C# tests seed `guardian_abode_residents.json` with a present resident and submit browser resident interaction forms for `talk` and `history`; each write produces exactly one `PendingGuardianAbodeResidentInteractionRequest` with `interactionType=talk|history`, matching guardian/abode/resident IDs, current turn, and player-facing success copy.

**Acceptance Scenarios**:

1. **Given** a materialized resident exists for a Guardian/Abode, **When** the browser opens the resident interaction command, **Then** the prompt allows selecting the resident and choosing `talk` or `history` where those actions are available.
2. **Given** a valid `talk` submit, **When** no matching talk request is already pending, **Then** `pending_guardian_abode_resident_interactions.json` contains one talk request for that resident.
3. **Given** a valid `history` submit, **When** no matching history request is already pending, **Then** the same pending interactions file contains one history request for that resident.
4. **Given** a matching resident interaction request already exists, **When** the player submits a duplicate interaction type for the same resident, **Then** the browser keeps the result player-facing and does not silently overwrite or duplicate the request.

---

### User Story 3 - Resident transfer request from browser (Priority: P1)

A player in the browser can select a resident whose migration state permits transfer and request either the recommended target Shining Abode or departure-only transfer using the console's resident transfer pending contract.

**Why this priority**: Resident Transfer is one of the named missing console parity actions in #809 and uses a separate pending/control surface.

**Independent Test**: Focused C# tests seed source and target Guardians/Abodes plus a transfer-ready resident, submit the browser resident transfer form, and assert `pending_guardian_abode_resident_transfers.json` contains one valid transfer request. A departure-only variant is also covered when no suitable target exists or the player chooses departure-only.

**Acceptance Scenarios**:

1. **Given** a resident has `migrationState=ready_to_transfer`, **When** the browser opens transfer, **Then** the prompt lists safe transfer choices derived from shared C# transfer competition logic or a departure-only option.
2. **Given** a valid target transfer submit, **When** no transfer is already pending for that resident, **Then** the browser writes one transfer request with source/target Guardian and Abode IDs, transfer mode, selection mode, current devotion/restlessness, competition metadata where applicable, and current turn.
3. **Given** the player chooses departure-only, **When** the submit is valid, **Then** the pending transfer request records `transferMode=departure_only` and player-facing copy explains that GM resolution is required.
4. **Given** a transfer request is already pending or the resident is not transfer-ready, **When** the browser submit is attempted, **Then** no new pending transfer is written and the result remains Russian/player-facing.

---

### User Story 4 - Preserve realm, player-facing copy, metadata, and GM docs (Priority: P2)

Browser resident actions are valid only in afterlife/Shining Abode context, must not leak raw pending/control diagnostics in default player UI, must be discoverable through current browser command/action metadata, and must keep GM-facing contract guidance synchronized.

**Why this priority**: Menu-level availability is not enough for mutating browser actions; direct command open and stale prompt submissions must enforce realm safety and docs must remain authoritative for GM resolution.

**Independent Test**: C# tests prove direct command open from Mortal World does not create `RequiresInput`, stale prompt submit after realm switch does not write, source guards/fixtures include player-facing browser metadata, and docs/source-guard tests pass when docs change.

**Acceptance Scenarios**:

1. **Given** current realm is Mortal World, **When** any resident browser command opens directly, **Then** it returns a Russian blocker and does not open a prompt.
2. **Given** a resident prompt was opened in an afterlife realm, **When** the realm changes to Mortal World before submit, **Then** submission returns a Russian blocker and writes no pending file.
3. **Given** malformed pending files, missing Guardian/Abode/resident state, invalid interaction type, or local-write contention, **When** the browser reports the problem, **Then** default copy does not expose raw `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, or `game_state/` wording.
4. **Given** browser command coverage and contract fixtures are checked, **When** resident commands/actions are present, **Then** labels/descriptions are Russian/player-facing and no raw slash-command leakage appears in default UI outside the intentional `/help`/composer surface.
5. **Given** implementation changes any pending/control shape, response tag, receipt, validation rule, or GM closure guidance, **When** the PR is ready, **Then** GM-facing docs/examples/manifests and documentation/source-guard tests are updated in the same change.

## Edge Cases

- No Guardians with Abodes are available: browser blocks with player-facing copy instead of opening an empty mutating form.
- Guardian has an Abode but no materialized residents: roster request is available; resident talk/history/transfer explains that the roster must be materialized first.
- Resident state contains nested non-resident references or historical receipt objects with `residentId`: browser selection must enumerate canonical resident roster entries only, not arbitrary nested objects.
- Resident is not present: talk/history/transfer availability follows existing console/C# semantics and must not create nonsensical pending requests.
- `availableInteractions` omits `talk` or `history`: browser respects the same availability semantics as console default/fallback behavior.
- Pending interaction/transfer bundles are malformed: browser blocks writes with safe player copy and does not overwrite files.
- Active GM turn/local write lock exists: submit path uses existing prompt-session/local write coordination.
- Transfer target competition yields weak/no target: browser offers departure-only or safe recommendations consistent with shared C# transfer logic.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shared command catalog MUST expose browser-supported resident roster, resident interaction, and resident transfer commands/aliases or an equivalent browser-supported resident command with action modes for roster/talk/history/transfer.
- **FR-002**: Browser command result builders MUST create prompt forms that use shared C# state to select Guardians/Abodes/residents and action modes; React/TypeScript MUST remain presentation-only.
- **FR-003**: Direct command open MUST be blocked outside valid afterlife/Shining Abode context with Russian player-facing copy and without `RequiresInput`.
- **FR-004**: Prompt submit/write MUST re-check realm before writing so stale prompt sessions cannot create resident pending requests after a realm switch.
- **FR-005**: Roster submit MUST validate Guardian/Abode identity, duplicate/malformed pending state, current reputation/request mode, and write through `GuardianAbodeResidentRequestState.WriteResidentsRequestAsync`.
- **FR-006**: Talk/history submit MUST validate canonical resident identity, Guardian/Abode relationship, interaction type, duplicate/malformed pending state, and write through `GuardianAbodeResidentRequestState.WriteInteractionRequestAsync`.
- **FR-007**: Transfer submit MUST validate canonical resident identity, transfer readiness, transfer target or departure-only mode, duplicate/malformed pending state, and write through `GuardianAbodeResidentRequestState.WriteTransferRequestAsync`.
- **FR-008**: Browser results MUST return Russian player-facing success, waiting, blocked, and validation copy; default UI MUST NOT expose raw `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, or file-path diagnostics.
- **FR-009**: `/help`, browser command coverage/action metadata, and frontend contract fixtures MUST stay synchronized with the new browser-supported resident actions.
- **FR-010**: If any resident pending request payload shape, response tag, receipt requirement, or GM closure guidance changes or is missing from docs, GM-facing prompts, docs, examples/manifests, and documentation/source-guard tests MUST be updated in the same change.

### Key Entities *(include if feature involves data)*

- **Pending Guardian Abode Residents Request**: Client-authored request under `pending_guardian_abode_residents_request.json` for GM materialization of a Guardian's Shining Abode resident roster.
- **Pending Guardian Abode Resident Interaction Request**: Client-authored request under `pending_guardian_abode_resident_interactions.json` for resident `talk` or `history` interaction awaiting GM receipt/closure.
- **Pending Guardian Abode Resident Transfer Request**: Client-authored request under `pending_guardian_abode_resident_transfers.json` for resident transfer/departure awaiting GM receipt/closure.
- **Resident source state**: Canonical entries in `game_state/meta/guardian_abode_residents.json` associated with a Guardian/Abode, not arbitrary nested receipt/history objects.
- **Browser resident prompt session**: Shared C# prompt-session result with Guardian/Abode/resident/action controls and existing local write owner/lock semantics.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused C# tests prove browser roster request opens/submits and writes exactly one valid pending residents request.
- **SC-002**: Focused C# tests prove browser resident `talk` and `history` submissions write exactly one valid pending interaction request each.
- **SC-003**: Focused C# tests prove browser resident transfer target and departure-only submissions write valid pending transfer requests, and duplicate/not-ready cases do not write.
- **SC-004**: Focused C# tests prove invalid-realm direct command and stale prompt submit are blocked without pending file writes.
- **SC-005**: Browser frontend verification (`npm run verify`) passes after fixture/type updates.
- **SC-006**: Documentation/contract tests pass when docs/contracts/examples change, or the PR records that existing resident pending contracts were reused without shape drift.
- **SC-007**: #809 can be closed while #810-#817 remain open where their acceptance criteria are not satisfied by this slice.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~GuardianAbodeResident|FullyQualifiedName~BrowserResident|FullyQualifiedName~BrowserGuardianSocialParityTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~BrowserTradeParityTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~CommandResult" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` if pending request fields, GM reminder text, contract docs, examples, or manifests change; otherwise document that existing resident contracts were reused without GM-authored contract drift.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Static/player-facing verification**: `git diff --check origin/main...HEAD`, added-line static security scan, and refined scan of default player-facing additions for raw diagnostics.

## Assumptions

- Existing `GuardianAbodeResidentRequestState` is the authoritative pending request service for both console and browser resident roster/interactions/transfers.
- Browser command naming may be one multi-mode command or several focused commands, but it must be discoverable through `/help`/browser command metadata and stay player-facing.
- The current browser shell remains minimalist command/composer plus `/help`; no card-heavy UI redesign is part of this issue.
- Existing GM-facing docs may already cover resident roster, resident interaction, and transfer contracts; implementation must verify this and update docs/examples if gaps or shape changes are found.
