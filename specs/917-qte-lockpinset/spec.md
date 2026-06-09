# Feature Specification: QTE v2 LockPinSet

**Feature Branch**: `work/917-qte-lockpinset`

**Created**: 2026-06-10

**Status**: Drafted for autonomous implementation; pending TDD implementation, independent review, PR/merge, issue evidence comment, closure, and worktree cleanup.

**Input**: GitHub issue #917 — LockPinSet QTE v2 mini-game where the player sets lock pins/tumblers into correct windows under durability and timer pressure.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #917 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/917
- **Parent / related issue(s)**: #911 QTE v2 epic; #920 layout-independent QTE input foundation; #912 MashInput, #913 PatternMemory, #914 RhythmPulse, #915 PrecisionChoice, #916 StealthNoise; #918 Browser QTE parity; #919/#925 training/practice follow-ups; #924 scoring/ranks follow-up.
- **Issue type**: task / feature / player-facing QTE contract and console implementation.
- **Spec Kit justification**: This work changes the GM-authored QTE contract, validation, console player-facing interaction, examples, and documentation. The issue explicitly marks it as contract-sensitive and requires Spec Kit before implementation.
- **Contract scope**: player-facing console QTE, GM-facing prompts/rules/API/example documentation, validation, local QTE runtime resolution. Browser interactive parity is out of scope for #918 unless existing read-only metadata must be kept honest.
- **Out of scope**: full Browser interactive mini-game parity (#918), Daren training scenario (#919), scoring/ranks (#924), practice mode (#925), ordinary command/text input changes outside QTE surfaces, and a generic lockpicking/inventory/door subsystem beyond this QTE.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - GM can author valid LockPinSet offers (Priority: P1)

A GM can write a QTE action with `check.type = "LockPinSet"` and a `check.config` object describing pin count, per-pin target windows, pick durability/mistakes, timer pressure, drift/instability, and grade thresholds. The validator accepts well-formed offers and reports precise errors for malformed ones.

**Why this priority**: LockPinSet is a GM-authored contract; without validation and documentation, runtime behavior would be fragile and later browser parity would lack a stable contract.

**Independent Test**: Validation tests can build a minimal QTE offer containing a LockPinSet action, prove it is valid, then mutate one field at a time and assert the exact validation issue.

**Acceptance Scenarios**:

1. **Given** a LockPinSet check with valid `pinCount`, `pinWindows`, `timerMs`, `pickDurability`, `maxMistakes`, `pinDriftPerSecond`, and `gradeThresholds`, **When** validation runs, **Then** no LockPinSet config error is produced.
2. **Given** missing or malformed pin windows, unsupported pin counts, invalid durability/mistake limits, impossible timer pressure, missing routing, or non-monotonic grade thresholds, **When** validation runs, **Then** validation reports a field-specific LockPinSet error.
3. **Given** optional player-facing labels/hints, **When** validation runs, **Then** empty labels and raw technical values are rejected where the player would otherwise see unclear guidance.

---

### User Story 2 - Console player can play LockPinSet locally (Priority: P1)

A player who accepts a LockPinSet action sees each pin state, target-window feedback, remaining time, pick durability/mistakes, and clear controls. The local client applies pin movement/drift and mistake pressure deterministically and resolves to `success`, `partial`, or `fail` without a follow-up GM turn.

**Why this priority**: The feature is not complete unless the new check type has a distinct, player-facing local resolver rather than a manual grade picker.

**Independent Test**: Deterministic tests can feed a sequence of pin adjustments and timestamps into a LockPinSet resolver without real-time sleeps, proving clean success, noisy/slow partial success, failure, timeout, cancellation, and broken-pick behavior.

**Acceptance Scenarios**:

1. **Given** a valid LockPinSet action and the player sets all pins inside their target windows before timer/durability fail states, **When** the resolver runs, **Then** it returns `success`.
2. **Given** the player opens the lock slowly, with enough mistakes/noise for a complication but not total failure, **When** the resolver runs, **Then** it returns `partial`.
3. **Given** the player breaks the pick, exceeds mistakes, times out, cancels, or the config is malformed, **When** the resolver runs, **Then** it resolves safely as `fail` without hanging or crashing.

---

### User Story 3 - Difficulty and characteristic influence LockPinSet fairly (Priority: P2)

LockPinSet uses `baseDifficulty` and `primaryCharacteristic` to adjust effective pin count, window tolerance, drift, durability/mistake allowance, or timer so stronger/relevant characters receive a documented advantage while harder locks remain harder.

**Why this priority**: Existing QTE checks consider difficulty/stat tier; QTE v2 must preserve the same gameplay expectation.

**Independent Test**: Focused tests can compare effective LockPinSet requirements for low and high stat tiers at the same base config and assert monotonic behavior.

**Acceptance Scenarios**:

1. **Given** identical LockPinSet config and a higher relevant stat tier, **When** effective requirements are computed, **Then** the check is not harder than for a lower tier.
2. **Given** a higher `baseDifficulty`, **When** effective requirements are computed, **Then** success is not easier than at lower difficulty for the same character/config.

---

### User Story 4 - GM-facing docs and examples teach LockPinSet (Priority: P2)

GM-facing QTE rules and the worked QTE example include LockPinSet config fields, limits, pin-window semantics, grade thresholds, and a lockpicking scene that demonstrates success/partial/fail routing.

**Why this priority**: The GM prompt/docs are product behavior. The GM must be able to author the new contract correctly.

**Independent Test**: Documentation/source guard tests can assert that `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, and `Examples/E_CLI_QTE_Offer.txt` mention LockPinSet required fields, invalid cases, pin-window/durability semantics, and browser boundary.

**Acceptance Scenarios**:

1. **Given** the GM reads the QTE rules block, **When** they find LockPinSet, **Then** required fields, limits, and pin/durability guidance are documented.
2. **Given** the example QTE offer is parsed by example validation, **When** the LockPinSet example is present, **Then** it remains valid JSON and demonstrates success/partial/fail routing.

### Edge Cases

- `pinCount` must be an integer from `2` to `8`; unsupported counts are invalid.
- `pinWindows` must contain exactly `pinCount` windows; each window must have ordered numeric `min`/`max` bounds within the pin track.
- A valid pin window must have a nonzero width and must fit inside the supported `0..100` pin-position range.
- `timerMs` must be an integer from `1000` to `60000`; zero, negative, unrealistically tiny, and excessive timers are invalid.
- `pickDurability` must be positive; zero or negative durability makes the mini-game unwinnable.
- `maxMistakes` must be `0..pickDurability` or otherwise not exceed the authored durability model.
- `pinDriftPerSecond` must be non-negative and bounded; excessive drift that makes all windows impossible is invalid.
- `gradeThresholds` must define monotonic success/partial boundaries for completion time, mistakes/noise, and broken-pick state.
- Missing `routing.success`, `routing.partial`, or `routing.fail` remains invalid for this tri-grade QTE.
- If the player cancels during active LockPinSet, the result is `fail`, not a crash, hang, or partial state leak.
- Console layout must remain stable for long labels and must not require audio-only or browser-only affordances.
- Dynamic GM/player text must remain escaped before Spectre.Console markup rendering.
- Existing QTE v1 types, #920 key normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, #915 PrecisionChoice, and #916 StealthNoise behavior remain compatible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The QTE offer contract MUST accept `check.type = "LockPinSet"` as a QTE v2 check type when its config is valid.
- **FR-002**: LockPinSet `check.config` MUST require `pinCount`, `pinWindows`, `timerMs`, `pickDurability`, `maxMistakes`, `pinDriftPerSecond`, and `gradeThresholds`.
- **FR-003**: `pinWindows` MUST define one target window per pin with ordered `min`/`max` bounds in a documented pin-position range.
- **FR-004**: `gradeThresholds` MUST define deterministic clean success, noisy/slow partial success, and failure boundaries using opened-pin count, elapsed time, mistakes/noise, and pick/broken state.
- **FR-005**: Validation MUST reject missing/non-object config, impossible pin windows, unsupported pin counts, invalid timer/durability/mistake/drift values, missing routing, and malformed/non-monotonic grade thresholds with precise issue messages.
- **FR-006**: The console client MUST resolve LockPinSet locally to `success`, `partial`, or `fail` and route through existing QTE chapter/terminal outcome logic.
- **FR-007**: The console implementation MUST show each pin state, target-window feedback, remaining time, pick durability/mistakes, and control guidance without layout instability.
- **FR-008**: Cancellation during LockPinSet MUST resolve safely as `fail` without leaving an active scene in a broken state.
- **FR-009**: LockPinSet MUST expose deterministic test hooks or pure helper functions so pin movement, windows, mistakes, timeout, cancel, broken pick, and difficulty/stat adjustment can be tested without fragile real-time sleeps.
- **FR-010**: `baseDifficulty` and `primaryCharacteristic` MUST influence LockPinSet effective requirements in a documented monotonic way.
- **FR-011**: GM-facing QTE rules, API documentation, examples, and prompt/source guards MUST describe LockPinSet required fields, grade thresholds, pin-window semantics, durability/mistakes, timer pressure, and browser boundary.
- **FR-012**: Existing QTE v1 types, #920 key normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, #915 PrecisionChoice, and #916 StealthNoise MUST not regress.
- **FR-013**: Browser client surfaces MUST not claim interactive LockPinSet support until #918 implements it; if metadata is exposed, it must be read-only/player-facing and not duplicate gameplay resolution in React.

### Key Entities *(include if feature involves data)*

- **LockPinSet check**: A QTE action check with type `LockPinSet`, existing `baseDifficulty` and `primaryCharacteristic`, and a config object for lockpicking play.
- **LockPinSet config**: `pinCount`, `pinWindows`, `timerMs`, `pickDurability`, `maxMistakes`, `pinDriftPerSecond`, `gradeThresholds`, and optional player-facing `prompt`/`pinLabel`/`durabilityLabel`/`warningLabel` fields.
- **Pin window**: A target interval for a pin on a bounded track, with `min`, `max`, and optional player-facing `label`.
- **LockPinSet state sample**: A deterministic representation of elapsed time, pin positions, locked/open pins, mistake count, pick durability, and timeout/cancel state used by runtime and tests.
- **Effective LockPinSet requirement**: The computed pin count, target-window tolerance, drift, durability/mistake allowance, and timer after base config, difficulty, and characteristic tier are applied.

### Implementation Contract Decisions

- LockPinSet uses the existing QTE action/check envelope and does not add a new inventory lockpicking subsystem or persistent lock state.
- Recommended config limits: `pinCount` `2..8`; pin positions and windows use `0..100`; `timerMs` `1000..60000`; `pickDurability` `1..20`; `maxMistakes` `0..pickDurability`; `pinDriftPerSecond` `0..100`.
- Recommended grade rule: `success` when all pins are set/opened inside their target windows within the success time/mistake thresholds and the pick is not broken; `partial` when the lock opens but time or mistakes/noise exceed clean thresholds; otherwise `fail`.
- Recommended monotonic adjustment: higher difficulty narrows effective windows, increases drift, or reduces timer/durability allowance; higher relevant stat widens windows, reduces drift, or increases durability/timer allowance. Higher stat must not reduce help, and higher difficulty must not increase help for the same config.
- Input should reuse existing QTE key helpers and RU/EN layout fallback where physical keys are used; GM-authored config must not encode keyboard layouts.
- Browser interactive LockPinSet parity remains #918; this issue keeps browser behavior non-interactive/manual-grade only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused validation tests cover one valid LockPinSet offer and malformed variants for pin count, pin windows, timer, durability, mistakes, drift, routing, and grade thresholds.
- **SC-002**: Focused `QteSceneService` tests prove deterministic LockPinSet clean success, slow/noisy partial, fail, cancel, timeout, broken-pick, and characteristic/difficulty adjustment behavior.
- **SC-003**: The worked QTE example includes a LockPinSet lockpicking scene that remains parseable under the project example validation tests.
- **SC-004**: Existing QTE v1, #920 key-normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, #915 PrecisionChoice, and #916 StealthNoise focused tests still pass.
- **SC-005**: GM-facing rules/docs explain LockPinSet as a lockpicking/pin mini-game distinct from BalanceMeter/PrecisionChoice and do not require browser-only interactivity or GM-authored keyboard-layout data.

## Verification Plan *(mandatory)*

- **Baseline observed before Spec Kit artifact creation**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` passed: 163 passed, 0 failed, 0 skipped.
- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Same focused command above, plus any QTE source/documentation guard tests updated by the implementation.
- **Build verification**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- **Spec Kit verification**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- **Diff hygiene**: `git diff --check origin/main...HEAD` and an added-line static security scan over `origin/main...HEAD`.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if browser metadata or frontend QTE files are touched; otherwise no frontend gate is required.
- **Manual/player-facing verification**: Inspect console LockPinSet prompt copy in code/tests for stable Russian pin-state labels, durability/mistake/timer guidance, and escaped dynamic text.

## Assumptions

- MashInput (#912), PatternMemory (#913), RhythmPulse (#914), PrecisionChoice (#915), and StealthNoise (#916) already established reusable validation/resolution patterns for QTE v2 children and remain unchanged except for shared helper reuse if needed.
- The implementation can add pure helpers inside or near `QteSceneService` if extracting a small focused type improves deterministic testing.
- The existing browser QTE service currently submits grades for non-BranchChoice actions; full browser interactive LockPinSet is deferred to #918.
- Local console timer/input loops may use bounded runtime waits for play, but automated tests must avoid sleeping for wall-clock durations.
