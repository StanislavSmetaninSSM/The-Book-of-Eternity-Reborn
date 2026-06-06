# Implementation Plan: Browser Shining Abode Resident Interactions

**Branch**: `fix/809-browser-residents` | **Date**: 2026-06-06 | **Spec**: `specs/809-browser-resident-interactions/spec.md`

**Input**: Feature specification from `/specs/809-browser-resident-interactions/spec.md`

## Summary

Implement browser parity for GitHub issue #809 by adding prompt-session flows for Shining Abode resident roster requests, resident talk/history requests, and resident transfer requests. Keep C# as the gameplay and pending-contract authority, reuse `GuardianAbodeResidentRequestState`, update tests/fixtures/help metadata, enforce direct-command and stale-prompt realm guards, and update GM-facing docs/examples only if existing resident contract guidance is missing or changes.

## Technical Context

**Language/Version**: C#/.NET 8, React/TypeScript/Vite for browser presentation.

**Primary Dependencies**: `BookOfEternityClient` command protocol, `ExplorerLifecycleLocalTurnCommandResultBuilder`, `ExplorerWebPromptSessionService`, `BrowserAfterlifeWriteService`, `GuardianAbodeResidentRequestState`, `GuardianAbodeResidentState`, `BrowserCommandCoverageService`, `BrowserPlayerCommandMenuBuilder`, Vite frontend contract fixtures.

**Storage**: File-backed JSON game state, especially `game_state/meta/guardians.json`, `game_state/meta/guardian_abode_residents.json`, `game_state/meta/soul_state.json`, and pending files under `game_state/control/` for resident roster, interactions, and transfers.

**Testing**: xUnit via `dotnet test`, frontend `npm run verify`, documentation/source-guard tests if contract docs/examples change.

**Target Platform**: Local Windows desktop game client with console and loopback browser UI.

**Project Type**: .NET game client plus local web frontend.

**Performance Goals**: Prompt/result construction should be fixture-scale and side-effect-free until submit; local write should stay bounded to resident pending request files and local write lock coordination.

**Constraints**: Offline/local-only, no cloud services, no React gameplay logic, no raw debug/API/file-path wording in default player UI, preserve console/browser semantic parity.

**Scale/Scope**: One #817 child parity issue: resident roster, resident talk, resident history, resident transfer.

**Source Issue(s)**:
- #809 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/809
- Parent #817 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817

**Contract Scope**: Player-facing browser/C# command protocol, existing Shining Abode resident roster/interactions/transfers pending contracts, GM reminder/docs/examples only if contract guidance is missing or shape changes.

**Baseline Evidence Recorded Before Implementation**:
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~GuardianAbodeResident|FullyQualifiedName~BrowserGuardianSocialParityTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~BrowserTradeParityTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~CommandResult|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` => 210 passed / 0 failed / 0 skipped.
- `npm ci --prefix BookOfEternityClient.WebFrontend` => 54 packages added, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` => TypeScript typecheck passed, player-facing/Vitest tests 27/27 passed, Vite build succeeded.

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~GuardianAbodeResident|FullyQualifiedName~BrowserResident|FullyQualifiedName~BrowserGuardianSocialParityTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~BrowserTradeParityTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~CommandResult" --logger "console;verbosity=minimal"`
- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` when docs/contracts/examples are touched
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`
- `git diff --check origin/main...HEAD`

## Constitution Check

- **GitHub traceability**: PASS. Source issues #809 and #817 are linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is browser/console parity and player-facing UX work on afterlife/Shining Abode pending/control surfaces.
- **Player-facing integrity**: PASS. The plan requires Russian in-world browser copy and no raw diagnostics in default UI.
- **Contract/state authority**: PASS. `GuardianAbodeResidentRequestState` remains the C# authority; docs/examples/tests must change if pending request shapes, response tags, receipts, or closure guidance change.
- **Test-first path**: PASS. Tasks require RED C# tests before command/write implementation and frontend verification after fixture/type changes.
- **Verification evidence**: PASS. Focused C#, frontend, docs-if-touched, Spec Kit, build, and diff-check commands are listed.
- **Agent orchestration**: PASS. Hermes will delegate implementation to Codex with this spec/plan/tasks, constitution, baseline results, Superpowers TDD/debugging/review requirements, and local verification commands.

## Project Structure

### Documentation (this feature)

```text
specs/809-browser-resident-interactions/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs
BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs
BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs
BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs
BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs
BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs
BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs
BookOfEternityClient/Services/GuardianAbodeResidentState.cs
BookOfEternityClient.Tests/WebUi/BrowserResidentInteractionsParityTests.cs     # likely new focused tests
BookOfEternityClient.Tests/*GuardianAbodeResident* / *PromptSession* / *BrowserApi* tests # update as needed
BookOfEternityClient.WebFrontend/src/api/contract-fixtures/*.json               # update if API contract fixture changes
BookOfEternityClient.WebFrontend/src/**                                         # presentation-only updates if current components need fixture/type sync
CLI_API_Specification.md                                                        # inspect/update if closure guidance is missing or shape changes
CLI_Agent_Daemon_Specification.md                                               # inspect/update if hidden routing tag guidance changes
OtherGuides/Afterlife_Contract_Matrix.md                                        # inspect/update if contract matrix is missing or shape changes
Examples/* and Examples/example_validation_manifest.json                         # update only if a worked example/guidance gap is found
```

**Structure Decision**: Implement browser commands/prompts in the existing C# command-result/prompt-session stack, add write handling in `BrowserAfterlifeWriteService`, and touch React only for generated contract fixture/type/rendering fallout. Do not create a new frontend gameplay subsystem.

## Complexity Tracking

No constitution violations are planned. If implementation discovers that browser resident interactions need a new request field, hidden routing tag, receipt, validation rule, or GM closure rule, that expands contract/docs scope and must be synchronized through docs/examples/tests before PR/merge.
