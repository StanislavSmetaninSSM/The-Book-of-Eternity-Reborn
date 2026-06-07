# Implementation Plan: Browser Shining Abode Actions

**Branch**: `task/811-browser-shining-actions` | **Date**: 2026-06-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from GitHub issue [#811](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/811)

## Summary

Implement browser parity for the console Shining Abode action flows:

- discover/open native Shining faction,
- invest in faction,
- support project,
- unsupport project,
- retire project.

The browser must expose player-facing command metadata and guided prompt forms, then submit through existing C# authority and `ShiningCoreActionRequestState` pending action writer. The implementation must not change the afterlife runtime contract or GM-authored response surface.

## Technical Context

**Language/Version**: C#/.NET 8 for runtime/tests; existing React/Vite frontend only if metadata consumption requires changes
**Primary Dependencies**: `BookOfEternityClient`, file-backed JSON state, existing browser prompt/session services
**Storage**: Existing local JSON game state and `game_state/control/pending_shining_abode_actions.json`
**Testing**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj` with focused filters, source guards, build verification
**Target Platform**: Local Windows client/runtime with browser UI
**Project Type**: C# game client plus browser command/prompt surface
**Performance Goals**: No new polling or remote services; prompt construction reads current local state only
**Constraints**: Russian player-facing copy by default; no raw API/DTO/pending/control/debug leakage; C# remains authority; no sibling issue scope
**Scale/Scope**: Five browser command forms plus tests/source guards; no contract migration

## Constitution Check

- **Issue traceability**: GitHub issue #811 is the tracked implementation task and is linked in `spec.md`, `plan.md`, and `tasks.md`.
- **Player-facing integrity**: Browser labels, help, prompt copy, and blockers must use Russian player-facing wording and avoid raw diagnostics.
- **Contract/state authority**: Implementation must reuse `ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync` and `WriteRequestAsync`; no React-side mechanics or new pending/control shape.
- **Test-first verification**: Add failing tests/source guards before production changes, then implement minimal code to pass.
- **Orchestration discipline**: Spec Kit artifacts are created before implementation; final verification evidence will be recorded in `tasks.md`.

## Project Structure

### Documentation

```text
specs/811-browser-shining-actions/
|-- spec.md
|-- plan.md
|-- tasks.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   `-- browser-shining-actions.md
`-- checklists/
    `-- requirements.md
```

### Source Areas

```text
BookOfEternityClient/
|-- CommandProtocol/ExplorerCommandCatalog.cs
|-- UI/ExplorerHelpCommandResultBuilder.cs
|-- UI/ExplorerShiningAbodeCommandResultBuilder.cs
`-- WebUi/
    |-- BrowserAfterlifeWriteService.cs
    |-- BrowserCommandCoverageService.cs
    |-- BrowserPlayerCommandMenuBuilder.cs
    `-- ExplorerWebPromptSessionService.cs

BookOfEternityClient.Tests/
`-- WebUi/
    |-- BrowserShiningActionsParityTests.cs
    |-- BrowserCommandCoverageServiceTests.cs
    |-- BrowserPlayerCommandMenuBuilderTests.cs
    `-- AfterlifeShiningPlayerFacingSourceGuardTests.cs
```

## Phase 0: Research

Research decisions are recorded in [research.md](research.md). Key expected decisions:

- Reuse existing Shining core pending request writer and validators.
- Add browser command catalog entries and prompt builders in C#.
- Keep frontend unchanged unless current metadata consumption proves insufficient.
- No afterlife contract docs/examples update unless implementation changes runtime contract shape.

## Phase 1: Design

Design artifacts:

- [data-model.md](data-model.md)
- [contracts/browser-shining-actions.md](contracts/browser-shining-actions.md)
- [quickstart.md](quickstart.md)
- [checklists/requirements.md](checklists/requirements.md)

## Phase 2: Task Planning

Detailed work breakdown is in [tasks.md](tasks.md). The implementation order is:

1. RED tests/source guards for command metadata, prompt forms, write payloads, direct realm guards, stale submit guards, and coverage.
2. Minimal C# prompt/read support in `ExplorerShiningAbodeCommandResultBuilder`.
3. Minimal C# submit/write support in `BrowserAfterlifeWriteService`.
4. Command catalog, menu, help, prompt-session lock, and coverage metadata.
5. GREEN verification, Spec Kit task evidence, final validation, and commit.

## Complexity Tracking

No constitution violation or complexity exception is planned. Any discovered need to alter afterlife pending/control contracts requires revising this plan and adding GM-facing documentation/example/test updates before completion.

## Verification Plan

Focused RED/GREEN:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningActionsParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests" --logger "console;verbosity=minimal"
```

Final focused parity sweep:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningAbode|ShiningActions|ShiningPolitics|BrowserAfterlifeWriteServiceTests|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests" --logger "console;verbosity=minimal"
```

C# build:

```powershell
dotnet build BookOfEternityClient.sln --no-restore
```

Whitespace and added-line checks:

```powershell
git diff --check origin/main...HEAD
```

Static security scan for added non-Spec Kit lines:

```powershell
git diff --unified=0 origin/main...HEAD -- . ":(exclude)specs/811-browser-shining-actions/**" | Select-String -Pattern "password|passwd|secret|token|apikey|api_key|authorization|bearer|client_secret|connectionstring|private_key|BEGIN RSA|BEGIN OPENSSH" -CaseSensitive:$false
```

Frontend verification:

- Run `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/TypeScript/frontend behavior is changed.

Documentation-sensitive afterlife verification:

- Not required if this feature preserves existing pending/control contract shape and GM-authored response surface.
- Required if any runtime contract shape changes:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```
