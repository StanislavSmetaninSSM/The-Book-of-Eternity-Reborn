# Implementation Plan: QTE v2 RhythmPulse

**Branch**: `work/914-qte-rhythmpulse` | **Date**: 2026-06-09 | **Spec**: `specs/914-qte-rhythmpulse/spec.md`

**Input**: Feature specification from `specs/914-qte-rhythmpulse/spec.md`.

## Summary

Implement GitHub issue #914 by adding a contract-validated `RhythmPulse` QTE v2 check, deterministic local console rhythm-window resolution, and synchronized GM-facing rules/examples. Reuse the #912 MashInput and #913 PatternMemory QTE v2 patterns, keep rhythm communication visually/textually accessible, and leave full browser interactivity out of scope for #918.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite only if browser metadata changes are required.

**Primary Dependencies**: Existing `QteSceneService`, `QteKeyInput`, `ValidationService`, `FileSystemManager`, Spectre.Console, xUnit, shared JSON models.

**Storage**: Existing file-backed QTE offer/runtime/history JSON files; no new persistent canonical state is expected beyond existing QTE runtime/history surfaces.

**Testing**: xUnit via `dotnet test`; frontend `npm run verify` only if frontend files change.

**Target Platform**: Local Windows console/browser client, with deterministic tests that do not require real keyboard timing, audio, or wall-clock sleeps.

**Project Type**: Local game client and GM-authored JSON contract.

**Performance Goals**: RhythmPulse console loop should remain responsive during short cinematic checks and deterministic helpers should run instantly in tests.

**Constraints**: No cloud dependencies, no telemetry, no ordinary text-input normalization, no real-time sleeps in automated tests, no browser gameplay duplication in this slice, no audio-only QTE signal.

**Scale/Scope**: One QTE v2 mini-game type, its validation/docs/examples/tests, and compatibility with existing QTE v1 types, #920 layout normalization, #912 MashInput, and #913 PatternMemory.

**Source Issue(s)**: #914 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/914; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Contract Scope**: player-facing console QTE, GM-facing prompts/docs/examples, validation, local QTE runtime resolution.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- `git diff --check origin/main...HEAD`
- Added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/plan recipe false positives.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` only if frontend files are touched.

## Constitution Check

- **GitHub traceability**: Pass. `spec.md`, `plan.md`, and `tasks.md` link #914 and parent #911.
- **Spec Kit fit**: Pass. This is player-facing, contract-sensitive, validation/docs/examples work and the issue explicitly requests Spec Kit.
- **Player-facing integrity**: Pass planned. Console prompts must use Russian in-world copy, provide visual/textual pulse timing, and avoid debug/API leakage; browser surfaces must not falsely claim completed parity.
- **Contract/state authority**: Pass planned. GM-facing `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and related documentation/source guards must change with the contract.
- **Test-first path**: Pass planned. Add failing validation and QteSceneService tests before implementation.
- **Verification evidence**: Pass planned. Focused QTE/docs tests, build, prerequisite check, diff check, and static scan are required before commit.
- **Agent orchestration**: Pass planned. Hermes delegates implementation to Codex with this spec/plan/tasks, constitution, issue criteria, and Superpowers TDD/review requirements.

## Project Structure

### Documentation (this feature)

```text
specs/914-qte-rhythmpulse/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── rhythmpulse-qte-contract.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.cs
BookOfEternityClient/Services/QteKeyInput.cs only if shared display helpers need reuse
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

**Structure Decision**: Keep the RhythmPulse implementation close to existing QTE runtime/validation code and reuse the QTE v2 shape established by MashInput and PatternMemory. Extract small focused helpers only if they keep deterministic tests clean and avoid further growth in `QteSceneService.cs`. Do not introduce a separate gameplay engine or browser-side resolver in this slice.

## Complexity Tracking

No constitution violations are planned. The feature is cross-contract but bounded to one QTE v2 type.

## Implementation Approach

1. Establish baseline by reading issue #914/#911, #912/#913/#920 artifacts, constitution, QTE service/validation/tests/docs/examples, and current worktree status.
2. Add RED validation tests for valid RhythmPulse config and malformed pulse count/beat interval/hit window/miss tolerance/variation variants.
3. Add RED local resolution tests for success, partial, fail, no-input timeout, cancel, variation schedule, and difficulty/stat adjustment using deterministic helpers.
4. Implement the minimal contract model/validation and console resolver to satisfy tests while preserving v1 QTE, MashInput, and PatternMemory behavior.
5. Update GM-facing API/rules/examples/coverage so GM can author RhythmPulse and knows the console provides visual/textual pulse fallback.
6. Re-run focused gates, build, prerequisite check, diff check, static scan, commit, and leave PR/merge/issue closure to Hermes.

## Initial Contract Decisions

- `pulseCount`: integer `2..16`.
- `beatIntervalMs`: integer `300..3000`.
- `hitWindowMs`: integer `40..1000`, interpreted as the early/late tolerance around each pulse.
- `hitWindowMs * 2` must be strictly less than `beatIntervalMs` so pulse windows do not overlap.
- `allowedMisses`: integer `0..pulseCount - 1`.
- `patternVariation`: optional `steady`, `accelerating`, or `swing`; omitted/null means `steady`.
- RhythmPulse uses Space as the local pulse key; the GM does not configure key layout for this check.
- Effective pulse count/window/misses follow the monotonic adjustment rules in `spec.md` and `contracts/rhythmpulse-qte-contract.md`.
- Browser interactive RhythmPulse parity remains #918; this slice keeps browser behavior to non-interactive/manual-grade metadata.

## Risk Controls

- Keep RhythmPulse field names and limits explicit in docs and tests so later QTE v2 children can reuse the pattern.
- Do not broaden `QteKeyInput` normalization beyond QTE key matching.
- Do not implement browser interactive RhythmPulse in React; #918 owns parity.
- Do not rely only on audio cues; render a visual/text pulse track and progress counters.
- Escape dynamic labels/narrative before Spectre.Console markup.
- Treat docs/examples as product behavior and verify with documentation/source guard tests.
