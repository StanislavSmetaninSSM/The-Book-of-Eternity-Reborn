# Feature Specification: Browser Shining Abode Politics

**Feature Branch**: `task/810-browser-shining-politics`

**Created**: 2026-06-07

**Status**: Draft

**Input**: GitHub issue #810 requests browser parity for the console Shining Abode politics flows in `ExplorerMode.Afterlife.ShiningAbode.Politics.cs`: found a Shining faction, request faction realignment, and request leadership transition. Console writes through `ShiningFactionRequestState.WriteFoundingRequestAsync`, `WriteRealignmentRequestAsync`, and `WriteLeadershipTransitionRequestAsync`; browser support is missing.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**:
  - #810: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/810
- **Issue type**: enhancement, browser-client parity child.
- **Spec Kit justification**: This is browser/console parity, player-facing browser UX, and afterlife/Shining Abode pending-control work. It spans command metadata, prompt-session forms, C# browser write handlers, action/help coverage, tests, and contract guard checks, so durable Spec Kit artifacts are required by `AGENTS.md` and the constitution.
- **Contract scope**: player-facing browser, shared C# command protocol, prompt-session write handling, and the existing Shining politics pending files:
  - `game_state/control/pending_shining_faction_foundings.json`
  - `game_state/control/pending_shining_faction_realignments.json`
  - `game_state/control/pending_shining_faction_leadership_transitions.json`
- **Out of scope**:
  - Sibling browser parity issues #811-#816 and umbrella closure #817.
  - React-side gameplay rules or new gameplay mechanics.
  - Changing Shining politics pending/control payload shapes, response tags, GM receipts, or GM closure guidance unless implementation discovers an unavoidable contract gap.
  - Exposing hidden Saref, Wings, internal strategy memory, or raw resource ledgers in the default browser UI.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Found a Shining faction from browser (Priority: P1)

A player in the browser can open a guided Shining politics form for founding a new faction, fill the same charter/supporter/cost inputs required by the console flow, and submit a pending founding request through the existing C# writer.

**Why this priority**: Founding a faction is one of the three named console flows in #810 and reserves player resources through the existing runtime contract.

**Independent Test**: Focused C# browser prompt/write tests seed an active Shining Abode with enough Ink Feathers, Light Sparks, and eligible ascended supporters; execute the founding browser command; submit the form; and assert exactly one `PendingShiningFactionFoundingRequest` is written through the existing writer while Feather and Light Spark reserves match console costs.

**Acceptance Scenarios**:

1. **Given** the player is in an active Shining Abode with enough resources and eligible ascended supporters, **When** the browser opens the founding command, **Then** it returns `RequiresInput` with Russian player-facing labels for faction name, hall name, charter, patron/service choices, supporters, and costs.
2. **Given** a valid founding submit, **When** no Shining politics local mutation is pending, **Then** the existing pending founding file contains one request and player resources are reserved using the console costs.
3. **Given** insufficient resources, duplicate/malformed pending state, or not enough eligible supporters, **When** the form is opened or submitted, **Then** the browser blocks with player-facing copy and writes nothing.

---

### User Story 2 - Request faction realignment from browser (Priority: P1)

A player in the browser can select a canonical visible resident who is ready to realign and request transfer to another visible faction or departure to neutrality through the existing realignment pending contract.

**Why this priority**: Realignment is a separate console flow and must preserve faction visibility filtering and resident eligibility.

**Independent Test**: Focused C# tests seed a ready-to-realign ascended resident and visible source/target factions; execute the realignment browser command; submit accepted-transfer and neutral modes; and assert valid `PendingShiningFactionRealignmentRequest` payloads.

**Acceptance Scenarios**:

1. **Given** an ascended resident is ready to realign, **When** the browser opens realignment, **Then** it lists only canonical eligible residents and visible target factions.
2. **Given** a valid accepted-transfer submit, **When** no duplicate realignment is pending, **Then** the pending realignment file contains the resident, source faction, target faction, mode, loyalty/restlessness context, and current turn.
3. **Given** a departure-to-neutral submit, **When** the resident is eligible, **Then** the target faction is empty and GM resolution remains pending.
4. **Given** no eligible residents or no safe target for transfer mode, **When** the player opens or submits the command, **Then** the browser blocks safely and does not invent eligibility rules in React.

---

### User Story 3 - Request leadership transition from browser (Priority: P1)

A player in the browser can select a visible Shining faction with current leadership and request abdication, peaceful succession, or revolt leadership transition using canonical eligible candidates and supporters.

**Why this priority**: Leadership transition is the third named console flow and has the highest risk of accidentally leaking hidden political actors if browser enumeration is careless.

**Independent Test**: Focused C# tests seed a visible faction with incumbent leadership, resident candidates, and supporters; execute the leadership browser command; submit a peaceful succession request; and assert exactly one `PendingShiningFactionLeadershipTransitionRequest` is written with incumbent/candidate/supporter fields validated by existing C# semantics.

**Acceptance Scenarios**:

1. **Given** a visible faction has non-vacant leadership, **When** the browser opens leadership transition, **Then** it lists visible factions and valid transition modes without raw diagnostics.
2. **Given** a valid peaceful succession or revolt submit, **When** candidates/supporters satisfy existing validation, **Then** the existing pending leadership transition file contains one request for GM resolution.
3. **Given** abdication is selected, **When** no replacement candidate is required by the existing C# contract, **Then** the request can leave candidate fields empty and still validates.
4. **Given** a faction has vacant leadership, hidden status, or insufficient supporters, **When** the browser opens or submits the command, **Then** it shows a player-facing blocker and writes nothing.

---

### User Story 4 - Preserve realm, player-facing copy, metadata, and contract stability (Priority: P2)

The new browser Shining politics actions are discoverable in player-facing metadata and `/help`, enforce the same Shining Abode realm/local-write blockers as console on direct open and stale submit, and keep the existing runtime contracts unchanged.

**Why this priority**: Menu presence alone is insufficient for mutating browser actions; stale prompt sessions and raw diagnostics are common parity failure modes.

**Independent Test**: Focused C# tests prove direct command open outside Shining Abode does not create `RequiresInput`, stale prompt submit after realm switch writes no pending file, player-facing action metadata exists, command coverage reports the browser forms as supported, and source guards prove the existing Shining politics write helpers are used.

**Acceptance Scenarios**:

1. **Given** current realm is not Shining Abode, **When** any Shining politics mutation command opens directly, **Then** it returns a Russian blocker and not `RequiresInput`.
2. **Given** a Shining politics prompt opened in Shining Abode, **When** the realm changes before submit, **Then** submit is blocked and no pending file is written.
3. **Given** browser action metadata and `/help` are rendered, **When** the new actions appear, **Then** their default labels/descriptions are Russian/player-facing and raw slash commands/API details are kept out of default action cards.
4. **Given** pending/control payload shapes remain unchanged, **When** verification runs, **Then** GM-facing docs/examples are not modified; if a shape or GM-authored output contract changes, the afterlife contract matrix, examples, manifest, and documentation tests are updated in the same change.

## Edge Cases

- The current soul realm is Mortal World, Chaos Sea, transit, or otherwise not an ordinary active Shining Abode.
- A prompt session is opened in Shining Abode, then the soul realm changes before submit.
- Active GM turn, local write lock, or existing pending local mutation blocks a new Shining politics request.
- Founding resources are insufficient after the prompt opens but before submit.
- Pending files are malformed or already contain a conflicting founding/realignment/leadership request.
- No canonical visible factions are available for target selection.
- No ascended supporters are eligible or fewer than the console minimum are selected.
- A selected resident is not `ready_to_realign` after current C# derived-state checks.
- A leadership faction is vacant, hidden, defeated, or no longer contains the selected candidate/supporters.
- Default browser copy must avoid raw `.json`, `pending_`, API, DTO, rollback, snapshot, debug, or `game_state/` wording.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shared command catalog MUST expose browser-supported Shining politics mutation commands or equivalent player-facing action metadata for founding a faction, faction realignment, and leadership transition.
- **FR-002**: Browser prompt construction MUST run in C# and enumerate only canonical visible/eligible factions, leaders, candidates, costs, reasons, and residents according to existing console/C# semantics.
- **FR-003**: Direct command open MUST block outside valid ordinary active Shining Abode context with Russian player-facing copy and without `RequiresInput`.
- **FR-004**: Prompt submit/write MUST re-check realm and local-write/pending conflicts before writing, so stale browser prompts cannot create pending Shining politics requests after a realm switch or blocker appears.
- **FR-005**: Founding submit MUST validate names, service choices, supporter selection, current resource balances, duplicate/malformed pending state, and write through `ShiningFactionRequestState.WriteFoundingRequestAsync` while reserving the console costs.
- **FR-006**: Realignment submit MUST validate canonical resident identity, readiness, source/target faction identity, mode, duplicate/malformed pending state, and write through `ShiningFactionRequestState.WriteRealignmentRequestAsync`.
- **FR-007**: Leadership submit MUST validate visible faction identity, incumbent leadership, transition mode, candidate/supporter eligibility, duplicate/malformed pending state, and write through `ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync`.
- **FR-008**: Browser results MUST return Russian player-facing success, waiting, blocked, and validation copy; default UI MUST NOT expose raw `.json`, `pending_`, request IDs, API, DTO, rollback, snapshot, debug, or file-path diagnostics.
- **FR-009**: `/help`, browser command coverage, player command menu/action metadata, and browser contract fixtures MUST stay synchronized with the three new browser-supported Shining politics forms.
- **FR-010**: If implementation changes any Shining politics pending/control shape, response field, receipt, validation rule, normalizer side effect, or GM-authored guidance, GM-facing docs/examples/manifests and documentation/source-guard tests MUST be updated in the same change.

### Key Entities *(include if feature involves data)*

- **Pending Shining Faction Founding Request**: Existing client-authored request under `pending_shining_faction_foundings.json` with proposed faction/hall IDs, charter, supporters, quoted costs, and current turn.
- **Pending Shining Faction Realignment Request**: Existing client-authored request under `pending_shining_faction_realignments.json` with resident/source/target faction, mode, loyalty/restlessness context, and current turn.
- **Pending Shining Faction Leadership Transition Request**: Existing client-authored request under `pending_shining_faction_leadership_transitions.json` with faction, incumbent, candidate, transition mode, supporters, and current turn.
- **Visible Shining faction**: A faction included by existing player-visible Shining filtering, not hidden Saref/Wings/internal data.
- **Browser Shining politics prompt session**: Shared C# prompt-session result with player-facing controls and existing local write owner/lock semantics.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused C# tests prove browser founding open/submit writes exactly one valid pending founding request and reserves costs.
- **SC-002**: Focused C# tests prove browser realignment open/submit writes exactly one valid pending realignment request.
- **SC-003**: Focused C# tests prove browser leadership transition open/submit writes exactly one valid pending leadership transition request.
- **SC-004**: Focused C# tests prove invalid-realm direct open and stale prompt submit are blocked without pending file writes.
- **SC-005**: Browser help/action metadata/command coverage recognize the new Shining politics browser-supported forms with player-facing default copy.
- **SC-006**: Documentation/contract tests pass when docs/contracts/examples change, or the final report records that existing Shining politics pending contracts were reused without shape drift.
- **SC-007**: #810 is ready for Hermes review while sibling #811-#816 and umbrella #817 remain untouched by lifecycle automation.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningPolitics|ShiningAbode|ExplorerWebCommandServiceTests|BrowserAfterlifeWriteServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests" --logger "console;verbosity=minimal"`
- **Build verification**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`
- **Documentation/contract verification**: Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` if pending request fields, GM reminder text, contract docs, examples, or manifests change; otherwise document that existing Shining politics contracts were reused without GM-authored contract drift.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files or browser contract fixtures change.
- **Static verification**: `git diff --check origin/main...HEAD`, added-line static security scan excluding Spec Kit docs, and refined scan of default player-facing additions for raw diagnostics.

## Assumptions

- Existing `ShiningFactionRequestState` is the authoritative pending request service for both console and browser Shining politics.
- Dedicated browser commands for the three mutation flows may coexist with read-only `/shining_politics`; the overview remains read-only and filtered.
- The current browser shell remains minimalist command/composer plus guided prompts; no broad React UI redesign is part of this issue.
- Existing GM-facing afterlife documentation already covers these pending contracts because console uses them today; implementation must update docs/examples only if shape or guidance changes.
