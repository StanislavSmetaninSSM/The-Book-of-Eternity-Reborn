# Feature Specification: QTE v2 PatternMemory

**Feature Branch**: `work/913-qte-patternmemory`

**Created**: 2026-06-09

**Status**: Implemented locally; pending Hermes review, PR, merge, and issue closure

**Input**: GitHub issue #913 — PatternMemory QTE v2 mini-game for memorizing and repeating a short sequence.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #913 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/913
- **Parent / related issue(s)**: #911 QTE v2 epic, #920 layout-independent QTE key input foundation, #912 MashInput predecessor, #918 Browser QTE parity follow-up, #919 Daren training/showcase follow-up.
- **Issue type**: task / feature / player-facing QTE contract and console implementation.
- **Spec Kit justification**: This work changes the GM-authored QTE contract, validation, console player-facing interaction, examples, and documentation. The issue explicitly marks it as contract-sensitive and requires Spec Kit before implementation.
- **Contract scope**: player-facing, GM-facing prompts, validation, docs, examples, console client, local QTE runtime resolution. Browser parity is documented as a follow-up through #918 unless an existing read-only/browser DTO surface needs non-interactive metadata to avoid regressions.
- **Out of scope**: RhythmPulse (#914), PrecisionChoice (#915), StealthNoise (#916), LockPinSet (#917), full Browser interactive mini-game parity (#918), the Daren training scenario (#919), and ordinary text-input normalization outside QTE surfaces.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - GM can author valid PatternMemory offers (Priority: P1)

A GM can write a QTE action with `check.type = "PatternMemory"` and a `check.config` object that defines the repeatable key alphabet, sequence length, reveal time, input timeout, and mistake tolerance. The validator accepts well-formed offers and reports precise errors for malformed ones.

**Why this priority**: PatternMemory is a GM-authored contract; without validation and documentation, runtime behavior would be fragile and later browser parity would lack a stable contract.

**Independent Test**: Validation tests can build a minimal QTE offer containing a PatternMemory action, prove it is valid, then mutate one field at a time and assert the exact validation issue.

**Acceptance Scenarios**:

1. **Given** a PatternMemory check with `alphabet: ["q", "w", "e", "space"]`, `sequenceLength: 4`, `revealMs: 2500`, `inputTimeoutMs: 6000`, and `allowedMistakes: 1`, **When** validation runs, **Then** no PatternMemory config error is produced.
2. **Given** a PatternMemory check with an empty or duplicate alphabet, **When** validation runs, **Then** validation reports that PatternMemory requires unique supported QTE key tokens.
3. **Given** a PatternMemory check with impossible length, invalid reveal/input timeouts, or invalid mistake tolerance, **When** validation runs, **Then** validation reports a field-specific error.

---

### User Story 2 - Console player can resolve PatternMemory locally (Priority: P1)

A player who accepts a QTE scene can watch a clear reveal phase, then repeat the remembered sequence during an input phase. The local client resolves the action to `success`, `partial`, or `fail` without a follow-up GM turn.

**Why this priority**: The feature is not complete unless the new check type has local player-facing resolution and clear phase separation.

**Independent Test**: Deterministic test hooks can feed an expected sequence and input sequence into the PatternMemory resolver without real-time sleeps, proving success, partial, fail, timeout, and cancel behavior.

**Acceptance Scenarios**:

1. **Given** a PatternMemory action with a four-symbol sequence and deterministic input that exactly repeats it before timeout, **When** the resolver runs, **Then** it returns `success`.
2. **Given** the same action and deterministic input with mistakes within tolerance but not perfect, **When** the resolver runs, **Then** it returns `partial`.
3. **Given** the player enters too many mistakes, times out, or presses Escape/cancel, **When** the resolver runs, **Then** it resolves safely as `fail` and returns to the normal QTE routing path.

---

### User Story 3 - Difficulty and characteristic influence PatternMemory fairly (Priority: P2)

PatternMemory uses `baseDifficulty` and `primaryCharacteristic` to adjust effective sequence length, reveal time, input timeout, or mistake tolerance so stronger/relevant characters receive a documented advantage while harder checks remain harder.

**Why this priority**: Existing QTE checks consider difficulty/stat tier; QTE v2 must preserve the same gameplay expectation.

**Independent Test**: Focused tests can compare effective PatternMemory requirements for low and high stat tiers at the same base config and assert monotonic behavior.

**Acceptance Scenarios**:

1. **Given** identical PatternMemory config and a higher relevant stat tier, **When** effective requirements are computed, **Then** the check is not harder than for a lower tier.
2. **Given** a higher `baseDifficulty`, **When** effective requirements are computed, **Then** success is not easier than at lower difficulty for the same character/config.

---

### User Story 4 - GM-facing docs and examples teach PatternMemory (Priority: P2)

GM-facing QTE rules and the worked QTE example include PatternMemory config fields, limits, and a short magical lock/rune scene that demonstrates the reveal/input phases and success/partial/fail routing.

**Why this priority**: The GM prompt/docs are product behavior. The GM must be able to author the new contract correctly.

**Independent Test**: Documentation/source guard tests can assert that `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and validation manifest guidance mention PatternMemory required fields and QTE key-layout ownership.

**Acceptance Scenarios**:

1. **Given** the GM reads the QTE rules block, **When** they find PatternMemory, **Then** required fields and supported key tokens are documented.
2. **Given** the example QTE offer is validated, **When** the PatternMemory example is present, **Then** it passes the validator and demonstrates success/partial/fail routing.

### Edge Cases

- `alphabet` must contain unique supported QTE key tokens from the existing layout-independent input contract: `q`, `w`, `e`, `a`, `s`, `d`, `space`.
- `sequenceLength` must be an integer from `2` to `12` and must not exceed the configured alphabet-driven playable bounds.
- `revealMs` must be an integer from `500` to `15000`; extremely tiny and excessively long reveal phases are invalid.
- `inputTimeoutMs` must be an integer from `1000` to `30000` and must be large enough for the effective sequence length.
- `allowedMistakes` must be an integer from `0` to `sequenceLength - 1`; values that make failure impossible are invalid.
- Generated deterministic sequences must be stable in tests and playable in runtime; tests must not rely on wall-clock sleeps.
- Escape/cancel during reveal or input resolves as `fail`, not as a crash, hang, or partial state leak.
- Dynamic player/GM-authored labels and narrative text must remain escaped before Spectre.Console markup rendering.
- Existing QTE v1 types, #920 layout-independent key normalization, and #912 MashInput behavior remain compatible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The QTE offer contract MUST accept `check.type = "PatternMemory"` as a QTE v2 check type when its config is valid.
- **FR-002**: PatternMemory `check.config` MUST require `alphabet`, `sequenceLength`, `revealMs`, `inputTimeoutMs`, and `allowedMistakes`.
- **FR-003**: PatternMemory `alphabet` MUST use canonical QTE key tokens and MUST reuse #920 layout-independent physical-key/RU-EN matching for console key input.
- **FR-004**: Validation MUST reject missing/non-object config, empty/duplicate/unsupported alphabets, invalid sequence lengths, invalid reveal/input timeouts, and malformed mistake tolerance with precise issue messages.
- **FR-005**: The console client MUST resolve PatternMemory locally to `success`, `partial`, or `fail` and route through existing QTE chapter/terminal outcome logic.
- **FR-006**: The console implementation MUST separate reveal and input phases clearly in player-facing Russian copy.
- **FR-007**: Escape/cancel during PatternMemory MUST resolve safely as `fail` without leaving an active scene in a broken state.
- **FR-008**: PatternMemory MUST expose deterministic test hooks or pure helper functions so generated sequence, success, partial, fail, timeout, difficulty/stat adjustment, and cancel behavior can be tested without fragile real-time sleeps.
- **FR-009**: `baseDifficulty` and `primaryCharacteristic` MUST influence PatternMemory effective requirements in a documented monotonic way.
- **FR-010**: GM-facing QTE rules, examples, and prompt/source guards MUST describe PatternMemory required fields and player-key layout ownership.
- **FR-011**: Existing QTE v1 types, #920 key normalization, and #912 MashInput MUST not regress.
- **FR-012**: Browser client surfaces MUST not claim interactive PatternMemory support until #918 implements it; if metadata is exposed, it must be read-only/player-facing and not duplicate gameplay resolution in React.

### Key Entities *(include if feature involves data)*

- **PatternMemory check**: A QTE action check with type `PatternMemory`, existing `baseDifficulty` and `primaryCharacteristic`, and a config object for memory sequence play.
- **PatternMemory config**: `alphabet[]`, `sequenceLength`, `revealMs`, `inputTimeoutMs`, and `allowedMistakes`.
- **Effective PatternMemory requirement**: The computed local sequence length, reveal/input timing, and mistake tolerance after base config, difficulty, and characteristic tier are applied.
- **PatternMemory sequence**: Deterministically generated or selected canonical key token sequence shown during reveal and matched during input.
- **PatternMemory input event**: A deterministic representation of console key presses, elapsed time/timeout, and cancel input used by runtime and tests.

### Implementation Contract Decisions

- Effective sequence length is `sequenceLength + max(0, baseDifficulty - 3) - max(0, statTier / 2)` rounded/clamped to `2..12` and never below the authored base length minus two.
- Effective reveal time is `revealMs - ((baseDifficulty - 3) * 150) + (statTier * 100)`, clamped to `500..15000`.
- Effective input timeout is `inputTimeoutMs - ((baseDifficulty - 3) * 250) + (statTier * 150)`, clamped to `1000..30000` and not below `effectiveSequenceLength * 300` ms.
- Effective allowed mistakes is `allowedMistakes - max(0, baseDifficulty - 3) + max(0, statTier / 2)`, clamped to `0..effectiveSequenceLength - 1`.
- A perfect repeat resolves `success`; an imperfect repeat with mistakes at or below effective tolerance and at least half the sequence matched resolves `partial`; too many mistakes, timeout, or cancel resolves `fail`.
- Browser interactive PatternMemory parity remains #918; this issue keeps browser behavior non-interactive/manual-grade only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused validation tests cover one valid PatternMemory offer and malformed variants for alphabet, sequence length, reveal time, input timeout, and allowed mistakes.
- **SC-002**: Focused QteSceneService tests prove deterministic PatternMemory success, partial, fail, timeout/cancel, RU/EN key matching, and characteristic/difficulty adjustment behavior.
- **SC-003**: The worked QTE example includes a PatternMemory scene/action that remains valid under the project validation tests.
- **SC-004**: Existing QTE v1, #920 key-normalization, and #912 MashInput focused tests still pass.
- **SC-005**: GM-facing rules/docs explain PatternMemory without telling players to switch keyboard layout and without requiring GM-authored keyboard-layout data.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Same focused command above, plus any QTE source/documentation guard tests updated by the implementation.
- **Build verification**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if browser metadata or frontend QTE files are touched; otherwise no frontend gate is required.
- **Manual/player-facing verification**: Inspect the console PatternMemory reveal/input prompt copy in code/tests for clear Russian phase labels, timer/key labels, and escaped dynamic text.

## Assumptions

- MashInput (#912) already established reusable validation/resolution patterns for QTE v2 children and remains unchanged except for shared helper reuse if needed.
- The implementation can add pure helpers inside or near `QteSceneService` if extracting a small focused type improves deterministic testing.
- The existing browser QTE service currently submits grades for non-BranchChoice actions; full browser interactive PatternMemory is deferred to #918.
- Local console timer/input loops may use bounded runtime waits for play, but automated tests must avoid sleeping for wall-clock durations.
