# Implementation Plan: Chaos Sea Browser Command Output Parity

**Branch**: `work/1124-chaos-browser` | **Date**: 2026-06-21 | **Spec**: `specs/1124-chaos-sea-browser-parity/spec.md`

**Input**: Feature specification from `specs/1124-chaos-sea-browser-parity/spec.md`

## Summary

Close #1124 by verifying and hardening existing shared C# browser command-result detail routes for Chaos Sea afterlife commands. The implementation path is test-first where behavior is missing; if existing builders already satisfy the behavior, add regression coverage and merge without runtime contract changes.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: Existing `ExplorerWebCommandService`, `ExplorerAfterlifeCombatCommandResultBuilder`, `ExplorerChaosSeaCommandResultBuilder`, and xUnit tests.

**Storage**: Existing JSON game-state fixtures in tests; no new persisted schema.

**Testing**: `dotnet test` focused xUnit filters.

**Target Platform**: Local browser client over the C# game engine.

**Project Type**: Desktop/local game client with browser UI facade.

**Performance Goals**: No measurable runtime impact; command-result builders remain lightweight over existing JSON reads.

**Constraints**: No runtime afterlife contract changes; hidden/GM-only state must remain hidden in default browser output.

**Scale/Scope**: Existing player-facing Chaos Sea afterlife commands and read-only details.

**Source Issue(s)**: #1124 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1124

**Contract Scope**: player-facing browser output only; no GM-facing prompt/docs/runtime-state contract changes.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_ChaosSeaAfterlife" --verbosity minimal`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "Afterlife|Chaos|ExplorerWebCommand" --verbosity minimal`
- `git diff --check`

## Constitution Check

- **GitHub traceability**: PASS. #1124 is linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is broad afterlife/browser parity and hidden-data boundary work.
- **Player-facing integrity**: PASS. Russian player-facing details and no technical leakage are explicit requirements.
- **Contract/state authority**: PASS. No runtime-state or GM-authored contract change is planned.
- **Test-first path**: PASS. Focused regression tests are the implementation vehicle; production code changes only if those tests expose a gap.
- **Verification evidence**: PASS. Focused and broad dotnet commands are listed.
- **Agent orchestration**: PASS. Work is local Codex implementation, no delegation.

## Project Structure

### Documentation (this feature)

```text
specs/1124-chaos-sea-browser-parity/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── UI/ExplorerAfterlifeCombatCommandResultBuilder.cs
└── UI/ExplorerChaosSeaCommandResultBuilder.cs

BookOfEternityClient.Tests/
└── ExplorerWebCommandServiceTests.cs
```

**Structure Decision**: Keep command authority in shared C# builders; add tests in the existing browser command service test suite.

## Complexity Tracking

No constitution violations.
