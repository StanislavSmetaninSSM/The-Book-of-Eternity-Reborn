# Implementation Plan: Daren QTE Training Showcase

**Branch**: `work/919-daren-qte-training` | **Date**: 2026-06-11 | **Spec**: `specs/919-daren-qte-training/spec.md`

**Input**: Feature specification from `specs/919-daren-qte-training/spec.md`

## Summary

Implement #919 as a standalone, client-owned Daren QTE showcase mini-adventure that reuses the existing QTE v1/v2, browser parity, score/rank, and practice isolation foundations. The work adds an authored route, deterministic ending/reward model, persistent best-tier profile record outside ordinary game state, exactly-once New Game Ink Feather grants, console/browser player-facing surfaces, validation/normalizer hardening, docs/examples/source guards, and tests.

## Technical Context

**Language/Version**: C#/.NET 8 for runtime/client/tests, TypeScript/React/Vite for browser UI.
**Primary Dependencies**: Spectre.Console, System.Text.Json, existing C# QTE services, existing browser QTE mini-game components, Vitest/TypeScript tests.
**Storage**: Existing file-backed local state plus a new client/profile record outside `game_session/game_state`; default planned path `client_profile/qte_showcase_rewards.json` under the configured base path unless implementation finds an existing profile convention.
**Testing**: xUnit via `dotnet test`; TypeScript/Vitest and Vite build via `npm run verify --prefix BookOfEternityClient.WebFrontend`.
**Target Platform**: Local Windows/loopback game client with console and browser frontends; no cloud dependency.
**Project Type**: Desktop/console C# local game client with local browser frontend.
**Performance Goals**: deterministic route tests must run without wall-clock sleeps; browser/frontend verify must stay in existing local CI-style command.
**Constraints**: no mutation of ordinary campaign state before valid reward write; no reward duplication; no React-side gameplay authority; no raw debug/API wording in default UI.
**Scale/Scope**: one full authored Daren route with all required QTE types, four ending tiers, persistent best-tier profile, New Game reward grant, and docs/examples.

**Source Issue(s)**: #919 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919>
**Contract Scope**: player-facing console/browser UX, QTE runtime/scoring, persistent client profile, New Game initialization, validation/normalizer, docs, examples, source guards.
**Verification Commands**:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|Daren|NewGame|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

## Constitution Check

- **GitHub traceability**: #919 is linked in `spec.md`, this plan, `tasks.md`, and `contracts/daren-qte-training-contract.md`.
- **Spec Kit fit**: This is multi-file, player-facing, contract-sensitive QTE/New Game/profile work and needs durable artifacts.
- **Player-facing integrity**: Console and browser surfaces must use Russian in-world/player-facing copy and preserve semantic parity. Debug/API/DTO/file-path language is forbidden in default UI.
- **Contract/state authority**: C# remains authoritative for route state, grade acceptance, scoring, persistent profile writes, and New Game reward grants. React remains presentation/input handling over existing browser QTE mini-games.
- **Test-first path**: Route coverage, ending thresholds, profile update, New Game grant, no-mutation, docs/source guard, and frontend player-copy tests must be written and observed failing before production changes.
- **Verification evidence**: Focused C# tests, frontend verify, client build, Spec Kit prerequisite check, diff hygiene, static scan, and independent review are required before PR/merge.
- **Agent orchestration**: Hermes delegates implementation to Codex with this feature path, issue body, constitution, Superpowers TDD/debug/review requirements, and baseline verification evidence. Hermes retains final PR/merge/issue closure authority.

## Project Structure

### Documentation (this feature)

```text
specs/919-daren-qte-training/
├── spec.md
├── plan.md
├── tasks.md
├── contracts/
│   └── daren-qte-training-contract.md
└── checklists/
    └── requirements.md
```

### Expected Source Areas

```text
BookOfEternityClient/
├── Core/GameEngine/GameEngine.MainMenu.cs          # New Game reward grant integration and player-facing start copy
├── Core/FileSystemManager.cs                       # profile path helpers if needed outside game_state
├── Services/                                      # Daren route/profile/validation services if new focused services are needed
├── WebUi/QteWebInteractionService.cs              # browser DTO/API projection for Daren showcase if this remains the QTE web boundary
├── WebUi/LocalWebUiMainMenuService.cs             # browser/launcher entry point metadata if needed
└── UI/ExplorerMode*.cs or command/menu surfaces    # console entry point and Daren showcase flow

BookOfEternityClient.Tests/
├── *Qte*Tests.cs                                  # route coverage, QTE resolution, scoring/ending tests
├── *Daren*Tests.cs or focused new tests           # profile/New Game/no-mutation tests
└── PromptDocumentationCoverageTests.cs / ExampleDocumentationValidationTests.cs / source guards

BookOfEternityClient.WebFrontend/src/
├── components/                                    # Daren showcase entry/route/result surfaces if new React components are needed
├── components/qte/                                # reuse existing QTE mini-game components, no duplicated gameplay authority
└── test/                                         # focused Daren/browser/player-copy tests

CLI_API_Specification.md
Rules/Block_CLI_QTE.txt
Examples/E_CLI_QTE_Offer.txt
Examples/example_validation_manifest.json          # update only if example validation coverage changes
```

**Structure Decision**: Prefer focused new services/classes for Daren route definition, route attempts, profile persistence, and New Game reward grant rather than expanding already-large UI/runtime files. If a small existing QTE service owns the correct abstraction, extend it narrowly and keep route/profile persistence separate from ordinary campaign state.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Persistent client/profile state outside ordinary game state | #919 requires rewards to survive New Game clearing and apply to future new sessions | Storing in `game_state/meta/achievements.json` would be deleted by New Game initialization and tied to one session |
| Console plus browser implementation in one feature | #919 acceptance requires discoverability/client path and the project requires console/browser parity | Console-only would leave Browser Client unable to use the showcase and would violate parity expectations |
| Docs/examples/source guards in the same change | QTE reward semantics and GM-authored-vs-client-owned boundaries are product contracts | Code-only behavior would leave the GM/player guidance inconsistent with runtime behavior |

## Implementation Strategy

1. Add RED tests/source guards for the contract before production changes.
2. Implement a Daren route definition and deterministic route runner/resolver that reuses existing QTE action/score helpers.
3. Add persistent profile service with upgrade-only best-tier semantics and validation/normalization.
4. Integrate exactly-once New Game reward grant during new session initialization, with visible player-facing copy and idempotency marker.
5. Add console and browser Daren entry/progress/result surfaces while preserving C# authority and existing QTE mini-games.
6. Update QTE docs/examples/source guards.
7. Run focused and broad-enough local gates, reconcile Spec Kit tasks, get independent review, PR, squash merge, post issue evidence, and close #919.

## Risk Controls

- Use deterministic tests and injected clocks/route inputs rather than wall-clock delays for the 20-30 minute scenario.
- Add explicit no-mutation assertions around existing campaign state before/after showcase launch/exit/completion.
- Derive Ink Feather bonus from canonical tier id instead of trusting persisted profile amount.
- Store and check per-session reward grant marker before modifying new soul Ink Feathers.
- Keep browser mini-game handlers guarded against child-control key bubbling per Browser QTE parity lessons.
- Keep Daren showcase out of QTE Practice Mode rewards and ordinary GM-authored QTE examples.
