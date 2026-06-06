# Implementation Plan: Browser Guardian Social Conversation and Lore

**Branch**: `fix/808-browser-guardian-social` | **Date**: 2026-06-06 | **Spec**: `specs/808-browser-guardian-social/spec.md`

**Input**: Feature specification from `/specs/808-browser-guardian-social/spec.md`

## Summary

Implement browser parity for GitHub issue #808 by adding an afterlife Guardian social command/prompt session that writes the same pending Guardian social request used by the console Guardian panel for ordinary conversation and lore/knowledge requests. Keep C# as the gameplay and state authority, update tests/fixtures/help metadata, enforce direct-command and stale-prompt realm guards, and update GM-facing documentation only if the existing pending Guardian social contract guidance is missing or changes.

## Technical Context

**Language/Version**: C#/.NET 8, React/TypeScript/Vite for browser presentation.

**Primary Dependencies**: `BookOfEternityClient` command protocol, `ExplorerLifecycleLocalTurnCommandResultBuilder`, `ExplorerWebPromptSessionService`, `BrowserAfterlifeWriteService`, `ActorSocialInteractionRequestState`, `BrowserCommandCoverageService`, `BrowserPlayerCommandMenuBuilder`, Vite frontend contract fixtures.

**Storage**: File-backed JSON game state, especially `game_state/meta/guardians.json`, `game_state/meta/soul_state.json`, and `game_state/control/pending_guardian_social_interactions.json`.

**Testing**: xUnit via `dotnet test`, frontend `npm run verify`, documentation/source-guard tests if contract docs/examples change.

**Target Platform**: Local Windows desktop game client with console and loopback browser UI.

**Project Type**: .NET game client plus local web frontend.

**Performance Goals**: Prompt/result construction should be fixture-scale and side-effect-free until submit; local write should stay bounded to pending Guardian social request files and local write lock coordination.

**Constraints**: Offline/local-only, no cloud services, no React gameplay logic, no raw debug/API/file-path wording in default player UI, preserve console/browser semantic parity.

**Scale/Scope**: One #817 child parity issue: Guardian social talk/lore only.

**Source Issue(s)**:
- #808 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/808
- Parent #817 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817

**Contract Scope**: Player-facing browser/C# command protocol, existing afterlife pending Guardian social request, GM reminder/docs/examples only if contract guidance is missing or changes.

**Baseline Evidence Recorded Before Implementation**:
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ActorSocialInteractionRequestStateTests|FullyQualifiedName~ActorSocialInteractionValidationTests|FullyQualifiedName~BrowserTradeParityTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` => 166 passed / 0 failed / 0 skipped.
- `npm ci --prefix BookOfEternityClient.WebFrontend` => 54 packages added, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` => TypeScript typecheck passed, player-facing/Vitest tests 27/27 passed, Vite build succeeded.

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ActorSocialInteractionRequestStateTests|FullyQualifiedName~ActorSocialInteractionValidationTests|FullyQualifiedName~BrowserGuardianSocialParityTests|FullyQualifiedName~BrowserTradeParityTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~CommandResult" --logger "console;verbosity=minimal"`
- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` when docs/contracts/examples are touched
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`
- `git diff --check origin/main...HEAD`

## Constitution Check

- **GitHub traceability**: PASS. Source issues #808 and #817 are linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is browser/console parity and player-facing UX work on an afterlife pending/control surface.
- **Player-facing integrity**: PASS. The plan requires Russian in-world browser copy and no raw diagnostics in default UI.
- **Contract/state authority**: PASS. `ActorSocialInteractionRequestState` remains the C# authority; docs/examples/tests must change if pending request shape or closure guidance changes.
- **Test-first path**: PASS. Tasks require RED C# tests before command/write implementation and frontend verification after fixture/type changes.
- **Verification evidence**: PASS. Focused C#, frontend, docs-if-touched, Spec Kit, build, and diff-check commands are listed.
- **Agent orchestration**: PASS. Hermes will delegate implementation to Codex with this spec/plan/tasks, constitution, baseline results, Superpowers TDD/debugging/review requirements, and local verification commands.

## Project Structure

### Documentation (this feature)

```text
specs/808-browser-guardian-social/
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
BookOfEternityClient/Services/ActorSocialInteractionRequestState.cs
BookOfEternityClient/Services/AfterlifeContractRegistry.cs                    # inspect/update only if contract registry/docs need sync
BookOfEternityClient.Tests/WebUi/BrowserGuardianSocialParityTests.cs            # likely new focused tests
BookOfEternityClient.Tests/*PromptSession* / *ActorSocial* / *BrowserApi* tests # update as needed
BookOfEternityClient.WebFrontend/src/api/contract-fixtures/*.json               # update if API contract fixture changes
BookOfEternityClient.WebFrontend/src/**                                         # presentation-only updates if current components need fixture/type sync
CLI_API_Specification.md                                                        # inspect/update if closure guidance is missing or shape changes
CLI_Agent_Daemon_Specification.md                                               # inspect/update if hidden routing tag guidance changes
OtherGuides/Afterlife_Contract_Matrix.md                                        # inspect/update if contract matrix is missing or shape changes
Examples/* and Examples/example_validation_manifest.json                         # update only if a worked example/guidance gap is found
```

**Structure Decision**: Implement the browser command and prompt in the existing C# command-result/prompt-session stack, add the write handler in `BrowserAfterlifeWriteService`, and touch React only for generated contract fixture/type/rendering fallout. Do not create a new frontend gameplay subsystem.

## Complexity Tracking

No constitution violations are planned. If implementation discovers that Guardian social browser submission needs a new request field or GM closure rule, that expands contract/docs scope and must be synchronized through docs/examples/tests before PR/merge.
