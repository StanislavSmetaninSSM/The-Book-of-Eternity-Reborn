# Feature Specification: QTE Layout-Independent Key Input

**Feature Branch**: `920-qte-layout-keys`

**Created**: 2026-06-09

**Status**: Draft

**Input**: GitHub issue #920 — layout-independent QTE keyboard input for RU/EN key prompts.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #920 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/920
- **Parent / related issue(s)**: #911 QTE v2 epic, #918 Browser QTE parity epic
- **Issue type**: task / player-facing fairness bug / contract-sensitive QTE foundation
- **Spec Kit justification**: This issue spans console and browser player-facing input, QTE validation/documentation, GM authoring guidance, and future QTE v2 keyboard-heavy mini-games. The issue explicitly requests a Spec Kit feature before implementation.
- **Contract scope**: player-facing, GM-facing prompts, validation/docs/examples, console, browser, frontend
- **Out of scope**: Implementing new QTE v2 mini-game types from #912-#917, the Daren training scenario from #919, changing ordinary text input/layout behavior, and adding OS-specific global keyboard hooks. Unsupported platform limitations may be documented but should not expand this slice.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Console QTE accepts the intended physical key (Priority: P1)

A player reacting to a console QTE prompt for `Q`, `W`, `E`, `A`, `S`, `D`, or `Space` succeeds when pressing the corresponding physical key, even if the active keyboard layout emits Cyrillic characters such as `й`, `ц`, `у`, `ф`, `ы`, or `в`.

**Why this priority**: This is the core fairness bug. QTEs should test reaction and timing, not whether the player remembered to switch OS layout.

**Independent Test**: A deterministic console input-normalization test can feed Cyrillic fallback characters and verify they resolve to the intended Latin QTE key without depending on the host OS keyboard layout.

**Acceptance Scenarios**:

1. **Given** a QTE expects `Q`, **When** the console input fallback receives `й`, **Then** the QTE comparison treats it as `q`.
2. **Given** a QTE expects `A`, **When** the console input fallback receives `ф`, **Then** the QTE comparison treats it as `a`.
3. **Given** a QTE expects `Space`, **When** the player presses Space, **Then** normalization preserves it as Space and does not require a text-layout mapping.

---

### User Story 2 - Browser QTE prefers physical KeyboardEvent.code (Priority: P1)

A browser QTE receives `KeyboardEvent.code` for physical keys and resolves `KeyQ`, `KeyW`, `KeyE`, `KeyA`, `KeyS`, `KeyD`, and `Space` by physical key. If `code` is unavailable, it falls back to the produced character and applies the same RU/EN mapping.

**Why this priority**: Browser parity is part of the issue scope and future browser QTE mini-games must not reintroduce layout-sensitive behavior.

**Independent Test**: Frontend tests can dispatch synthetic keyboard events with both `code` and Cyrillic `key` values and verify normalized QTE input.

**Acceptance Scenarios**:

1. **Given** a browser QTE expects `W`, **When** `KeyboardEvent.code` is `KeyW` and `key` is `ц`, **Then** the browser treats the input as `w`.
2. **Given** `KeyboardEvent.code` is missing and `key` is `ц`, **When** the browser normalizes QTE input, **Then** it treats the input as `w`.
3. **Given** ordinary command/chat input, **When** the player types Cyrillic text, **Then** this QTE normalization is not applied to the command/chat text.

---

### User Story 3 - Prompts and docs explain physical keys without debug leakage (Priority: P2)

The QTE prompt clearly shows the intended physical key and RU fallback label, such as `Q / Й`, and explains briefly that QTEs read physical keys / support RU-EN layouts. GM-facing rules and examples explain that GM-authored QTE configs name gameplay checks and keys, not the player's keyboard layout.

**Why this priority**: The acceptance criteria require player-facing clarity and documentation. Prompt copy must prevent false instructions to switch OS layout.

**Independent Test**: Source/docs tests can assert the RU/EN note and examples exist; UI tests can assert player-facing prompt text contains physical/RU labels without API/DTO/debug wording.

**Acceptance Scenarios**:

1. **Given** a QTE displays a required key `Q`, **When** the prompt is rendered, **Then** it shows a concise `Q / Й`-style label.
2. **Given** docs describe QTE authoring, **When** a GM reads them, **Then** they understand the player layout is handled by the client and should not be encoded in GM-authored QTE config.
3. **Given** a platform cannot provide layout-independent physical input, **When** the QTE starts, **Then** the player sees a clear warning before the timer starts.

---

### Edge Cases

- Uppercase and lowercase Latin/Cyrillic fallback characters normalize equivalently.
- Unsupported characters remain distinct and do not accidentally match QTE keys.
- `Space`, whitespace, and Enter-style control keys are not confused with letter keys.
- QTE normalization is scoped to QTE key matching and does not alter ordinary text entry, command composition, save names, or GM-authored narrative text.
- If browser `event.code` reports a physical key that conflicts with `event.key`, the physical `code` wins for QTE matching.
- If a QTE action has no key prompt or is resolved by selecting a grade/button in the current v1 UI, the new normalization helper remains available without changing that action's semantics.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a shared QTE key-normalization concept for the keys `Q`, `W`, `E`, `A`, `S`, `D`, and `Space`.
- **FR-002**: The console QTE input path MUST normalize Cyrillic fallback characters `й`, `ц`, `у`, `ф`, `ы`, and `в` to `q`, `w`, `e`, `a`, `s`, and `d` for QTE matching.
- **FR-003**: The browser QTE input path MUST prefer physical `KeyboardEvent.code` values for `KeyQ`, `KeyW`, `KeyE`, `KeyA`, `KeyS`, `KeyD`, and `Space` when available.
- **FR-004**: The browser QTE input path MUST apply character fallback RU/EN normalization when physical code is unavailable.
- **FR-005**: Player-facing QTE prompts MUST display the intended physical key and supported RU fallback label in a concise form.
- **FR-006**: Player-facing QTE copy MUST NOT tell players to switch OS keyboard layout when the implementation supports layout-independent matching.
- **FR-007**: Documentation and GM-facing QTE guidance MUST explain physical-key matching and clarify that GM-authored QTE configs do not need player keyboard-layout data.
- **FR-008**: Tests MUST cover console fallback mapping and browser physical-code/fallback mapping without relying on the host OS keyboard layout.
- **FR-009**: Existing QTE v1 behavior MUST remain compatible; current `BranchChoice`, `TimingBar`, `PromptChain`, `BalanceMeter`, and `ChargeRelease` resolution must not regress.
- **FR-010**: The implementation MUST avoid applying QTE key normalization to ordinary player text input.

### Key Entities *(include if feature involves data)*

- **QTE key token**: A canonical gameplay key label such as `q`, `w`, `e`, `a`, `s`, `d`, or `space` used only for QTE matching/display.
- **Physical key code**: Browser keyboard-event code such as `KeyQ` or `Space`, preferred over produced text for QTE matching.
- **Fallback character**: Produced character such as `й` or `ц` used when the input API does not expose physical key code.
- **QTE prompt label**: Player-facing display form such as `Q / Й` that communicates the physical key and RU fallback.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused C# tests prove all required Cyrillic fallback mappings normalize to their Latin QTE keys.
- **SC-002**: Frontend tests prove `KeyboardEvent.code` wins over Cyrillic `key` values and fallback mapping works when `code` is absent.
- **SC-003**: QTE docs/examples contain explicit guidance that layout support is client-owned and GM configs do not encode player layouts.
- **SC-004**: Existing focused QTE, browser contract, and documentation validation tests pass after implementation.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|BrowserApiContractTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests"`
- **Documentation/contract verification**: The focused command above plus source/docs guards touched by QTE docs/examples.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`
- **Manual/player-facing verification**: Inspect console/browser QTE prompt copy and, where feasible, dispatch synthetic browser keyboard events for `KeyQ` with Cyrillic `key` fallback.

## Assumptions

- QTE v1 currently resolves some actions by submitted grade/button rather than real-time key capture; this issue may add reusable key-normalization/display helpers and tests even if full real-time mini-game consumers arrive in #912-#918.
- The required RU/EN mapping is the common ЙЦУКЕН mapping for the QTE keys explicitly named in #920, not a full keyboard layout conversion library.
- `Space` is handled as a physical/control key and does not need a Cyrillic fallback label.
- No ordinary command/composer/chat text path should call the QTE key-normalization helper.
