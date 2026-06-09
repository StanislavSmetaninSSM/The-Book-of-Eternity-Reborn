# Implementation Plan: QTE v2 StealthNoise

**Branch**: `work/916-qte-stealthnoise` | **Date**: 2026-06-10 | **Spec**: `specs/916-qte-stealthnoise/spec.md`

**Input**: Feature specification from `specs/916-qte-stealthnoise/spec.md`.

## Summary

Implement GitHub issue #916 by adding a contract-validated `StealthNoise` QTE v2 check, deterministic local console noise-meter resolution, and synchronized GM-facing rules/examples. Reuse the #912 MashInput, #913 PatternMemory, #914 RhythmPulse, and #915 PrecisionChoice QTE v2 patterns, keep console noise/threshold display stable and accessible, and leave full browser interactivity out of scope for #918.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite only if browser metadata changes are required.

**Primary Dependencies**: Existing `QteSceneService`, `QteKeyInput`, `ValidationService`, `FileSystemManager`, Spectre.Console, xUnit, shared JSON models.

**Storage**: Existing file-backed QTE offer/runtime/history JSON files; no new persistent canonical state is expected beyond existing QTE runtime/history surfaces.

**Testing**: xUnit via `dotnet test`; frontend `npm run verify` only if frontend files change.

**Target Platform**: Local Windows console/browser client, with deterministic tests that do not require real keyboard timing, audio, browser automation, or wall-clock sleeps.

**Project Type**: Local game client and GM-authored JSON contract.

**Performance Goals**: StealthNoise console loop should remain responsive during short infiltration checks and deterministic helpers should run instantly in tests.

**Constraints**: No cloud dependencies, no telemetry, no ordinary text-input normalization, no real-time sleeps in automated tests, no browser gameplay duplication in this slice, no unstable console layouts for noise-meter surfaces.

**Scale/Scope**: One QTE v2 mini-game type, its validation/docs/examples/tests, and compatibility with existing QTE v1 types, #920 layout normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, and #915 PrecisionChoice.

**Source Issue(s)**: #916 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/916; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Contract Scope**: player-facing console QTE, GM-facing prompts/docs/examples, validation, local QTE runtime resolution.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- `git diff --check origin/main...HEAD`
- Added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/plan recipe false positives.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` only if frontend files are touched.

## Constitution Check

- **GitHub traceability**: Pass. `spec.md`, `plan.md`, and `tasks.md` link #916 and parent #911.
- **Spec Kit fit**: Pass. This is player-facing, contract-sensitive, validation/docs/examples work and the issue explicitly requests Spec Kit.
- **Player-facing integrity**: Pass planned. Console prompts must use Russian in-world copy, provide stable noise/threshold/recovery guidance, and avoid debug/API leakage; browser surfaces must not falsely claim completed parity.
- **Contract/state authority**: Pass planned. GM-facing `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and related documentation/source guards must change with the contract.
- **Test-first path**: Pass planned. Add failing validation and QteSceneService tests before implementation.
- **Verification evidence**: Pass planned. Focused QTE/docs tests, build, prerequisite check, diff check, and static scan are required before commit.
- **Agent orchestration**: Pass planned. Hermes delegates implementation to Codex with this spec/plan/tasks, constitution, issue criteria, and Superpowers TDD/review requirements.

## Project Structure

### Documentation (this feature)

```text
specs/916-qte-stealthnoise/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── stealthnoise-qte-contract.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.cs
BookOfEternityClient/Services/QteKeyInput.cs only if shared display/input helpers need reuse
BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs
BookOfEternityClient.Tests/QteSceneServiceTests.cs
BookOfEternityClient.Tests/ValidationServiceQteTests.cs
BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs
CLI_API_Specification.md
Rules/Block_CLI_QTE.txt
Examples/E_CLI_QTE_Offer.txt
Examples/example_validation_manifest.json if the changed example needs manifest registration
BookOfEternityClient.WebFrontend/* only if existing read-only QTE metadata must be adjusted to avoid false browser claims
```

**Structure Decision**: Keep the StealthNoise implementation close to existing QTE runtime/validation code and reuse the QTE v2 shape established by MashInput, PatternMemory, RhythmPulse, and PrecisionChoice. Extract small focused helpers only if they keep deterministic tests clean and avoid further growth in `QteSceneService.cs`. Do not introduce a separate gameplay engine, stealth resource subsystem, or browser-side resolver in this slice.

## Complexity Tracking

No constitution violations are planned. The feature is cross-contract but bounded to one QTE v2 type.

## Implementation Approach

1. Establish baseline by reading issue #916/#911, #912/#913/#914/#915/#920 artifacts, constitution, QTE service/validation/tests/docs/examples, and current worktree status.
2. Add RED validation tests for valid StealthNoise config and malformed duration/threshold/drift/recovery/allowance/grade-threshold cases.
3. Add RED local resolution tests for success, partial, fail, cancel, over-threshold, and difficulty/stat adjustment using deterministic helpers.
4. Implement the minimal contract model/validation and console resolver to satisfy tests while preserving v1 QTE, MashInput, PatternMemory, RhythmPulse, and PrecisionChoice behavior.
5. Update GM-facing API/rules/examples/coverage so GM can author StealthNoise and understands noise-meter, threshold, recovery, and browser-boundary behavior.
6. Re-run focused gates, build, prerequisite check, diff check, static scan, commit, and leave PR/merge/issue closure to Hermes.

## Initial Contract Decisions

- `durationMs`: integer `1000..30000`.
- `startingNoise`: integer `0..100`; must not be above `dangerThreshold`.
- `dangerThreshold`: integer `1..100`.
- `noiseDriftPerSecond`: positive number `1..100`; passive noise increase over time.
- `recoveryPerInput`: positive number `1..100`; how much the recovery input can reduce noise.
- `allowedOverThresholdMs`: integer `0..durationMs`; how long the player may remain over the danger threshold before failure pressure becomes terminal.
- `gradeThresholds`: object with monotonic success/partial boundaries for final noise and accumulated over-threshold time.
- Optional `recoveryKey` should default to an existing canonical QTE key token and use QTE helper labels/fallbacks; GM config must not encode RU/EN layout details.
- Browser interactive StealthNoise parity remains #918; this slice keeps browser behavior to non-interactive/manual-grade metadata.

## Risk Controls

- Keep StealthNoise field names and limits explicit in docs and tests so later QTE v2 children can reuse the pattern.
- Do not broaden `QteKeyInput` normalization beyond QTE key matching.
- Do not implement browser interactive StealthNoise in React; #918 owns parity.
- Do not make zero-drift/no-recovery configs valid because they collapse the mini-game or make it impossible.
- Keep meter rendering stable for long/short GM-authored labels and escape dynamic labels/narrative before Spectre.Console markup.
- Treat docs/examples as product behavior and verify with documentation/source guard tests.
