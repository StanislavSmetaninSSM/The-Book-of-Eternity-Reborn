# Feature Specification: QTE v2 RhythmPulse

**Feature Branch**: `work/914-qte-rhythmpulse`

**Created**: 2026-06-09

**Status**: Implemented locally; pending Hermes-owned independent review, PR/merge, issue evidence comment, closure, and worktree cleanup.

**Input**: GitHub issue #914 — RhythmPulse QTE v2 mini-game for pressing on pulse windows across a short rhythm pattern.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #914 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/914
- **Parent / related issue(s)**: #911 QTE v2 epic, #920 layout-independent QTE key input foundation, #912 MashInput predecessor, #913 PatternMemory predecessor, #918 Browser QTE parity follow-up, #919 Daren training/showcase follow-up.
- **Issue type**: task / feature / player-facing QTE contract and console implementation.
- **Spec Kit justification**: This work changes the GM-authored QTE contract, validation, console player-facing interaction, examples, and documentation. The issue explicitly marks it as contract-sensitive and requires Spec Kit before implementation.
- **Contract scope**: player-facing, GM-facing prompts, validation, docs, examples, console client, local QTE runtime resolution. Browser parity is documented as a follow-up through #918 unless an existing read-only/browser DTO surface needs non-interactive metadata to avoid regressions.
- **Out of scope**: PrecisionChoice (#915), StealthNoise (#916), LockPinSet (#917), full Browser interactive mini-game parity (#918), the Daren training scenario (#919), scoring/ranks (#924), practice/training modes (#925/#919), and ordinary text-input normalization outside QTE surfaces.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - GM can author valid RhythmPulse offers (Priority: P1)

A GM can write a QTE action with `check.type = "RhythmPulse"` and a `check.config` object that defines the number of pulses, beat interval, timing window, miss tolerance, and optional rhythm variation. The validator accepts well-formed offers and reports precise errors for malformed ones.

**Why this priority**: RhythmPulse is a GM-authored contract; without validation and documentation, runtime behavior would be fragile and later browser parity would lack a stable contract.

**Independent Test**: Validation tests can build a minimal QTE offer containing a RhythmPulse action, prove it is valid, then mutate one field at a time and assert the exact validation issue.

**Acceptance Scenarios**:

1. **Given** a RhythmPulse check with `pulseCount: 4`, `beatIntervalMs: 650`, `hitWindowMs: 120`, `allowedMisses: 1`, and `patternVariation: "steady"`, **When** validation runs, **Then** no RhythmPulse config error is produced.
2. **Given** a RhythmPulse check with zero or negative `pulseCount`, **When** validation runs, **Then** validation reports that the pulse count is outside the playable range.
3. **Given** a RhythmPulse check with invalid beat interval, overlapping hit windows, impossible miss tolerance, or unsupported pattern variation, **When** validation runs, **Then** validation reports a field-specific error.

---

### User Story 2 - Console player can resolve RhythmPulse locally (Priority: P1)

A player who accepts a QTE scene can watch a clear visual pulse track and press Space inside each pulse window. The local client resolves the action to `success`, `partial`, or `fail` without a follow-up GM turn and without relying only on audio.

**Why this priority**: The feature is not complete unless the new check type has local player-facing resolution and an accessibility fallback that is not purely audio-dependent.

**Independent Test**: Deterministic test hooks can feed pulse offsets and input offsets into the RhythmPulse resolver without real-time sleeps, proving success, partial, fail, no-input timeout, and cancel behavior.

**Acceptance Scenarios**:

1. **Given** a RhythmPulse action with four pulses and deterministic inputs inside all pulse windows, **When** the resolver runs, **Then** it returns `success`.
2. **Given** the same action and deterministic inputs that hit enough pulses for meaningful progress but miss the success tolerance, **When** the resolver runs, **Then** it returns `partial`.
3. **Given** the player presses too far outside the windows, gives no meaningful input by the end of the pulse pattern, or presses Escape/cancel, **When** the resolver runs, **Then** it resolves safely as `fail`.

---

### User Story 3 - Difficulty and characteristic influence RhythmPulse fairly (Priority: P2)

RhythmPulse uses `baseDifficulty` and `primaryCharacteristic` to adjust effective pulse count, hit window, or miss tolerance so stronger/relevant characters receive a documented advantage while harder checks remain harder.

**Why this priority**: Existing QTE checks consider difficulty/stat tier; QTE v2 must preserve the same gameplay expectation.

**Independent Test**: Focused tests can compare effective RhythmPulse requirements for low and high stat tiers at the same base config and assert monotonic behavior.

**Acceptance Scenarios**:

1. **Given** identical RhythmPulse config and a higher relevant stat tier, **When** effective requirements are computed, **Then** the check is not harder than for a lower tier.
2. **Given** a higher `baseDifficulty`, **When** effective requirements are computed, **Then** success is not easier than at lower difficulty for the same character/config.

---

### User Story 4 - GM-facing docs and examples teach RhythmPulse (Priority: P2)

GM-facing QTE rules and the worked QTE example include RhythmPulse config fields, limits, visual/accessibility guidance, and a short ritual or chase scene that demonstrates pulse timing and success/partial/fail routing.

**Why this priority**: The GM prompt/docs are product behavior. The GM must be able to author the new contract correctly.

**Independent Test**: Documentation/source guard tests can assert that `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, and `Examples/E_CLI_QTE_Offer.txt` mention RhythmPulse required fields, variation limits, visual fallback, and browser boundary.

**Acceptance Scenarios**:

1. **Given** the GM reads the QTE rules block, **When** they find RhythmPulse, **Then** required fields, limits, and visual pulse guidance are documented.
2. **Given** the example QTE offer is parsed by example validation, **When** the RhythmPulse example is present, **Then** it remains valid JSON and demonstrates success/partial/fail routing.

### Edge Cases

- `pulseCount` must be an integer from `2` to `16`; zero, negative, one-pulse non-patterns, and excessive pulse counts are invalid.
- `beatIntervalMs` must be an integer from `300` to `3000`; zero, negative, unrealistically tiny, and excessively slow intervals are invalid.
- `hitWindowMs` is the allowed early/late tolerance around each pulse and must be an integer from `40` to `1000`.
- `hitWindowMs * 2` must be strictly less than `beatIntervalMs` so adjacent pulse windows do not overlap.
- `allowedMisses` must be an integer from `0` to `pulseCount - 1`; values that make success possible without hitting any pulse are invalid.
- Optional `patternVariation` must be absent/null or one of `steady`, `accelerating`, or `swing`; unsupported strings and non-string values are invalid.
- Pattern variation must still generate a strictly increasing pulse schedule.
- Escape/cancel during active RhythmPulse resolves as `fail`, not as a crash, hang, or partial state leak.
- Console copy must show visual/textual pulse timing and progress so resolution is not purely audio-dependent.
- Dynamic player/GM-authored labels and narrative text must remain escaped before Spectre.Console markup rendering.
- Existing QTE v1 types, #920 layout-independent key normalization, #912 MashInput, and #913 PatternMemory behavior remain compatible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The QTE offer contract MUST accept `check.type = "RhythmPulse"` as a QTE v2 check type when its config is valid.
- **FR-002**: RhythmPulse `check.config` MUST require `pulseCount`, `beatIntervalMs`, `hitWindowMs`, and `allowedMisses`.
- **FR-003**: RhythmPulse `check.config.patternVariation` MUST be optional and, when present, MUST use one supported canonical variation token.
- **FR-004**: Validation MUST reject missing/non-object config, zero/negative/out-of-range pulse counts, invalid beat intervals, invalid or overlapping hit windows, impossible miss rules, and malformed/unsupported pattern variation with precise issue messages.
- **FR-005**: The console client MUST resolve RhythmPulse locally to `success`, `partial`, or `fail` and route through existing QTE chapter/terminal outcome logic.
- **FR-006**: The console implementation MUST communicate rhythm visually/textually and may use existing QTE audio cues only as enhancement, not as the sole signal.
- **FR-007**: Escape/cancel during RhythmPulse MUST resolve safely as `fail` without leaving an active scene in a broken state.
- **FR-008**: RhythmPulse MUST expose deterministic test hooks or pure helper functions so pulse schedule generation, success, partial, fail, no-input timeout, cancel, and difficulty/stat adjustment can be tested without fragile real-time sleeps.
- **FR-009**: `baseDifficulty` and `primaryCharacteristic` MUST influence RhythmPulse effective requirements in a documented monotonic way.
- **FR-010**: GM-facing QTE rules, API documentation, examples, and prompt/source guards MUST describe RhythmPulse required fields, pattern variation, visual fallback, and browser boundary.
- **FR-011**: Existing QTE v1 types, #920 key normalization, #912 MashInput, and #913 PatternMemory MUST not regress.
- **FR-012**: Browser client surfaces MUST not claim interactive RhythmPulse support until #918 implements it; if metadata is exposed, it must be read-only/player-facing and not duplicate gameplay resolution in React.

### Key Entities *(include if feature involves data)*

- **RhythmPulse check**: A QTE action check with type `RhythmPulse`, existing `baseDifficulty` and `primaryCharacteristic`, and a config object for pulse timing play.
- **RhythmPulse config**: `pulseCount`, `beatIntervalMs`, `hitWindowMs`, `allowedMisses`, and optional `patternVariation`.
- **Effective RhythmPulse requirement**: The computed pulse count, hit window, and miss tolerance after base config, difficulty, and characteristic tier are applied.
- **RhythmPulse schedule**: A deterministic list of pulse offsets in milliseconds generated from pulse count, beat interval, and pattern variation.
- **RhythmPulse input event**: A deterministic representation of console key presses with elapsed offsets and cancel input used by runtime and tests.

### Implementation Contract Decisions

- RhythmPulse uses Space as the local pulse key; the GM does not configure keyboard layout or physical key aliases for this check.
- Effective pulse count is `pulseCount + max(0, baseDifficulty - 3) - max(0, statTier / 2)`, clamped to `2..16` and never below authored `pulseCount - 2`.
- Effective hit window is `hitWindowMs - ((baseDifficulty - 3) * 10) + (statTier * 8)`, clamped to `40..1000` and never allowed to overlap adjacent pulse windows.
- Effective allowed misses is `allowedMisses - max(0, baseDifficulty - 3) + max(0, statTier / 2)`, clamped to `0..effectivePulseCount - 1`.
- A run resolves `success` when missed pulses are within effective allowed misses, `partial` when at least half of effective pulses are hit but success tolerance is missed, and `fail` otherwise.
- Browser interactive RhythmPulse parity remains #918; this issue keeps browser behavior non-interactive/manual-grade only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused validation tests cover one valid RhythmPulse offer and malformed variants for pulse count, beat interval, hit window, allowed misses, and pattern variation.
- **SC-002**: Focused QteSceneService tests prove deterministic RhythmPulse success, partial, fail, no-input timeout, cancel, schedule variation, and characteristic/difficulty adjustment behavior.
- **SC-003**: The worked QTE example includes a RhythmPulse ritual or chase scene that remains parseable under the project example validation tests.
- **SC-004**: Existing QTE v1, #920 key-normalization, #912 MashInput, and #913 PatternMemory focused tests still pass.
- **SC-005**: GM-facing rules/docs explain RhythmPulse visual/textual fallback and do not require audio-only timing or GM-authored keyboard-layout data.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Same focused command above, plus any QTE source/documentation guard tests updated by the implementation.
- **Build verification**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if browser metadata or frontend QTE files are touched; otherwise no frontend gate is required.
- **Manual/player-facing verification**: Inspect the console RhythmPulse pulse prompt copy in code/tests for clear Russian visual timing/progress labels, Space input guidance, and escaped dynamic text.

## Assumptions

- MashInput (#912) and PatternMemory (#913) already established reusable validation/resolution patterns for QTE v2 children and remain unchanged except for shared helper reuse if needed.
- RhythmPulse can use Space as the local pulse key because the issue contract names timing fields but does not require a configurable key set.
- The implementation can add pure helpers inside or near `QteSceneService` if extracting a small focused type improves deterministic testing.
- The existing browser QTE service currently submits grades for non-BranchChoice actions; full browser interactive RhythmPulse is deferred to #918.
- Local console timer/input loops may use bounded runtime waits for play, but automated tests must avoid sleeping for wall-clock durations.
