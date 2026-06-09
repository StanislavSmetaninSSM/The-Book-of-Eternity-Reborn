# Feature Specification: QTE v2 PrecisionChoice

**Feature Branch**: `work/915-qte-precisionchoice`

**Created**: 2026-06-10

**Status**: Drafted for autonomous implementation; pending TDD implementation, independent review, PR/merge, issue evidence comment, closure, and worktree cleanup.

**Input**: GitHub issue #915 — PrecisionChoice QTE v2 mini-game where the player chooses the correct option under time pressure.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #915 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/915
- **Parent / related issue(s)**: #911 QTE v2 epic, #920 layout-independent QTE input foundation, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, #918 Browser QTE parity follow-up, #919/#925 training/practice follow-ups, #924 scoring/ranks follow-up.
- **Issue type**: task / feature / player-facing QTE contract and console implementation.
- **Spec Kit justification**: This work changes the GM-authored QTE contract, validation, console player-facing interaction, examples, and documentation. The issue explicitly marks it as contract-sensitive and requires Spec Kit before implementation.
- **Contract scope**: player-facing console QTE, GM-facing prompts/rules/API/example documentation, validation, local QTE runtime resolution. Browser interactive parity is out of scope for #918 unless existing read-only metadata must be kept honest.
- **Out of scope**: StealthNoise (#916), LockPinSet (#917), full Browser interactive mini-game parity (#918), the Daren training scenario (#919), scoring/ranks (#924), practice mode (#925), and ordinary command/text input changes outside QTE surfaces.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - GM can author valid PrecisionChoice offers (Priority: P1)

A GM can write a QTE action with `check.type = "PrecisionChoice"` and a `check.config` object containing timed choices, the correct choice, grade mapping for choices, timeout behavior, and optional decoy hints. The validator accepts well-formed offers and reports precise errors for malformed ones.

**Why this priority**: PrecisionChoice is a GM-authored contract; without validation and documentation, runtime behavior would be fragile and later browser parity would lack a stable contract.

**Independent Test**: Validation tests can build a minimal QTE offer containing a PrecisionChoice action, prove it is valid, then mutate one field at a time and assert the exact validation issue.

**Acceptance Scenarios**:

1. **Given** a PrecisionChoice check with three unique choices, one `correctChoiceId`, a playable `timeoutMs`, `timeoutGrade: "fail"`, and optional decoy hints, **When** validation runs, **Then** no PrecisionChoice config error is produced.
2. **Given** duplicate choice ids, a missing correct choice, an invalid grade token, or an impossible timeout, **When** validation runs, **Then** validation reports a field-specific PrecisionChoice error.
3. **Given** optional decoy hints that reference unknown choices or use malformed values, **When** validation runs, **Then** validation rejects the malformed hints without accepting ambiguous player guidance.

---

### User Story 2 - Console player can resolve PrecisionChoice locally (Priority: P1)

A player who accepts a QTE scene sees a stable list of choices and a visible timer. The local client resolves the selection to `success`, `partial`, or `fail` without a follow-up GM turn, and timeout resolves deterministically according to config, normally `fail` or `partial`.

**Why this priority**: The feature is not complete unless the new check type has local player-facing resolution and deterministic timeout behavior.

**Independent Test**: Deterministic tests can feed selected choice ids and elapsed times into the PrecisionChoice resolver without real-time sleeps, proving success, partial, fail, timeout, invalid selection, and cancel behavior.

**Acceptance Scenarios**:

1. **Given** a valid PrecisionChoice action and the player selects the correct choice before timeout, **When** the resolver runs, **Then** it returns `success`.
2. **Given** the player selects a partial/near-miss choice before timeout, **When** the resolver runs, **Then** it returns `partial`.
3. **Given** the player selects a fail choice, selects nothing before timeout, selects an unknown choice, or cancels, **When** the resolver runs, **Then** it resolves safely as `fail` unless the authored `timeoutGrade` is `partial` for the timeout-only path.

---

### User Story 3 - Difficulty and characteristic influence PrecisionChoice fairly (Priority: P2)

PrecisionChoice uses `baseDifficulty` and `primaryCharacteristic` to adjust effective timeout and hint clarity so stronger/relevant characters receive a documented advantage while harder checks remain harder.

**Why this priority**: Existing QTE checks consider difficulty/stat tier; QTE v2 must preserve the same gameplay expectation.

**Independent Test**: Focused tests can compare effective PrecisionChoice requirements for low and high stat tiers at the same base config and assert monotonic behavior.

**Acceptance Scenarios**:

1. **Given** identical PrecisionChoice config and a higher relevant stat tier, **When** effective requirements are computed, **Then** the check is not harder than for a lower tier.
2. **Given** a higher `baseDifficulty`, **When** effective requirements are computed, **Then** success is not easier than at lower difficulty for the same character/config.

---

### User Story 4 - GM-facing docs and examples teach PrecisionChoice (Priority: P2)

GM-facing QTE rules and the worked QTE example include PrecisionChoice config fields, limits, timer/choice guidance, hint behavior, and a short chase or trap scene that demonstrates success/partial/fail routing.

**Why this priority**: The GM prompt/docs are product behavior. The GM must be able to author the new contract correctly.

**Independent Test**: Documentation/source guard tests can assert that `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, and `Examples/E_CLI_QTE_Offer.txt` mention PrecisionChoice required fields, invalid cases, timer fallback, and browser boundary.

**Acceptance Scenarios**:

1. **Given** the GM reads the QTE rules block, **When** they find PrecisionChoice, **Then** required fields, limits, and timer/choice guidance are documented.
2. **Given** the example QTE offer is parsed by example validation, **When** the PrecisionChoice example is present, **Then** it remains valid JSON and demonstrates success/partial/fail routing.

### Edge Cases

- `choices` must be an array with `2` to `8` entries; fewer, excessive, non-object entries, or duplicate ids are invalid.
- Choice ids must be non-empty stable tokens and must be unique within the PrecisionChoice config.
- Each choice must have a player-facing label and a grade mapping token of `success`, `partial`, or `fail`.
- `correctChoiceId` must reference exactly one configured choice and that choice must map to `success`.
- At least one non-success choice should exist so the QTE is a meaningful timed choice rather than an unconditional success.
- `timeoutMs` must be an integer from `1000` to `30000`; zero, negative, unrealistically tiny, and excessive timers are invalid.
- `timeoutGrade` is optional and may be `fail` or `partial`; timeout must not resolve as `success`.
- `decoyHints` is optional. When present, each hint must reference an existing non-success choice and contain non-empty player-facing text; unknown choice references are invalid.
- Escape/cancel during active PrecisionChoice resolves as `fail`, not as a crash, hang, or partial state leak.
- Console layout must remain stable for short/long labels and must not require audio-only or browser-only affordances.
- Dynamic player/GM-authored labels and narrative text must remain escaped before Spectre.Console markup rendering.
- Existing QTE v1 types, #920 key normalization, #912 MashInput, #913 PatternMemory, and #914 RhythmPulse behavior remain compatible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The QTE offer contract MUST accept `check.type = "PrecisionChoice"` as a QTE v2 check type when its config is valid.
- **FR-002**: PrecisionChoice `check.config` MUST require `choices`, `correctChoiceId`, and `timeoutMs`; `timeoutGrade` and `decoyHints` are optional.
- **FR-003**: Each choice MUST define a unique `id`, a player-facing `label`, and a `grade` token of `success`, `partial`, or `fail`.
- **FR-004**: Validation MUST reject missing/non-object config, missing or duplicate choices, missing correct choice, invalid grade mapping, impossible timers, invalid timeout grade, and malformed decoy hints with precise issue messages.
- **FR-005**: The console client MUST resolve PrecisionChoice locally to `success`, `partial`, or `fail` and route through existing QTE chapter/terminal outcome logic.
- **FR-006**: The console implementation MUST show stable choices, a visible timer/remaining-time cue, input guidance, and optional decoy hints without layout instability.
- **FR-007**: Timeout MUST resolve deterministically according to config (`fail` by default, optionally `partial`) and MUST NOT resolve as `success`.
- **FR-008**: Escape/cancel during PrecisionChoice MUST resolve safely as `fail` without leaving an active scene in a broken state.
- **FR-009**: PrecisionChoice MUST expose deterministic test hooks or pure helper functions so success, partial, fail, timeout, cancel, and difficulty/stat adjustment can be tested without fragile real-time sleeps.
- **FR-010**: `baseDifficulty` and `primaryCharacteristic` MUST influence PrecisionChoice effective timeout/hint clarity in a documented monotonic way.
- **FR-011**: GM-facing QTE rules, API documentation, examples, and prompt/source guards MUST describe PrecisionChoice required fields, grade mapping, timeout behavior, decoy hints, and browser boundary.
- **FR-012**: Existing QTE v1 types, #920 key normalization, #912 MashInput, #913 PatternMemory, and #914 RhythmPulse MUST not regress.
- **FR-013**: Browser client surfaces MUST not claim interactive PrecisionChoice support until #918 implements it; if metadata is exposed, it must be read-only/player-facing and not duplicate gameplay resolution in React.

### Key Entities *(include if feature involves data)*

- **PrecisionChoice check**: A QTE action check with type `PrecisionChoice`, existing `baseDifficulty` and `primaryCharacteristic`, and a config object for timed selection play.
- **PrecisionChoice choice**: A configured option with stable `id`, player-facing `label`, optional detail/hint copy, and grade mapping.
- **PrecisionChoice config**: `choices`, `correctChoiceId`, `timeoutMs`, optional `timeoutGrade`, and optional `decoyHints`.
- **Effective PrecisionChoice requirement**: The computed timeout and hint clarity after base config, difficulty, and characteristic tier are applied.
- **PrecisionChoice input event**: A deterministic representation of a selected choice id, elapsed time, or cancel input used by runtime and tests.

### Implementation Contract Decisions

- PrecisionChoice uses the existing QTE action/check envelope and does not replace `BranchChoice`.
- `BranchChoice` remains a branch/grade selection surface; PrecisionChoice is a timed local QTE resolver where selected option, elapsed time, and timeout behavior determine the grade.
- Effective timeout is `timeoutMs - ((baseDifficulty - 3) * 300) + (statTier * 250)`, clamped to `1000..30000` and never below half the authored timeout.
- Effective hint clarity is monotonic: higher stat tier may reveal/strengthen decoy hints, while higher difficulty may hide/soften them; higher stat must not reduce help and higher difficulty must not increase help for the same config.
- Timeout resolves as configured by `timeoutGrade` when present, otherwise `fail`; `success` is invalid for timeout.
- Browser interactive PrecisionChoice parity remains #918; this issue keeps browser behavior non-interactive/manual-grade only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused validation tests cover one valid PrecisionChoice offer and malformed variants for choices, duplicate ids, correct choice, grade mapping, timeout, timeout grade, and decoy hints.
- **SC-002**: Focused `QteSceneService` tests prove deterministic PrecisionChoice success, partial, fail, timeout, cancel, invalid selection, and characteristic/difficulty adjustment behavior.
- **SC-003**: The worked QTE example includes a PrecisionChoice chase or trap scene that remains parseable under the project example validation tests.
- **SC-004**: Existing QTE v1, #920 key-normalization, #912 MashInput, #913 PatternMemory, and #914 RhythmPulse focused tests still pass.
- **SC-005**: GM-facing rules/docs explain PrecisionChoice timer/choice fallback and do not require browser-only interactivity or GM-authored keyboard-layout data.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Same focused command above, plus any QTE source/documentation guard tests updated by the implementation.
- **Build verification**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if browser metadata or frontend QTE files are touched; otherwise no frontend gate is required.
- **Manual/player-facing verification**: Inspect the console PrecisionChoice prompt copy in code/tests for stable Russian choice labels, timer guidance, optional hints, and escaped dynamic text.

## Assumptions

- MashInput (#912), PatternMemory (#913), and RhythmPulse (#914) already established reusable validation/resolution patterns for QTE v2 children and remain unchanged except for shared helper reuse if needed.
- The implementation can add pure helpers inside or near `QteSceneService` if extracting a small focused type improves deterministic testing.
- The existing browser QTE service currently submits grades for non-BranchChoice actions; full browser interactive PrecisionChoice is deferred to #918.
- Local console timer/input loops may use bounded runtime waits for play, but automated tests must avoid sleeping for wall-clock durations.
