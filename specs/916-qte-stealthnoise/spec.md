# Feature Specification: QTE v2 StealthNoise

**Feature Branch**: `work/916-qte-stealthnoise`

**Created**: 2026-06-10

**Status**: Drafted for autonomous implementation; pending TDD implementation, independent review, PR/merge, issue evidence comment, closure, and worktree cleanup.

**Input**: GitHub issue #916 — StealthNoise QTE v2 mini-game where the player keeps a noise meter below a danger threshold during infiltration.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #916 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/916
- **Parent / related issue(s)**: #911 QTE v2 epic; #920 layout-independent QTE input foundation; #912 MashInput, #913 PatternMemory, #914 RhythmPulse, #915 PrecisionChoice; #917 LockPinSet follow-up; #918 Browser QTE parity; #919/#925 training/practice follow-ups; #924 scoring/ranks follow-up.
- **Issue type**: task / feature / player-facing QTE contract and console implementation.
- **Spec Kit justification**: This work changes the GM-authored QTE contract, validation, console player-facing interaction, examples, and documentation. The issue explicitly marks it as contract-sensitive and requires Spec Kit before implementation.
- **Contract scope**: player-facing console QTE, GM-facing prompts/rules/API/example documentation, validation, local QTE runtime resolution. Browser interactive parity is out of scope for #918 unless existing read-only metadata must be kept honest.
- **Out of scope**: LockPinSet (#917), full Browser interactive mini-game parity (#918), the Daren training scenario (#919), scoring/ranks (#924), practice mode (#925), ordinary command/text input changes outside QTE surfaces, and a generic stealth/resource system beyond this QTE.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - GM can author valid StealthNoise offers (Priority: P1)

A GM can write a QTE action with `check.type = "StealthNoise"` and a `check.config` object describing duration, starting noise, danger threshold, passive noise drift, player recovery control, and grade thresholds. The validator accepts well-formed offers and reports precise errors for malformed ones.

**Why this priority**: StealthNoise is a GM-authored contract; without validation and documentation, runtime behavior would be fragile and later browser parity would lack a stable contract.

**Independent Test**: Validation tests can build a minimal QTE offer containing a StealthNoise action, prove it is valid, then mutate one field at a time and assert the exact validation issue.

**Acceptance Scenarios**:

1. **Given** a StealthNoise check with valid `durationMs`, `startingNoise`, `dangerThreshold`, `noiseDriftPerSecond`, `recoveryPerInput`, `allowedOverThresholdMs`, and `gradeThresholds`, **When** validation runs, **Then** no StealthNoise config error is produced.
2. **Given** a missing or malformed threshold, impossible duration, invalid recovery strength, non-monotonic grade thresholds, or missing route grade threshold, **When** validation runs, **Then** validation reports a field-specific StealthNoise error.
3. **Given** optional player-facing labels/hints, **When** validation runs, **Then** empty labels and raw technical values are rejected where the player would otherwise see unclear guidance.

---

### User Story 2 - Console player can play StealthNoise locally (Priority: P1)

A player who accepts a StealthNoise action sees a current noise meter, danger threshold, recovery input guidance, and remaining time. The local client applies drift and recovery deterministically and resolves to `success`, `partial`, or `fail` without a follow-up GM turn.

**Why this priority**: The feature is not complete unless the new check type has a distinct, player-facing local resolver rather than a manual grade picker.

**Independent Test**: Deterministic tests can feed a sequence of timestamped recovery inputs into a StealthNoise resolver without real-time sleeps, proving success, partial, fail, timeout/end-of-duration, cancellation, and over-threshold behavior.

**Acceptance Scenarios**:

1. **Given** a valid StealthNoise action and the player keeps noise below the threshold for the configured duration, **When** the resolver runs, **Then** it returns `success`.
2. **Given** the player briefly crosses the threshold but stays within the partial allowance, **When** the resolver runs, **Then** it returns `partial`.
3. **Given** the player exceeds the danger threshold too long, cancels, or the config is malformed, **When** the resolver runs, **Then** it resolves safely as `fail` without hanging or crashing.

---

### User Story 3 - Difficulty and characteristic influence StealthNoise fairly (Priority: P2)

StealthNoise uses `baseDifficulty` and `primaryCharacteristic` to adjust effective drift, threshold tolerance, recovery strength, or duration so stronger/relevant characters receive a documented advantage while harder checks remain harder.

**Why this priority**: Existing QTE checks consider difficulty/stat tier; QTE v2 must preserve the same gameplay expectation.

**Independent Test**: Focused tests can compare effective StealthNoise requirements for low and high stat tiers at the same base config and assert monotonic behavior.

**Acceptance Scenarios**:

1. **Given** identical StealthNoise config and a higher relevant stat tier, **When** effective requirements are computed, **Then** the check is not harder than for a lower tier.
2. **Given** a higher `baseDifficulty`, **When** effective requirements are computed, **Then** success is not easier than at lower difficulty for the same character/config.

---

### User Story 4 - GM-facing docs and examples teach StealthNoise (Priority: P2)

GM-facing QTE rules and the worked QTE example include StealthNoise config fields, limits, noise-meter semantics, grade thresholds, and an infiltration scene that demonstrates success/partial/fail routing.

**Why this priority**: The GM prompt/docs are product behavior. The GM must be able to author the new contract correctly.

**Independent Test**: Documentation/source guard tests can assert that `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, and `Examples/E_CLI_QTE_Offer.txt` mention StealthNoise required fields, invalid cases, recovery/noise semantics, and browser boundary.

**Acceptance Scenarios**:

1. **Given** the GM reads the QTE rules block, **When** they find StealthNoise, **Then** required fields, limits, and noise/recovery guidance are documented.
2. **Given** the example QTE offer is parsed by example validation, **When** the StealthNoise example is present, **Then** it remains valid JSON and demonstrates success/partial/fail routing.

### Edge Cases

- `durationMs` must be an integer from `1000` to `30000`; zero, negative, unrealistically tiny, and excessive durations are invalid.
- Noise values use a bounded `0..100` meter unless the implementation documents a narrower range.
- `startingNoise` must be `0..dangerThreshold` so a valid action does not begin in immediate failure.
- `dangerThreshold` must be greater than `0` and less than or equal to `100`.
- `noiseDriftPerSecond` must be positive; zero or negative drift would collapse the mini-game into a static meter.
- `recoveryPerInput` must be positive and must not exceed the whole meter range; excessive recovery is invalid.
- `allowedOverThresholdMs` must be `0..durationMs`.
- `gradeThresholds.successMaxNoise` and `gradeThresholds.partialMaxNoise` must be monotonic and must not make `partial` harder than `success`.
- If the player cancels during active StealthNoise, the result is `fail`, not a crash, hang, or partial state leak.
- Console layout must remain stable for long labels and must not require audio-only or browser-only affordances.
- Dynamic GM/player text must remain escaped before Spectre.Console markup rendering.
- Existing QTE v1 types, #920 key normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, and #915 PrecisionChoice behavior remain compatible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The QTE offer contract MUST accept `check.type = "StealthNoise"` as a QTE v2 check type when its config is valid.
- **FR-002**: StealthNoise `check.config` MUST require `durationMs`, `startingNoise`, `dangerThreshold`, `noiseDriftPerSecond`, `recoveryPerInput`, `allowedOverThresholdMs`, and `gradeThresholds`.
- **FR-003**: `gradeThresholds` MUST define deterministic success/partial/fail boundaries using the final noise value and/or accumulated over-threshold time.
- **FR-004**: Validation MUST reject missing/non-object config, impossible durations, invalid meter values, invalid drift/recovery, missing or non-monotonic grade thresholds, and malformed player-facing hints with precise issue messages.
- **FR-005**: The console client MUST resolve StealthNoise locally to `success`, `partial`, or `fail` and route through existing QTE chapter/terminal outcome logic.
- **FR-006**: The console implementation MUST show current noise, danger threshold, remaining time, recovery control guidance, and over-threshold warning without layout instability.
- **FR-007**: Cancellation during StealthNoise MUST resolve safely as `fail` without leaving an active scene in a broken state.
- **FR-008**: StealthNoise MUST expose deterministic test hooks or pure helper functions so drift/recovery, success, partial, fail, cancel, and difficulty/stat adjustment can be tested without fragile real-time sleeps.
- **FR-009**: `baseDifficulty` and `primaryCharacteristic` MUST influence StealthNoise effective requirements in a documented monotonic way.
- **FR-010**: GM-facing QTE rules, API documentation, examples, and prompt/source guards MUST describe StealthNoise required fields, grade thresholds, meter semantics, recovery controls, and browser boundary.
- **FR-011**: Existing QTE v1 types, #920 key normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, and #915 PrecisionChoice MUST not regress.
- **FR-012**: Browser client surfaces MUST not claim interactive StealthNoise support until #918 implements it; if metadata is exposed, it must be read-only/player-facing and not duplicate gameplay resolution in React.

### Key Entities *(include if feature involves data)*

- **StealthNoise check**: A QTE action check with type `StealthNoise`, existing `baseDifficulty` and `primaryCharacteristic`, and a config object for noise-meter play.
- **StealthNoise config**: `durationMs`, `startingNoise`, `dangerThreshold`, `noiseDriftPerSecond`, `recoveryPerInput`, `allowedOverThresholdMs`, `gradeThresholds`, and optional player-facing `prompt`/`recoveryLabel`/`warningLabel` fields.
- **StealthNoise state sample**: A deterministic representation of elapsed time, noise value, recovery input count/timestamps, and accumulated over-threshold time used by runtime and tests.
- **Effective StealthNoise requirement**: The computed duration, threshold, drift, recovery, and over-threshold allowance after base config, difficulty, and characteristic tier are applied.

### Implementation Contract Decisions

- StealthNoise uses the existing QTE action/check envelope and does not replace `BalanceMeter`; BalanceMeter remains balance/position semantics, while StealthNoise is stealth/noise pressure over time.
- Recommended config limits: `durationMs` `1000..30000`; `startingNoise` `0..100`; `dangerThreshold` `1..100`; `noiseDriftPerSecond` `1..100`; `recoveryPerInput` `1..100`; `allowedOverThresholdMs` `0..durationMs`.
- Recommended grade rule: `success` when final noise is at or below the authored success threshold and accumulated over-threshold time is within success allowance; `partial` when it stays within partial thresholds/allowance; otherwise `fail`.
- Recommended monotonic adjustment: higher difficulty increases drift or lowers over-threshold allowance; higher relevant stat increases recovery strength or raises allowance. Higher stat must not reduce help, and higher difficulty must not increase help for the same config.
- Recovery input should reuse existing QTE key helpers and RU/EN layout fallback where a physical key is used; GM-authored config must not encode keyboard layouts.
- Browser interactive StealthNoise parity remains #918; this issue keeps browser behavior non-interactive/manual-grade only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused validation tests cover one valid StealthNoise offer and malformed variants for duration, threshold, drift, recovery, over-threshold allowance, and grade thresholds.
- **SC-002**: Focused `QteSceneService` tests prove deterministic StealthNoise success, partial, fail, cancel, over-threshold, and characteristic/difficulty adjustment behavior.
- **SC-003**: The worked QTE example includes a StealthNoise infiltration scene that remains parseable under the project example validation tests.
- **SC-004**: Existing QTE v1, #920 key-normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, and #915 PrecisionChoice focused tests still pass.
- **SC-005**: GM-facing rules/docs explain StealthNoise as a stealth/noise mini-game distinct from BalanceMeter and do not require browser-only interactivity or GM-authored keyboard-layout data.

## Verification Plan *(mandatory)*

- **Baseline already observed before Spec Kit artifact creation**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` passed: 133 passed, 0 failed, 0 skipped.
- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: Same focused command above, plus any QTE source/documentation guard tests updated by the implementation.
- **Build verification**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- **Spec Kit verification**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- **Diff hygiene**: `git diff --check origin/main...HEAD` and an added-line static security scan over `origin/main...HEAD`.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if browser metadata or frontend QTE files are touched; otherwise no frontend gate is required.
- **Manual/player-facing verification**: Inspect console StealthNoise prompt copy in code/tests for stable Russian noise-meter labels, threshold warning, recovery guidance, and escaped dynamic text.

## Assumptions

- MashInput (#912), PatternMemory (#913), RhythmPulse (#914), and PrecisionChoice (#915) already established reusable validation/resolution patterns for QTE v2 children and remain unchanged except for shared helper reuse if needed.
- The implementation can add pure helpers inside or near `QteSceneService` if extracting a small focused type improves deterministic testing.
- The existing browser QTE service currently submits grades for non-BranchChoice actions; full browser interactive StealthNoise is deferred to #918.
- Local console timer/input loops may use bounded runtime waits for play, but automated tests must avoid sleeping for wall-clock durations.
