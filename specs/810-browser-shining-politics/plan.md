# Implementation Plan: Browser Shining Abode Politics

**Branch**: `task/810-browser-shining-politics` | **Date**: 2026-06-07 | **Spec**: `specs/810-browser-shining-politics/spec.md`

**Input**: Feature specification from `/specs/810-browser-shining-politics/spec.md`

## Summary

Implement browser parity for GitHub issue #810 by adding three guided Shining politics prompt/write flows: faction founding, faction realignment, and leadership transition. Keep `/shining_politics` as the read-only filtered overview, add dedicated browser-supported local-turn commands for mutations, reuse `ShiningFactionRequestState` DTO validators/writers, update command/help/action coverage, and avoid changing pending/control contracts.

## Technical Context

**Language/Version**: C#/.NET 8, React/TypeScript/Vite for browser presentation when fixtures or frontend contracts change.

**Primary Dependencies**: `BookOfEternityClient` command protocol, `ExplorerShiningAbodeCommandResultBuilder`, `ExplorerWebPromptSessionService`, `BrowserAfterlifeWriteService`, `ShiningFactionRequestState`, `SarefMainStoryState`, `BrowserCommandCoverageService`, `BrowserPlayerCommandMenuBuilder`, browser frontend contract fixtures if C# DTO fixtures change.

**Storage**: File-backed JSON game state, especially `game_state/meta/soul_state.json`, `game_state/meta/shining_abode_state.json`, `game_state/meta/guardian_abode_residents.json`, `game_state/meta/guardians.json`, and Shining politics pending files under `game_state/control/`.

**Testing**: xUnit via `dotnet test`, focused RED/GREEN tests before production code, frontend `npm run verify` when fixtures/frontend change, documentation/source-guard tests if contract docs/examples change.

**Target Platform**: Local Windows desktop game client with console and loopback browser UI.

**Project Type**: .NET game client plus local web frontend.

**Performance Goals**: Prompt/result construction should be bounded by local game-state files and side-effect-free until submit; local writes should stay bounded to Shining politics pending files, soul resource reserves, Shining root resource reserves, and local write lock coordination.

**Constraints**: Offline/local-only, no cloud services, no React gameplay logic, no raw debug/API/file-path wording in default player UI, preserve console/browser semantic parity, do not expose hidden Saref/Wings/internal Shining ledgers.

**Scale/Scope**: One #817 child parity issue: Shining faction founding, realignment, and leadership transition only.

**Source Issue(s)**:
- #810 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/810

**Contract Scope**: Player-facing browser/C# command protocol and existing Shining faction founding/realignment/leadership pending contracts. GM-facing docs/examples only if a payload shape, response tag, receipt, validation rule, or GM-authored guidance changes.

**Baseline Evidence Recorded Before Implementation**:
- Hermes baseline before this Codex run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningPolitics|ShiningAbode|ExplorerWebCommandServiceTests|BrowserAfterlifeWriteServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests" --logger "console;verbosity=minimal"` => 308 passed / 0 failed / 0 skipped.

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningPolitics|ShiningAbode|ExplorerWebCommandServiceTests|BrowserAfterlifeWriteServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`
- `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files or browser contract fixtures change.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` when docs/contracts/examples are touched.
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- `git diff --check origin/main...HEAD`
- Added-line static security scan excluding Spec Kit docs and refined default player-facing raw diagnostic scan over production UI/browser additions.

## Constitution Check

- **GitHub traceability**: PASS. Source issue #810 is linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is browser/console parity, player-facing UX, multi-file work, and Shining Abode pending-control work.
- **Player-facing integrity**: PASS. The plan requires Russian in-world browser copy, filtered visible Shining faction data, and no raw diagnostics in default UI.
- **Contract/state authority**: PASS. `ShiningFactionRequestState` remains the C# authority; React remains presentation-only.
- **Afterlife docs guardrail**: PASS. No pending/control shape change is planned; docs/examples/tests become mandatory if implementation changes any runtime or GM-authored contract surface.
- **Test-first path**: PASS. Tasks require RED C# tests and source guards before production code.
- **Verification evidence**: PASS. Focused C#, build, frontend-if-touched, docs-if-touched, Spec Kit, diff-check, and scan commands are listed.
- **Agent orchestration**: PASS. Hermes owns independent acceptance, PR creation/merge, and issue closure after Codex completes a locally verified commit.

## Project Structure

### Documentation (this feature)

```text
specs/810-browser-shining-politics/
├── checklists/
│   └── requirements.md
├── contracts/
│   └── browser-shining-politics.md
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs
BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs
BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs
BookOfEternityClient/WebUi/ExplorerWebCommandService.cs
BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs
BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs
BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs
BookOfEternityClient/Services/ShiningFactionRequestState.cs               # authority to reuse, not rewrite
BookOfEternityClient.Tests/WebUi/BrowserShiningPoliticsParityTests.cs      # likely new focused tests
BookOfEternityClient.Tests/*Shining* / *BrowserApi* tests                  # update as needed
BookOfEternityClient.WebFrontend/src/api/contract-fixtures/*.json          # update if C# API fixtures change
OtherGuides/Afterlife_Contract_Matrix.md                                   # update only if contract shape/guidance changes
Examples/E_CLI_Afterlife_Turns.txt
Examples/example_validation_manifest.json
BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs
BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs
```

**Structure Decision**: Implement dedicated C# browser commands/prompts for the three Shining politics mutations, route submit handling through `BrowserAfterlifeWriteService`, reuse `ShiningFactionRequestState` validation/writers, and touch React only if generated DTO fixtures/types require synchronization.

## Complexity Tracking

No constitution violations are planned. If implementation discovers that browser Shining politics needs a new request field, response receipt, validation rule, or GM closure rule, that expands contract/docs scope and must be synchronized through docs/examples/tests before commit.
