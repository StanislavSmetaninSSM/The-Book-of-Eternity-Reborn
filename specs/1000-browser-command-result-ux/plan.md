# Implementation Plan: Browser Command Result UX Audit and Fixes

**Branch**: `1000-browser-command-result-ux` *(spec path only; current worktree remains on its existing branch)* | **Date**: 2026-06-17 | **Spec**: `specs/1000-browser-command-result-ux/spec.md`

**Input**: Feature specification from `/specs/1000-browser-command-result-ux/spec.md`

## Summary

Audit browser command-result rendering against console detail quality and fix high-impact defects in test-first slices. The initial faction-detail fix exposed a broader pattern, so the implemented scope now includes a default-mode safety projection and all-command regression gates for browser-executable player-default read-only and local-turn commands.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite

**Primary Dependencies**: ASP.NET/local web host, Spectre.Console for console, React browser frontend

**Storage**: JSON game session files under `BookOfEternityClient/game_session`

**Testing**: xUnit via `dotnet test`; frontend/browser verification via Browser Act

**Target Platform**: Windows local desktop/browser clients

**Project Type**: Desktop console client plus local browser frontend

**Performance Goals**: Command-result rendering should remain local and instant for typical session files.

**Constraints**: Do not change canonical game-state authority or GM contracts in the first client-owned presentation slice.

**Scale/Scope**: Multiple command-result surfaces; implemented scope covers faction detail, status label localization, default/raw separation, and systematic default output hygiene across player-default command descriptors.

**Source Issue(s)**: #1087 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1087

**Contract Scope**: player-facing, browser, frontend, console parity

**Verification Commands**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExplorerWebCommandServiceTests"` plus Browser Act before/after audit. Full project test may still fail on unrelated Daren QTE text snapshots and console live smoke environment issues.

## Constitution Check

- **GitHub traceability**: Pass. Source issue #1087 is linked in spec, plan, and tasks.
- **Spec Kit fit**: Pass. This is player-facing browser UX and console/browser parity work.
- **Player-facing integrity**: Pass. Default browser output must use Russian player-facing labels and avoid debug/API leakage.
- **Contract/state authority**: Pass for first slice. No canonical state, validation, pending/control, GM prompt, or example contract changes are planned.
- **Test-first path**: Pass. Add focused regression tests before production changes.
- **Verification evidence**: Pass. Focused xUnit command and Browser Act audit are identified.
- **Agent orchestration**: Pass. Work remains directly in Codex; any delegation must include #1087, this spec path, TDD, and verification commands.

## Project Structure

### Documentation (this feature)

```text
specs/1000-browser-command-result-ux/
├── spec.md
├── plan.md
├── research.md
├── quickstart.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── UI/ExplorerMortalWorldCommandResultBuilder.cs
├── UI/ExplorerUniversalMetaCommandResultBuilder.cs
├── UI/ExplorerChaosSeaCommandResultBuilder.cs
├── UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs
└── WebUi/ExplorerWebCommandService.cs

BookOfEternityClient.WebFrontend/src/
├── components/BlockRenderer.tsx
├── components/CommandResult.tsx
└── playerFacingCommandResult.ts

BookOfEternityClient.Tests/
└── ExplorerWebCommandServiceTests.cs
```

**Structure Decision**: Start at the C# projection layer when backend blocks already contain player-facing detail gaps; touch React only when the block renderer itself cannot present the corrected projection.

## Complexity Tracking

No constitution violations are planned for the first implementation slice.
