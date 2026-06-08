# Implementation Plan: QTE v2 MashInput

**Branch**: `work/912-qte-mashinput` | **Date**: 2026-06-09 | **Spec**: `specs/912-qte-mashinput/spec.md`

**Input**: Feature specification from `specs/912-qte-mashinput/spec.md`.

## Summary

Implement GitHub issue #912 by adding a contract-validated `MashInput` QTE v2 check, deterministic local console resolution, and synchronized GM-facing rules/examples. Reuse the #920 QTE key normalization helpers for physical-key/RU-EN matching and keep full browser interactivity out of scope for #918.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite only if browser metadata changes are required.

**Primary Dependencies**: Existing `QteSceneService`, `ValidationService`, `FileSystemManager`, Spectre.Console, xUnit, shared JSON models.

**Storage**: Existing file-backed QTE offer/runtime/history JSON files; no new persistent canonical state is expected beyond existing QTE runtime/history surfaces.

**Testing**: xUnit via `dotnet test`; frontend `npm run verify` only if frontend files change.

**Target Platform**: Local Windows console/browser client, with deterministic tests that do not require a real keyboard layout or wall-clock timing.

**Project Type**: Local game client and GM-authored JSON contract.

**Performance Goals**: MashInput console loop should remain responsive during short cinematic checks and deterministic helpers should run instantly in tests.

**Constraints**: No cloud dependencies, no telemetry, no ordinary text-input normalization, no real-time sleeps in automated tests, no browser gameplay duplication in this slice.

**Scale/Scope**: One QTE v2 mini-game type, its validation/docs/examples/tests, and compatibility with existing QTE v1 types.

**Source Issue(s)**: #912 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/912; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Contract Scope**: player-facing console QTE, GM-facing prompts/docs/examples, validation, local QTE runtime resolution.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- `git diff --check origin/main...HEAD`
- Added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/plan recipe false positives.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` only if frontend files are touched.

## Constitution Check

- **GitHub traceability**: Pass. `spec.md`, `plan.md`, and `tasks.md` link #912 and parent #911.
- **Spec Kit fit**: Pass. This is player-facing, contract-sensitive, validation/docs/examples work and the issue explicitly requests Spec Kit.
- **Player-facing integrity**: Pass planned. Console prompts must use Russian in-world copy and avoid debug/API leakage; browser surfaces must not falsely claim completed parity.
- **Contract/state authority**: Pass planned. GM-facing `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and related documentation/source guards must change with the contract.
- **Test-first path**: Pass planned. Add failing validation and QteSceneService tests before implementation.
- **Verification evidence**: Pass planned. Focused QTE/docs tests, build, diff check, and static scan are required before PR/merge.
- **Agent orchestration**: Pass planned. Hermes delegates implementation to Codex with this spec/plan/tasks, constitution, issue criteria, and Superpowers TDD/review requirements.

## Project Structure

### Documentation (this feature)

```text
specs/912-qte-mashinput/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── mashinput-qte-contract.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.cs
BookOfEternityClient/Core/ValidationService*.cs or nearby validation partials
BookOfEternityClient/UI/ or existing console input abstractions if a small helper is needed
BookOfEternityClient.Tests/QteSceneServiceTests.cs
BookOfEternityClient.Tests/ValidationServiceQteTests.cs
BookOfEternityClient.Tests/*Documentation* or source guard tests that cover QTE docs/examples
Rules/Block_CLI_QTE.txt
Examples/E_CLI_QTE_Offer.txt
Examples/example_validation_manifest.json if the example manifest needs to enumerate the changed QTE example
BookOfEternityClient.WebFrontend/* only if existing read-only QTE metadata must be adjusted to avoid false browser claims
```

**Structure Decision**: Keep the first MashInput implementation close to existing QTE runtime/validation code. Extract a small focused helper only if it keeps deterministic tests clean and avoids further growth in `QteSceneService.cs`. Do not introduce a separate gameplay engine or browser-side resolver in this slice.

## Complexity Tracking

No constitution violations are planned. The feature is cross-contract but bounded to one QTE v2 type.

## Implementation Approach

1. Establish baseline by reading issue #912/#911, #920 artifacts, constitution, QTE service/validation/tests/docs/examples, and current worktree status.
2. Add RED validation tests for valid MashInput config and malformed key/duration/target/threshold variants.
3. Add RED local resolution tests for success, partial, fail, cancel, and difficulty/stat adjustment using deterministic helpers or injected input/time.
4. Implement the minimal contract model/validation and console resolver to satisfy tests while preserving v1 QTE behavior.
5. Update GM-facing rules/examples/coverage so GM can author MashInput and knows layout normalization is client-owned.
6. Re-run focused gates, build, diff check, static scan, independent review, PR, squash-merge, issue closure, and post-merge focused verification.

## Final Contract Decisions

- `durationMs`: integer `750..10000`.
- `targetPresses`: integer `1..80`, additionally capped by `floor(durationMs / 1000 * 12)`.
- `partialThreshold`: number `> 0` and `<= 1`.
- Effective success target: `targetPresses + (baseDifficulty - 3) - statTier`, clamped to `1..80`.
- Effective partial target: `ceil(effectiveSuccessTarget * partialThreshold)`, clamped to `1..effectiveSuccessTarget`.
- Browser interactive MashInput parity remains #918; this slice keeps browser behavior to non-interactive/manual-grade metadata.

## Risk Controls

- Keep MashInput field names and limits explicit in docs and tests so later QTE v2 children can reuse the pattern.
- Do not broaden `QteKeyInput` normalization beyond QTE key matching.
- Do not implement browser interactive MashInput in React; #918 owns parity.
- Escape dynamic labels/narrative before Spectre.Console markup.
- Treat any changes to docs/examples as product behavior and verify with documentation/source guard tests.
