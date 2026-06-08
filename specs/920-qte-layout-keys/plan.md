# Implementation Plan: QTE Layout-Independent Key Input

**Branch**: `work/920-qte-layout-keys` | **Date**: 2026-06-09 | **Spec**: `specs/920-qte-layout-keys/spec.md`

**Input**: Feature specification from `/specs/920-qte-layout-keys/spec.md`

## Summary

Implement layout-independent QTE key handling for the common RU/EN prompt keys from GitHub issue #920. The implementation should introduce scoped QTE-only key normalization and display helpers, wire browser keyboard handling to prefer physical `KeyboardEvent.code`, keep console/fallback mappings deterministic, and update QTE docs/examples so GM-authored configuration remains layout-agnostic.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite

**Primary Dependencies**: Spectre.Console for console UI, xUnit for C# tests, Vitest/TypeScript frontend verification

**Storage**: File-backed JSON game state; this issue should not add persistent state unless implementation discovers an existing QTE key field that requires display metadata.

**Testing**: xUnit via `dotnet test`, frontend verification via `npm run verify --prefix BookOfEternityClient.WebFrontend`

**Target Platform**: Local Windows game client with console and local browser UI; implementation should stay cross-platform where possible.

**Project Type**: Local game client with C# runtime/web host and React frontend presentation layer.

**Performance Goals**: Key normalization must be constant-time/string-table style and cheap enough for real-time QTE consumers.

**Constraints**: QTE normalization must be scoped to QTE matching/display, must not alter ordinary text entry, and must not introduce OS-global keyboard hooks or cloud/remote dependencies.

**Scale/Scope**: One QTE foundation slice covering current v1 QTE display/resolution surfaces plus reusable helpers for #911/#918 future mini-games.

**Source Issue(s)**: #920 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/920; parent #911; related #918.

**Contract Scope**: player-facing, GM-facing prompts, validation/docs/examples, console, browser, frontend.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|BrowserApiContractTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests"`
- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `git diff --check origin/main...HEAD`
- Added-line static security scan excluding Spec Kit/docs text false positives.

## Constitution Check

- **GitHub traceability**: PASS — source issue #920 is linked in `spec.md`, this `plan.md`, and `tasks.md`; parent/related QTE epics are named but not included as closure targets.
- **Spec Kit fit**: PASS — #920 is explicitly contract-sensitive and spans console/browser/player docs plus future QTE v2 foundations.
- **Player-facing integrity**: PASS — prompts must use clear Russian/in-world copy and must not expose API/DTO/debug terminology.
- **Contract/state authority**: PASS — GM-facing QTE docs/examples must be updated because authoring guidance changes; no afterlife pending/control contract is expected.
- **Test-first path**: PASS — tests for console normalization, browser physical-code preference, docs/examples, and existing QTE compatibility are planned before production changes.
- **Verification evidence**: PASS — focused C# and frontend verification commands are listed.
- **Agent orchestration**: PASS — Codex delegation must include this Spec Kit feature, issue body, constitution path, tasks, Superpowers method requirements, and final Hermes acceptance rule.

## Project Structure

### Documentation (this feature)

```text
specs/920-qte-layout-keys/
├── spec.md
├── plan.md
├── tasks.md
├── contracts/
│   └── qte-layout-input.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.cs                 # QTE runtime/service and possible console prompt/display integration
BookOfEternityClient/WebUi/QteWebInteractionService.cs           # QTE web DTO/action bridge if display metadata changes
BookOfEternityClient.WebFrontend/src/components/QteScenePanel.tsx # Browser QTE panel and keyboard-event handling/prompt copy
BookOfEternityClient.WebFrontend/src/api/contracts.ts            # Browser DTO contract if key-display fields are exposed
BookOfEternityClient.WebFrontend/src/**                          # QTE-only frontend key normalization helper/test target
BookOfEternityClient.Tests/QteSceneServiceTests.cs               # Console/shared QTE normalization tests
BookOfEternityClient.Tests/ValidationServiceQteTests.cs          # Existing QTE validation compatibility/docs tests if needed
BookOfEternityClient.Tests/BrowserApiContractTests.cs            # Browser DTO/player-copy guards if DTO changes
Rules/Block_CLI_QTE.txt                                          # GM-facing QTE rules
Examples/E_CLI_QTE_Offer.txt                                     # Worked QTE example
TaskGuides/CLI_Step_Main.txt                                     # QTE guidance entrypoint if currently references QTE authoring
```

**Structure Decision**: Prefer small, scoped QTE key helper(s) over spreading mapping tables through UI code. If a C# helper is added, keep it in the QTE service/domain area and test it directly. If a frontend helper is added, keep it in a QTE-specific utility/module and verify it with focused Vitest tests. React remains presentation-only; C# remains the gameplay/application authority.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | The feature can be implemented with scoped helpers, tests, and docs updates. | N/A |
