# Implementation Plan: Mortal Combat Read-Only Detail Drill-Downs

**Branch**: `1054-mortal-combat-drilldowns` | **Date**: 2026-06-16 | **Spec**: `specs/1054-mortal-combat-drilldowns/spec.md`

**Input**: Feature specification from `/specs/1054-mortal-combat-drilldowns/spec.md`

## Summary

Implement #1054 as a focused Mortal World `/combat` / `/бой` drill-down parity slice. Preserve the current combat overview while adding player-facing enemy, ally, and combat-log detail affordances/content for the shared browser command-result path and semantically equivalent console behavior, with tests guarding against raw-only/all-in-one-only regressions.

## Technical Context

**Language/Version**: C# / .NET 8 for client and tests; React/Vite frontend is not expected to need changes unless existing browser rendering cannot consume the command-result DTO affordances.

**Primary Dependencies**: Existing `ExplorerMortalWorldCommandResultBuilder`, `ExplorerWebCommandService`, `ExplorerMode` console handlers, Spectre.Console markup escaping, file-backed JSON through `FileSystemManager` / `StateManager`.

**Storage**: Existing canonical files under `game_state/combat/enemies.json`, `game_state/combat/allies.json`, and `game_state/combat/combat_log.json`. No new runtime files or schema migration are planned.

**Testing**: xUnit via `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true` with focused filters; build via `dotnet build`.

**Target Platform**: Local console client and local browser client on the existing Windows/local .NET stack.

**Project Type**: Existing game client repository with console and local web frontends.

**Performance Goals**: Keep command rendering bounded for ordinary combat files; avoid scanning unrelated state files or rewriting broad command-result infrastructure.

**Constraints**: Player-facing Russian/in-world copy; no default debug/API/DTO leakage; no afterlife spiritual combat changes; dynamic GM-authored text must remain safe for Spectre/browser rendering.

**Scale/Scope**: One command family (`/combat` / `/бой`) plus focused tests/audit/spec artifacts. Sibling gaps remain #1055, #1056, #1057.

**Source Issue(s)**: #1054 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1054

**Contract Scope**: player-facing / console / browser / docs-audit / tests. No GM-facing prompt, runtime-state schema, validation, afterlife, Chaos Sea, or Shining Abode contract changes are intended.

**Verification Commands**:

```bash
# Focused behavior/source-guard slice; Codex may refine the filter after adding exact test names.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "Combat|MortalReadOnlyDrilldownAudit|ExplorerMortalWorldCommandResultBuilder|ExplorerWebCommandServiceTests|ExplorerModeCommandTests|ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

# Build gates when C# source changes.
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

# Spec Kit discoverability.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

# Diff hygiene/security.
git diff --check origin/main...HEAD
# Added-line scan over changed C# and non-plan code for secrets/shell/eval/deserialization/SQL string formatting; report NO_MATCHES or exact findings.
```

## Constitution Check

- **GitHub traceability**: #1054 is linked in spec, plan, and tasks. Implementation must reference #1054 in PR and issue evidence.
- **Spec Kit fit**: Required because the issue is multi-file player-facing console/browser parity work with durable handoff needs.
- **Player-facing integrity**: `/бой` default output must use Russian/in-world terms and avoid raw JSON/debug/API language for ordinary detail output.
- **Contract/state authority**: The feature reads existing canonical combat files; no new GM-authored schema or validation contract is planned. If Codex discovers a schema/GM prompt gap, it must stop and create/link a follow-up rather than silently changing contracts.
- **Test-first path**: Add failing tests/source guards for enemy, ally, and combat-log detail output before production changes.
- **Verification evidence**: Focused C# tests, builds, Spec Kit prerequisite check, `git diff --check`, and added-line scan are required before PR.
- **Agent orchestration**: Hermes launches Codex with this spec/plan/tasks, Superpowers TDD/debugging/review requirements, and final acceptance remains with Hermes.

## Project Structure

### Documentation (this feature)

```text
specs/1054-mortal-combat-drilldowns/
├── spec.md
├── plan.md
├── tasks.md
├── checklists/requirements.md
└── contracts/mortal-combat-drilldowns.md
```

### Source Code (likely touched paths)

```text
BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerMode*.cs or BookOfEternityClient/UI/ExplorerMode/ExplorerMode.*.cs
BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs
BookOfEternityClient.Tests/ExplorerModeCommandTests.cs
BookOfEternityClient.Tests/MortalReadOnlyDrilldownAuditTests.cs
BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs
```

**Structure Decision**: Prefer focused helper methods near the existing Mortal World command-result builder and console combat handler rather than a broad frontend redesign or shared framework rewrite. If a reusable drill-down DTO helper already exists, use it; otherwise implement the smallest command-specific structure that can later inform #1055/#1056/#1057 without blocking this issue.

## Complexity Tracking

No constitution violations are planned. If implementation requires changing GM-authored state contracts, validation, or afterlife behavior, that is out of scope for #1054 and must be escalated or split into a tracked follow-up.
