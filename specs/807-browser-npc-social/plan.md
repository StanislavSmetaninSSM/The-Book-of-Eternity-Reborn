# Implementation Plan: Browser NPC Social Conversation

**Branch**: `fix/807-browser-npc-social` | **Date**: 2026-06-06 | **Spec**: `specs/807-browser-npc-social/spec.md`

**Input**: Feature specification from `/specs/807-browser-npc-social/spec.md`

## Summary

Implement browser parity for GitHub issue #807 by adding a Mortal World NPC conversation command/prompt session that writes the same pending NPC social request used by the console `Поговорить` action, including a browser-supplied conversation topic when needed. Keep C# as the gameplay authority, update tests/fixtures/help metadata, and update GM-facing documentation only if the pending request contract shape changes.

## Technical Context

**Language/Version**: C#/.NET 8, React/TypeScript/Vite for browser presentation.

**Primary Dependencies**: `BookOfEternityClient` command protocol, `ExplorerLifecycleLocalTurnCommandResultBuilder`, `ExplorerWebPromptSessionService`, `BrowserMortalWorldWriteService`, `ActorSocialInteractionRequestState`, `BrowserCommandCoverageService`, Vite frontend contract fixtures.

**Storage**: File-backed JSON game state, especially `game_state/npcs/npc_core.json` and `game_state/control/pending_npc_social_interactions.json`.

**Testing**: xUnit via `dotnet test`, frontend `npm run verify`, documentation/source-guard tests if contract docs/examples change.

**Target Platform**: Local Windows desktop game client with console and loopback browser UI.

**Project Type**: .NET game client plus local web frontend.

**Performance Goals**: Prompt/result construction should be fixture-scale and side-effect-free until submit; local write should stay bounded to pending social request files and local write lock coordination.

**Constraints**: Offline/local-only, no cloud services, no React gameplay logic, no raw debug/API/file-path wording in default player UI, preserve console/browser semantic parity.

**Scale/Scope**: One #817 child parity issue: Mortal NPC talk only.

**Source Issue(s)**:
- #807 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/807
- Parent #817 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817

**Contract Scope**: Player-facing browser/C# command protocol, Mortal pending NPC social request, GM reminder/docs/examples only if a topic field or other payload shape changes.

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ActorSocialInteractionRequestStateTests|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~CommandResult" --logger "console;verbosity=minimal"`
- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` when docs/contracts/examples are touched
- `git diff --check origin/main...HEAD`

## Constitution Check

- **GitHub traceability**: PASS. Source issues #807 and #817 are linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is browser/console parity and player-facing UX work with possible pending request contract documentation impact.
- **Player-facing integrity**: PASS. The plan requires Russian in-world browser copy and no raw diagnostics in default UI.
- **Contract/state authority**: PASS. `ActorSocialInteractionRequestState` remains the C# authority; docs/examples/tests must change if pending request shape changes.
- **Test-first path**: PASS. Tasks require RED C# tests before command/write implementation and frontend verification after fixture/type changes.
- **Verification evidence**: PASS. Focused C#, frontend, docs-if-touched, and diff-check commands are listed.
- **Agent orchestration**: PASS. Hermes will delegate implementation to Codex with this spec/plan/tasks, constitution, baseline results, Superpowers TDD/debugging/review requirements, and local verification commands.

## Project Structure

### Documentation (this feature)

```text
specs/807-browser-npc-social/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs
BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs
BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs
BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs
BookOfEternityClient/Services/ActorSocialInteractionRequestState.cs
BookOfEternityClient/Services/AfterlifeContractRegistry.cs                     # inspect only unless contract docs need sync
BookOfEternityClient.Tests/*Browser* / *PromptSession* / *ActorSocial* tests
BookOfEternityClient.WebFrontend/src/api/contract-fixtures/*.json               # update if API contract fixture changes
BookOfEternityClient.WebFrontend/src/**                                         # presentation-only updates if current components need fixture/type sync
OtherGuides/Afterlife_Contract_Matrix.md                                        # update only if pending contract documentation changes
Examples/* and example_validation_manifest.json                                 # update only if pending contract examples change
```

**Structure Decision**: Implement the browser command and prompt in the existing C# command-result/prompt-session stack, add the write handler in `BrowserMortalWorldWriteService`, and touch React only for generated contract fixture/type/rendering fallout. Do not create a new frontend gameplay subsystem.

## Complexity Tracking

No constitution violations are planned. If implementation discovers that the conversation topic requires a new pending request field, that is accepted scope for #807 and must be documented/tested rather than hidden as a code-only drift.
