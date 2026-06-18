# Implementation Plan: Browser Lifecycle Panel Player Copy

**Branch**: `work/1088-browser-lifecycle-copy` | **Date**: 2026-06-18 | **Spec**: `specs/1088-browser-lifecycle-copy/spec.md`
**Input**: Feature specification from `/specs/1088-browser-lifecycle-copy/spec.md`
**Source Issue**: #1088 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1088

## Summary

Fix the Browser Client scene lifecycle panel's idle/ready copy so players see grammatical, in-world Russian guidance instead of implementation-placeholder wording about browser turn recording being connected later. The implementation is a focused C# DTO copy change guarded by a failing browser API contract test and frontend verification.

## Technical Context

**Language/Version**: C#/.NET 8 and React/TypeScript/Vite Browser Client
**Primary Dependencies**: xUnit, BookOfEternityClient browser DTOs, frontend contract fixtures
**Storage**: N/A
**Testing**: `dotnet test`, `npm run verify --prefix BookOfEternityClient.WebFrontend`
**Target Platform**: Local Windows Book client with Browser Client frontend
**Project Type**: Local game client and local browser UI
**Performance Goals**: N/A; copy-only change
**Constraints**: Preserve lifecycle state IDs/flags/actions; default UI must not leak technical terms
**Scale/Scope**: One Browser Client lifecycle panel state and its focused contract coverage
**Source Issue(s)**: #1088 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1088
**Contract Scope**: Player-facing browser C# DTO copy and focused browser API/source-guard tests; no GM/runtime-state/afterlife contract changes
**Verification Commands**:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserApiContractTests.GameScreenIdleTurnState_UsesPlayerFacingRussianCopy" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"
npm run verify --prefix BookOfEternityClient.WebFrontend
git diff --check origin/main...HEAD
```

## Constitution Check

- **GitHub traceability**: PASS — source issue #1088 is linked in `spec.md`, `plan.md`, and `tasks.md`.
- **Spec Kit fit**: PASS — although the code change is small, `AGENTS.md` requires Spec Kit for player-facing Browser Client UX changes.
- **Player-facing integrity**: PASS — scope explicitly replaces implementation-placeholder copy and forbids API/DTO/pending/protocol/browser implementation wording in the default panel.
- **Contract/state authority**: PASS — no lifecycle state machine, validation, GM-authored behavior, save format, or afterlife contract changes are planned.
- **Test-first path**: PASS — update `BrowserApiContractTests.GameScreenIdleTurnState_UsesPlayerFacingRussianCopy`, verify RED, then change DTO copy.
- **Verification evidence**: PASS — focused C# test, browser API contract filter, frontend verify, diff check, static scan, and independent review are required.
- **Agent orchestration**: PASS — this issue can be implemented inline because it is a tiny copy/test fix; if delegated, Codex must receive this spec/plan/tasks and Superpowers TDD/review requirements.

## Project Structure

### Documentation (this feature)

```text
specs/1088-browser-lifecycle-copy/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/WebUi/BrowserGameScreenService.cs
    Owns BrowserGameScreenTurnStateDto ready-state copy used by the scene lifecycle panel.

BookOfEternityClient.Tests/BrowserApiContractTests.cs
    Owns focused browser API/source-guard coverage for the idle ready lifecycle copy.

BookOfEternityClient.WebFrontend/
    Consumes the DTO contract; no React behavior change expected, but frontend verify must pass.
```

**Structure Decision**: Keep copy authority in the existing C# DTO builder because the frontend already renders `turnState.message` and recommended action descriptions. Do not add React-side string substitution or new frontend gameplay rules.

## Complexity Tracking

No constitution violations are introduced. The only complexity note is that Spec Kit artifacts are intentionally lightweight because the issue is a narrow player-facing Browser Client copy bug, but `AGENTS.md` requires durable UX traceability.
