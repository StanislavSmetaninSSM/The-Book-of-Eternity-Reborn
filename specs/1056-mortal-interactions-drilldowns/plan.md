# Implementation Plan: Mortal Player-Interaction Read-Only Detail Drill-Downs

**Branch**: `1056-mortal-interactions-drilldowns` | **Date**: 2026-06-16 | **Spec**: `specs/1056-mortal-interactions-drilldowns/spec.md`

**Input**: Feature specification from `/specs/1056-mortal-interactions-drilldowns/spec.md`

## Summary

Implement #1056 as a focused Mortal World `/interactions` / `/взаимодействия` drill-down parity slice. Preserve the current overview while adding player-facing player-entry and interaction-record detail affordances/content for the shared browser command-result path and semantically equivalent console behavior, with tests guarding against raw-only/all-in-one-only regressions.

## Technical Context

**Language/Version**: C# / .NET 8 for client and tests; React/Vite frontend is not expected to need changes unless existing browser rendering cannot consume command-result DTO affordances.

**Primary Dependencies**: Existing `ExplorerMortalWorldCommandResultBuilder`, `ExplorerWebCommandService`, `ExplorerMode` console command handlers, `ExplorerCommandCatalog`, Spectre.Console markup escaping, file-backed JSON through `FileSystemManager` / `StateManager`.

**Storage**: Existing canonical Mortal World file `game_state/misc/player_interactions.json`, which is mapped from `otherPlayersInteractions`. Player keys may map to rich objects with `records[]` or directly to arrays of canonical command-object payloads. No new runtime files, pending files, write paths, schema migration, validation rule, or normalizer change is planned.

**Testing**: xUnit via `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true` with focused filters; build via `dotnet build`.

**Target Platform**: Local console client and local browser client on the existing Windows/local .NET stack.

**Project Type**: Existing game client repository with console and local web frontends.

**Performance Goals**: Keep command rendering bounded for ordinary player-interaction files; avoid scanning unrelated state directories or rewriting broad command-result infrastructure.

**Constraints**: Player-facing Russian/in-world copy; no default debug/API/DTO leakage; read-only behavior; no afterlife/social pending contract changes; dynamic GM-authored text must remain safe for Spectre/browser rendering.

**Scale/Scope**: One command family (`/interactions` / `/взаимодействия`) plus focused tests/audit/spec artifacts. Sibling gaps remain #1057 and #949.

**Source Issue(s)**: #1056 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1056

**Contract Scope**: player-facing / console / browser / docs-audit / tests. No GM-facing prompt, runtime-state schema, validation, afterlife, Chaos Sea, Shining Abode, Guardian, NPC, or resident social-request contract changes are intended.

**Verification Commands**:

```bash
# Baseline and broader relevant slice.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

# Focused tests after Codex adds them; Codex should update this filter in tasks.md with exact test names.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "Interactions|PlayerInteractions|MortalReadOnlyDrilldownAudit|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"

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

- **GitHub traceability**: #1056 is linked in spec, plan, tasks, checklist, and contract. Implementation must reference #1056 in PR and issue evidence.
- **Spec Kit fit**: Required because the issue is multi-file player-facing console/browser parity work with durable handoff needs.
- **Player-facing integrity**: `/взаимодействия` default output must use Russian/in-world terms and avoid raw JSON/debug/API language for ordinary detail output.
- **Contract/state authority**: The feature reads existing `game_state/misc/player_interactions.json`; no new GM-authored schema, validation, normalizer, pending/control, or social-request contract is planned. If Codex discovers a schema/GM prompt gap, it must stop and create/link a follow-up rather than silently changing contracts.
- **Test-first path**: Add failing tests/source guards for player-entry detail and interaction-record detail output before production changes.
- **Verification evidence**: Focused C# tests, broader slice, builds, Spec Kit prerequisite check, `git diff --check`, and added-line scan are required before PR.
- **Agent orchestration**: Hermes launches Codex with this spec/plan/tasks, Superpowers TDD/debugging/review requirements, and final acceptance remains with Hermes.

## Project Structure

### Documentation (this feature)

```text
specs/1056-mortal-interactions-drilldowns/
├── spec.md
├── plan.md
├── tasks.md
├── checklists/requirements.md
└── contracts/mortal-interactions-drilldowns.md
```

### Source Code (likely touched paths)

```text
BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerMode*.cs or BookOfEternityClient/UI/ExplorerMode/ExplorerMode.*.cs
BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs
BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs
BookOfEternityClient.Tests/ExplorerModeCommandTests*.cs
BookOfEternityClient.Tests/MortalReadOnlyDrilldownAuditTests.cs
docs/audits/mortal-readonly-drilldown-audit.md
```

**Structure Decision**: Prefer focused helper methods near the existing Mortal World command-result builder and console interactions handler rather than a broad frontend redesign or shared framework rewrite. Reuse #1054/#1055 drill-down patterns for command parsing, action metadata, and shared command-result rendering. If the interactions command currently has only a generic bundle summary, implement the smallest command-specific structure that covers player and record details without introducing write behavior.

## Complexity Tracking

No constitution violations are planned. If implementation requires changing GM-authored state contracts, validation, afterlife behavior, social pending files, or broad browser navigation UX, that is out of scope for #1056 and must be escalated or split into a tracked follow-up.
