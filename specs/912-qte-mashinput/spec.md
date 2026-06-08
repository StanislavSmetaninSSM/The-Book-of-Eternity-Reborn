# Feature Specification: QTE v2 MashInput

**Feature Branch**: `work/912-qte-mashinput`

**Created**: 2026-06-09

**Status**: Draft

**Input**: GitHub issue #912 — MashInput QTE v2 mini-game for rapid repeated input.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #912 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/912
- **Parent / related issue(s)**: #911 QTE v2 epic, #920 layout-independent QTE key input foundation, #918 Browser QTE parity follow-up, #919 Daren training/showcase follow-up.
- **Issue type**: task / feature / player-facing QTE contract and console implementation.
- **Spec Kit justification**: This work changes the GM-authored QTE contract, validation, console player-facing interaction, examples, and documentation. It is explicitly marked contract-sensitive in the issue and must remain durable for later QTE v2 children.
- **Contract scope**: player-facing, GM-facing prompts, validation, docs, examples, console client, local QTE runtime resolution. Browser parity is documented as a follow-up through #918 unless an existing read-only/browser DTO surface needs non-interactive metadata to avoid regressions.
- **Out of scope**: PatternMemory (#913), RhythmPulse (#914), PrecisionChoice (#915), StealthNoise (#916), LockPinSet (#917), full Browser interactive mini-game parity (#918), and the Daren training scenario (#919). Do not change ordinary text-input normalization outside QTE surfaces.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - GM can author valid MashInput offers (Priority: P1)

A GM can write a QTE action with `check.type = "MashInput"` and a `check.config` object that defines the accepted physical key set, duration, target press count, and partial threshold. The validator accepts well-formed offers and reports precise errors for malformed ones.

**Why this priority**: Without a documented and validated contract, the console client and later browser parity would rely on fragile ad-hoc JSON.

**Independent Test**: Validation tests can build a minimal QTE offer containing a MashInput action, prove it is valid, then mutate one field at a time and assert the exact validation issue.

**Acceptance Scenarios**:

1. **Given** a MashInput check with `keys: ["space"]`, `durationMs: 2500`, `targetPresses: 12`, and `partialThreshold: 0.5`, **When** validation runs, **Then** no MashInput config error is produced.
2. **Given** a MashInput check with an empty `keys` array, **When** validation runs, **Then** validation reports that MashInput requires at least one supported QTE key.
3. **Given** a MashInput check with impossible `targetPresses`, invalid `durationMs`, or malformed `partialThreshold`, **When** validation runs, **Then** validation reports a field-specific error.

---

### User Story 2 - Console player can resolve MashInput locally (Priority: P1)

A player who accepts a QTE scene can play a MashInput action by rapidly pressing the configured key or keys before time expires. The local client resolves the action to `success`, `partial`, or `fail` without a follow-up GM turn.

**Why this priority**: The feature is not complete unless the new check type has local player-facing resolution.

**Independent Test**: Deterministic test hooks can feed key events and elapsed time into the MashInput resolver without real-time sleeps, proving success, partial, fail, and cancel behavior.

**Acceptance Scenarios**:

1. **Given** a MashInput action requiring 10 presses and a deterministic input sequence with 10 matching physical keys before the deadline, **When** the resolver runs, **Then** it returns `success`.
2. **Given** the same action and a deterministic input sequence that reaches the partial threshold but not the target, **When** the resolver runs, **Then** it returns `partial`.
3. **Given** Escape/cancel input during MashInput, **When** the resolver runs, **Then** it resolves safely as `fail` and returns to the normal QTE routing path.

---

### User Story 3 - Difficulty and characteristic influence MashInput fairly (Priority: P2)

MashInput uses `baseDifficulty` and `primaryCharacteristic` to adjust the effective target or tolerance so stronger/relevant characters receive a documented advantage while harder checks remain harder.

**Why this priority**: Existing QTE v1 checks already consider difficulty/stat tier; QTE v2 must preserve the same gameplay expectation.

**Independent Test**: Focused tests can compare effective MashInput requirements for low and high stat tiers at the same base config and assert monotonic behavior.

**Acceptance Scenarios**:

1. **Given** identical MashInput config and a higher relevant stat tier, **When** the effective requirement is computed, **Then** the target is not harder than for a lower tier.
2. **Given** a higher `baseDifficulty`, **When** the effective requirement is computed, **Then** success is not easier than at a lower difficulty for the same character.

---

### User Story 4 - GM-facing docs and examples teach MashInput (Priority: P2)

GM-facing QTE rules and the worked QTE example include MashInput config fields, limits, and a short scene example that demonstrates rapid repeated input without encoding player keyboard layout.

**Why this priority**: The GM prompt/docs are product behavior. The GM must be able to author the new contract correctly.

**Independent Test**: Documentation/source guard tests can assert that `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and validation manifest guidance mention MashInput required fields and QTE key-layout ownership.

**Acceptance Scenarios**:

1. **Given** the GM reads the QTE rules block, **When** they find MashInput, **Then** required fields and supported key tokens are documented.
2. **Given** the example QTE offer is validated, **When** the MashInput example is present, **Then** it passes the validator and demonstrates success/partial/fail routing.

### Edge Cases

- `keys` must contain only supported QTE key tokens from the existing layout-independent input contract: `q`, `w`, `e`, `a`, `s`, `d`, `space`.
- Duplicate keys are rejected or normalized deterministically; the behavior must be documented.
- `durationMs` must be an integer from `750` to `10000`; zero, negative, unrealistically tiny, and excessively long durations are invalid.
- `targetPresses` must be an integer from `1` to `80`; zero, negative, and values above `floor(durationMs / 1000 * 12)` are invalid.
- `partialThreshold` must be a numeric ratio greater than `0` and less than or equal to `1`; values that require more presses than success are invalid.
- Escape/cancel during active MashInput resolves as `fail`, not as a crash, hang, or partial state leak.
- Dynamic player/GM-authored labels and narrative text must remain escaped before Spectre.Console markup rendering.
- Existing QTE v1 types remain compatible and continue to validate/play as before.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The QTE offer contract MUST accept `check.type = "MashInput"` as a QTE v2 check type when its config is valid.
- **FR-002**: MashInput `check.config` MUST require `keys`, `durationMs`, `targetPresses`, and `partialThreshold`.
- **FR-003**: MashInput `keys` MUST use canonical QTE key tokens and MUST reuse #920 layout-independent physical-key/RU-EN matching for console key input.
- **FR-004**: Validation MUST reject empty key sets, unsupported key tokens, duplicate/ambiguous key sets, invalid durations, invalid target counts, and malformed thresholds with precise issue messages.
- **FR-005**: The console client MUST resolve MashInput locally to `success`, `partial`, or `fail` and route through existing QTE chapter/terminal outcome logic.
- **FR-006**: Escape/cancel during MashInput MUST resolve safely as `fail` without leaving an active scene in a broken state.
- **FR-007**: MashInput MUST expose deterministic test hooks or pure helper functions so success, partial, fail, difficulty/stat adjustment, and cancel behavior can be tested without fragile real-time sleeps.
- **FR-008**: `baseDifficulty` and `primaryCharacteristic` MUST influence MashInput effective target/tolerance in a documented monotonic way.
- **FR-009**: GM-facing QTE rules, examples, and prompt/source guards MUST describe MashInput required fields and player-key layout ownership.
- **FR-010**: Existing QTE v1 types and layout-independent key normalization from #920 MUST not regress.
- **FR-011**: Browser client surfaces MUST not claim interactive MashInput support until #918 implements it; if metadata is exposed, it must be read-only/player-facing and not duplicate gameplay resolution in React.

### Key Entities *(include if feature involves data)*

- **MashInput check**: A QTE action check with type `MashInput`, existing `baseDifficulty` and `primaryCharacteristic`, and a config object for rapid repeated input.
- **MashInput config**: `keys[]`, `durationMs`, `targetPresses`, `partialThreshold`, plus optional display/help copy only if already supported by the QTE action model.
- **Effective MashInput requirement**: The computed local target/tolerance after base config, difficulty, and characteristic tier are applied.
- **MashInput input event**: A deterministic representation of console key presses and cancel input used by runtime and tests.

### Implementation Contract Decisions

- Effective success target is `targetPresses + (baseDifficulty - 3) - statTier`, clamped to `1..80`.
- Effective partial target is `ceil(effectiveSuccessTarget * partialThreshold)`, clamped to `1..effectiveSuccessTarget`.
- Browser interactive MashInput parity remains #918; this issue keeps browser behavior non-interactive/manual-grade only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused validation tests cover one valid MashInput offer and malformed variants for key set, duration, target count, and partial threshold.
- **SC-002**: Focused QteSceneService tests prove deterministic MashInput success, partial, fail, cancel, and characteristic/difficulty adjustment behavior.
- **SC-003**: The worked QTE example includes a MashInput scene/action that remains valid under the project validation tests.
- **SC-004**: Existing QTE v1 focused tests and #920 key-normalization tests still pass.
- **SC-005**: GM-facing rules/docs explain MashInput without telling players to switch keyboard layout and without requiring GM-authored keyboard-layout data.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Same focused command above, plus any QTE source/documentation guard tests updated by the implementation.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if browser metadata or frontend QTE files are touched; otherwise run existing `test/qteLayoutInput.test.ts` only if frontend changes are made.
- **Manual/player-facing verification**: Inspect the console MashInput prompt copy in code/tests for clear Russian progress/timer/key labels and escaped dynamic text.

## Assumptions

- MashInput is the first QTE v2 check implemented under #911; it should establish reusable validation/resolution patterns for later child issues without implementing those children.
- The implementation can add pure helpers inside or near `QteSceneService` if extracting a small focused type improves deterministic testing.
- The existing browser QTE service currently submits grades for non-BranchChoice actions; full browser interactive MashInput is deferred to #918.
- The local console timer may use bounded loops for real play, but automated tests must avoid sleeping for wall-clock durations.
