# Implementation Plan: QTE v2 PatternMemory

**Branch**: `work/913-qte-patternmemory` | **Date**: 2026-06-09 | **Spec**: `specs/913-qte-patternmemory/spec.md`

**Input**: Feature specification from `specs/913-qte-patternmemory/spec.md`.

## Summary

Implement GitHub issue #913 by adding a contract-validated `PatternMemory` QTE v2 check, deterministic local console reveal/input resolution, and synchronized GM-facing rules/examples. Reuse the #920 QTE key normalization helpers and the #912 MashInput QTE v2 patterns, while keeping full browser interactivity out of scope for #918.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite only if browser metadata changes are required.

**Primary Dependencies**: Existing `QteSceneService`, `QteKeyInput`, `ValidationService`, `FileSystemManager`, Spectre.Console, xUnit, shared JSON models.

**Storage**: Existing file-backed QTE offer/runtime/history JSON files; no new persistent canonical state is expected beyond existing QTE runtime/history surfaces.

**Testing**: xUnit via `dotnet test`; frontend `npm run verify` only if frontend files change.

**Target Platform**: Local Windows console/browser client, with deterministic tests that do not require a real keyboard layout or wall-clock timing.

**Project Type**: Local game client and GM-authored JSON contract.

**Performance Goals**: PatternMemory console reveal/input phases should remain responsive during short cinematic checks and deterministic helpers should run instantly in tests.

**Constraints**: No cloud dependencies, no telemetry, no ordinary text-input normalization, no real-time sleeps in automated tests, no browser gameplay duplication in this slice.

**Scale/Scope**: One QTE v2 mini-game type, its validation/docs/examples/tests, and compatibility with existing QTE v1 types, #920 layout normalization, and #912 MashInput.

**Source Issue(s)**: #913 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/913; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Contract Scope**: player-facing console QTE, GM-facing prompts/docs/examples, validation, local QTE runtime resolution.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- `git diff --check origin/main...HEAD`
- Added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/plan recipe false positives.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` only if frontend files are touched.

## Constitution Check

- **GitHub traceability**: Pass. `spec.md`, `plan.md`, and `tasks.md` link #913 and parent #911.
- **Spec Kit fit**: Pass. This is player-facing, contract-sensitive, validation/docs/examples work and the issue explicitly requests Spec Kit.
- **Player-facing integrity**: Pass planned. Console prompts must use Russian in-world copy and avoid debug/API leakage; browser surfaces must not falsely claim completed parity.
- **Contract/state authority**: Pass planned. GM-facing `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and related documentation/source guards must change with the contract.
- **Test-first path**: Pass planned. Add failing validation and QteSceneService tests before implementation.
- **Verification evidence**: Pass planned. Focused QTE/docs tests, build, diff check, and static scan are required before PR/merge.
- **Agent orchestration**: Pass planned. Hermes delegates implementation to Codex with this spec/plan/tasks, constitution, issue criteria, and Superpowers TDD/review requirements.

## Project Structure

### Documentation (this feature)

```text
specs/913-qte-patternmemory/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── patternmemory-qte-contract.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.cs
BookOfEternityClient/Core/ValidationService*.cs or nearby validation partials
BookOfEternityClient/Services/QteKeyInput.cs or existing input helpers only if shared helper reuse is needed
BookOfEternityClient.Tests/QteSceneServiceTests.cs
BookOfEternityClient.Tests/ValidationServiceQteTests.cs
BookOfEternityClient.Tests/*Documentation* or source guard tests that cover QTE docs/examples
Rules/Block_CLI_QTE.txt
Examples/E_CLI_QTE_Offer.txt
Examples/example_validation_manifest.json if the example manifest needs to enumerate the changed QTE example
BookOfEternityClient.WebFrontend/* only if existing read-only QTE metadata must be adjusted to avoid false browser claims
```

**Structure Decision**: Keep the PatternMemory implementation close to existing QTE runtime/validation code and reuse the QTE v2 shape established by MashInput. Extract small focused helpers only if they keep deterministic tests clean and avoid further growth in `QteSceneService.cs`. Do not introduce a separate gameplay engine or browser-side resolver in this slice.

## Complexity Tracking

No constitution violations are planned. The feature is cross-contract but bounded to one QTE v2 type.

## Implementation Approach

1. Establish baseline by reading issue #913/#911, #912/#920 artifacts, constitution, QTE service/validation/tests/docs/examples, and current worktree status.
2. Add RED validation tests for valid PatternMemory config and malformed alphabet/sequence/reveal/input/mistake variants.
3. Add RED local resolution tests for success, partial, fail, timeout/cancel, RU/EN key matching, and difficulty/stat adjustment using deterministic helpers or injected input/time.
4. Implement the minimal contract model/validation and console resolver to satisfy tests while preserving v1 QTE and MashInput behavior.
5. Update GM-facing rules/examples/coverage so GM can author PatternMemory and knows layout normalization is client-owned.
6. Re-run focused gates, build, diff check, static scan, independent review, PR, squash-merge, issue closure, and post-merge focused verification.

## Initial Contract Decisions

- `alphabet`: unique array of supported QTE key tokens: `q`, `w`, `e`, `a`, `s`, `d`, `space`.
- `sequenceLength`: integer `2..12`.
- `revealMs`: integer `500..15000`.
- `inputTimeoutMs`: integer `1000..30000`, not below `sequenceLength * 300` ms after effective adjustment.
- `allowedMistakes`: integer `0..sequenceLength - 1`.
- Effective sequence length/timing/mistakes follow the monotonic adjustment rules in `spec.md` and `contracts/patternmemory-qte-contract.md`.
- Browser interactive PatternMemory parity remains #918; this slice keeps browser behavior to non-interactive/manual-grade metadata.

## Risk Controls

- Keep PatternMemory field names and limits explicit in docs and tests so later QTE v2 children can reuse the pattern.
- Do not broaden `QteKeyInput` normalization beyond QTE key matching.
- Do not implement browser interactive PatternMemory in React; #918 owns parity.
- Escape dynamic labels/narrative before Spectre.Console markup.
- Treat docs/examples as product behavior and verify with documentation/source guard tests.
