# Implementation Plan: QTE v2 PrecisionChoice

**Branch**: `work/915-qte-precisionchoice` | **Date**: 2026-06-10 | **Spec**: `specs/915-qte-precisionchoice/spec.md`

**Input**: Feature specification from `specs/915-qte-precisionchoice/spec.md`.

## Summary

Implement GitHub issue #915 by adding a contract-validated `PrecisionChoice` QTE v2 check, deterministic local console timed-choice resolution, and synchronized GM-facing rules/examples. Reuse the #912 MashInput, #913 PatternMemory, and #914 RhythmPulse QTE v2 patterns, keep console choices/timer stable and accessible, and leave full browser interactivity out of scope for #918.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite only if browser metadata changes are required.

**Primary Dependencies**: Existing `QteSceneService`, `QteKeyInput`, `ValidationService`, `FileSystemManager`, Spectre.Console, xUnit, shared JSON models.

**Storage**: Existing file-backed QTE offer/runtime/history JSON files; no new persistent canonical state is expected beyond existing QTE runtime/history surfaces.

**Testing**: xUnit via `dotnet test`; frontend `npm run verify` only if frontend files change.

**Target Platform**: Local Windows console/browser client, with deterministic tests that do not require real keyboard timing, audio, browser automation, or wall-clock sleeps.

**Project Type**: Local game client and GM-authored JSON contract.

**Performance Goals**: PrecisionChoice console loop should remain responsive during short cinematic checks and deterministic helpers should run instantly in tests.

**Constraints**: No cloud dependencies, no telemetry, no ordinary text-input normalization, no real-time sleeps in automated tests, no browser gameplay duplication in this slice, no unstable console layouts for choice/timer surfaces.

**Scale/Scope**: One QTE v2 mini-game type, its validation/docs/examples/tests, and compatibility with existing QTE v1 types, #920 layout normalization, #912 MashInput, #913 PatternMemory, and #914 RhythmPulse.

**Source Issue(s)**: #915 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/915; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Contract Scope**: player-facing console QTE, GM-facing prompts/docs/examples, validation, local QTE runtime resolution.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- `git diff --check origin/main...HEAD`
- Added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/plan recipe false positives.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` only if frontend files are touched.

## Constitution Check

- **GitHub traceability**: Pass. `spec.md`, `plan.md`, and `tasks.md` link #915 and parent #911.
- **Spec Kit fit**: Pass. This is player-facing, contract-sensitive, validation/docs/examples work and the issue explicitly requests Spec Kit.
- **Player-facing integrity**: Pass planned. Console prompts must use Russian in-world copy, provide stable choices/timer guidance, and avoid debug/API leakage; browser surfaces must not falsely claim completed parity.
- **Contract/state authority**: Pass planned. GM-facing `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and related documentation/source guards must change with the contract.
- **Test-first path**: Pass planned. Add failing validation and QteSceneService tests before implementation.
- **Verification evidence**: Pass planned. Focused QTE/docs tests, build, prerequisite check, diff check, and static scan are required before commit.
- **Agent orchestration**: Pass planned. Hermes delegates implementation to Codex with this spec/plan/tasks, constitution, issue criteria, and Superpowers TDD/review requirements.

## Project Structure

### Documentation (this feature)

```text
specs/915-qte-precisionchoice/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── precisionchoice-qte-contract.md
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

**Structure Decision**: Keep the PrecisionChoice implementation close to existing QTE runtime/validation code and reuse the QTE v2 shape established by MashInput, PatternMemory, and RhythmPulse. Extract small focused helpers only if they keep deterministic tests clean and avoid further growth in `QteSceneService.cs`. Do not introduce a separate gameplay engine or browser-side resolver in this slice.

## Complexity Tracking

No constitution violations are planned. The feature is cross-contract but bounded to one QTE v2 type.

## Implementation Approach

1. Establish baseline by reading issue #915/#911, #912/#913/#914/#920 artifacts, constitution, QTE service/validation/tests/docs/examples, and current worktree status.
2. Add RED validation tests for valid PrecisionChoice config and malformed choices/duplicates/correct choice/grade mapping/timeout/decoy hints.
3. Add RED local resolution tests for success, partial, fail, timeout, cancel, invalid selection, and difficulty/stat adjustment using deterministic helpers.
4. Implement the minimal contract model/validation and console resolver to satisfy tests while preserving v1 QTE, MashInput, PatternMemory, and RhythmPulse behavior.
5. Update GM-facing API/rules/examples/coverage so GM can author PrecisionChoice and knows the console provides stable choices/timer fallback.
6. Re-run focused gates, build, prerequisite check, diff check, static scan, commit, and leave PR/merge/issue closure to Hermes.

## Initial Contract Decisions

- `choices`: array of `2..8` objects.
- Each choice: unique non-empty stable `id`, non-empty player-facing `label`, optional `description`, optional `hint`, and `grade` token `success`, `partial`, or `fail`.
- `correctChoiceId`: required string referencing exactly one configured choice whose `grade` is `success`.
- `timeoutMs`: integer `1000..30000`.
- `timeoutGrade`: optional `fail` or `partial`; omitted means `fail`; `success` is invalid.
- `decoyHints`: optional array/object of hints for non-success choices only; unknown choices and empty hint text are invalid.
- Effective timeout and hint clarity follow the monotonic adjustment rules in `spec.md` and `contracts/precisionchoice-qte-contract.md`.
- Browser interactive PrecisionChoice parity remains #918; this slice keeps browser behavior to non-interactive/manual-grade metadata.

## Risk Controls

- Keep PrecisionChoice field names and limits explicit in docs and tests so later QTE v2 children can reuse the pattern.
- Do not broaden `QteKeyInput` normalization beyond QTE key matching.
- Do not implement browser interactive PrecisionChoice in React; #918 owns parity.
- Do not make timeout success possible; timeout may only fail or partially succeed when explicitly authored.
- Keep choice rendering stable for long/short GM-authored labels and escape dynamic labels/narrative before Spectre.Console markup.
- Treat docs/examples as product behavior and verify with documentation/source guard tests.
