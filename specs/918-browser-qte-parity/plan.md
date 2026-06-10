# Implementation Plan: Browser QTE Interactive Mini-Games

**Branch**: `work/918-browser-qte-parity` | **Date**: 2026-06-10 | **Spec**: `specs/918-browser-qte-parity/spec.md`  
**Source Issues**: [#918](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918), parent [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680), QTE v2 parent [#911](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911)

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript, React, Vite  
**Primary Dependencies**: Spectre.Console QTE runtime, `QteSceneService`, local Browser API host, React frontend, Vitest/player-facing source guards  
**Storage**: Existing file-backed `game_session` QTE runtime state only; no new save/pending/control files  
**Testing**: `dotnet test` focused QTE/browser/docs filters; `npm run verify --prefix BookOfEternityClient.WebFrontend`; component/player-facing tests; `git diff --check`; added-line static scan  
**Target Platform**: Local Windows host, browser over loopback/local web UI, console unchanged  
**Project Type**: C# game client + local React browser frontend  
**Performance Goals**: QTE panel must not add always-on polling loops or heavy global listeners outside active mini-game lifecycle  
**Constraints**: React remains presentation/request-state; C# remains gameplay/write authority; no scoring/practice/Daren scope; no new GM-authored QTE fields  
**Scale/Scope**: One cross-surface Browser Client parity slice for existing QTE v1 and v2 checks

## Constitution Check

- **GitHub Issue Traceability**: #918 is the tracked implementation issue; parent #680 and #911 are referenced for context.
- **Player-Facing Game Client Integrity**: Default Browser UI must be player-facing Russian game UI, not manual debug-grade selection.
- **Contract and State Authority**: No new GM-authored QTE contract fields; if API projection/docs change, source guards and examples must stay synchronized. Dynamic text rendered in browser remains React-escaped/player-copy filtered.
- **Test-First Verification**: Add failing C#/frontend guards before implementation where behavior changes.
- **Agent Orchestration Discipline**: Spec Kit artifacts are created before code. Codex implementation must follow Superpowers TDD/debugging/review/verification. Hermes owns final acceptance and GitHub closure.

## Project Structure

### Existing files expected to change

- `BookOfEternityClient/WebUi/QteWebInteractionService.cs`  
  Extend browser QTE action DTO projection with read-only check config details for supported mini-games while preserving existing endpoint/write semantics.

- `BookOfEternityClient/WebUi/LocalWebUiHost.cs`  
  Update endpoint/contract docs or generated fixture behavior only if DTO/API contract snapshots require it.

- `BookOfEternityClient.Tests/BrowserApiContractTests.cs`  
  Add/adjust API contract tests proving QTE action config projection and no raw grade-default leak in default player surfaces.

- `BookOfEternityClient.Tests/QteSceneServiceTests.cs` / `ValidationServiceQteTests.cs` / `PromptDocumentationCoverageTests.cs` / `ExampleDocumentationValidationTests.cs`  
  Touch only when docs/source guards need synchronization for browser QTE parity guidance; avoid changing core QTE semantics unless a true gap is found.

- `BookOfEternityClient.WebFrontend/src/api/contracts.ts`  
  Add typed QTE config projection and supported-check metadata.

- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/qte-state.json` and generated/check files  
  Update fixtures so TypeScript and C# contract guards cover supported check types.

- `BookOfEternityClient.WebFrontend/src/components/QteScenePanel.tsx`  
  Replace default grade selection with check-specific mini-game rendering and submit path.

- New focused frontend component/helper files under `BookOfEternityClient.WebFrontend/src/components/` or `src/qte/`  
  Prefer small components/helpers per check family rather than growing `QteScenePanel.tsx` into a monolith.

- `BookOfEternityClient.WebFrontend/src/utils/qteKeyInput.ts`  
  Reuse existing RU/EN layout normalization support; do not duplicate key maps per mini-game.

- New/updated frontend tests under `BookOfEternityClient.WebFrontend/test/`  
  Add deterministic tests for mini-game grade calculation, default-player no-grade-selector behavior, keyboard/pointer paths, and fixture contract coverage.

- GM/player docs (likely `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and/or browser docs)  
  Update only if the browser parity behavior or DTO contract needs player/GM-facing synchronization. Do not add new GM fields.

### Spec Kit artifacts

- `specs/918-browser-qte-parity/spec.md`
- `specs/918-browser-qte-parity/plan.md`
- `specs/918-browser-qte-parity/tasks.md`
- `specs/918-browser-qte-parity/contracts/browser-qte-mini-games.md`

## Implementation Strategy

1. Baseline current behavior and add RED tests/source guards showing that supported non-BranchChoice browser actions still expose manual grade selection and lack typed check config in the browser contract.
2. Extend the C# browser DTO with read-only, normalized check config sufficient for each supported type. Keep endpoint request/resolve semantics unchanged.
3. Add frontend type contracts, fixture coverage, and deterministic grade helpers for the mini-games.
4. Refactor `QteScenePanel` to delegate to supported mini-game components. The panel should submit only computed grades and should hide manual grade controls in default player mode.
5. Cover accessibility/responsive behavior with component/source tests and, if feasible, a dependency-light visual smoke artifact under `TestResults/browser-smoke/`.
6. Reconcile docs/source guards only where behavior/API text changes.
7. Run frontend verify, focused C# QTE/browser/docs tests, build gates, diff hygiene, static scan, independent review, PR, squash merge, issue evidence comment, and closure.

## Verification Plan

Baseline already recorded on this branch before Spec Kit edits:

- `npm ci --prefix BookOfEternityClient.WebFrontend` — completed, 52 packages, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` — passed; Vitest player-facing slice 44/44 passed; Vite build succeeded.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` — passed 247/247.

Expected implementation verification:

- RED and GREEN focused frontend tests for `QteScenePanel` / mini-game helpers.
- RED and GREEN C# contract tests for QTE action config projection.
- `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`.
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolves `specs/918-browser-qte-parity`.
- `git diff --check origin/main...HEAD`.
- Added-line static security scan over code/docs with plan/spec false positives inspected.

## Risk Log

- **Risk**: Implementing every QTE type in one issue is broad.  
  **Mitigation**: Keep mini-game code small/deterministic; no scoring/Daren/practice scope; if a specific check cannot be safely completed, add explicit unsupported-state coverage and create a follow-up rather than shipping broken controls.

- **Risk**: React-side grade calculation could appear to move gameplay authority out of C#.  
  **Mitigation**: React computes only local mini-game outcome; C# remains the only write/routing/history/completion authority through `ResolveActiveActionAsync`.

- **Risk**: Timing-based tests can be flaky.  
  **Mitigation**: Extract pure grade calculators and test them with deterministic inputs; keep live timers as thin UI wrappers.

- **Risk**: Player-facing browser surface may leak debug/config/grade language.  
  **Mitigation**: Add player-facing source/component tests that default UI does not render manual grade selectors for supported checks and does not expose raw DTO/config paths.

## Phase 0 - Research Notes

- Current `QteScenePanel.tsx` renders a manual outcome selector for `action.requiresSubmittedGrade` and directly submits the selected grade.
- Current `QteWebActionDto` includes `actionId`, `label`, `checkType`, `baseDifficulty`, `primaryCharacteristic`, `requiresSubmittedGrade`, and `gradeOptions`, but not typed check config.
- `QteSceneService` already has runtime implementations and constants for v1/v2 checks; browser parity should reuse the same authored config shape and endpoint routing rather than inventing a separate contract.

## Phase 1 - Design Decisions

- Use a typed config projection rather than raw JSON rendering in default UI.
- Keep mini-game grade calculators pure and separately tested; keep React components as UI wrappers.
- Treat `BranchChoice` as direct choice with no manual grade UI.
- Treat unknown/future check types as unsupported in default player UI, not as a manual grade backdoor.
- Defer #924 scoring/ranks, #919 Daren, and #925 practice mode.
