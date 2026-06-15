# Implementation Plan: Mortal World-News Read-Only Detail Drill-Downs

**Branch**: `1055-mortal-world-news-drilldowns` | **Date**: 2026-06-16 | **Spec**: `specs/1055-mortal-world-news-drilldowns/spec.md`

**Input**: Feature specification from `/specs/1055-mortal-world-news-drilldowns/spec.md`

## Summary

Implement #1055 as a focused Mortal World `/world_news` / `/новости_мира` drill-down parity slice. Preserve the current overview while adding player-facing world-event, major non-event subsection, and progression detail affordances/content for the shared browser command-result path and semantically equivalent console behavior, with tests guarding against raw-only/all-in-one-only regressions.

## Technical Context

**Language/Version**: C# / .NET 8 for client and tests; React/Vite frontend is not expected to need changes unless existing browser rendering cannot consume the command-result DTO affordances.

**Primary Dependencies**: Existing `ExplorerMortalWorldCommandResultBuilder`, `ExplorerWebCommandService`, `ExplorerMode` console command handlers, `ExplorerCommandCatalog`, Spectre.Console markup escaping, file-backed JSON through `FileSystemManager` / `StateManager`.

**Storage**: Existing canonical Mortal World files currently consumed by `/новости_мира`, including `game_state/world/world_events.json`, `game_state/world/world_flags.json`, `game_state/world/progression.json`, and any existing optional section files the command already renders. No new runtime files or schema migration are planned.

**Testing**: xUnit via `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true` with focused filters; build via `dotnet build`.

**Target Platform**: Local console client and local browser client on the existing Windows/local .NET stack.

**Project Type**: Existing game client repository with console and local web frontends.

**Performance Goals**: Keep command rendering bounded for ordinary world-news files; avoid scanning unrelated state directories or rewriting broad command-result infrastructure.

**Constraints**: Player-facing Russian/in-world copy; no default debug/API/DTO leakage; no afterlife or GM contract changes; dynamic GM-authored text must remain safe for Spectre/browser rendering.

**Scale/Scope**: One command family (`/world_news` / `/новости_мира`) plus focused tests/audit/spec artifacts. Sibling gaps remain #1056, #1057, and #949.

**Source Issue(s)**: #1055 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1055

**Contract Scope**: player-facing / console / browser / docs-audit / tests. No GM-facing prompt, runtime-state schema, validation, afterlife, Chaos Sea, or Shining Abode contract changes are intended.

**Verification Commands**:

```bash
# Baseline and broader relevant slice.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

# Focused tests after Codex adds them; Codex should update this filter in tasks.md with exact test names.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "WorldNews|MortalReadOnlyDrilldownAudit|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"

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

- **GitHub traceability**: #1055 is linked in spec, plan, tasks, checklist, and contract. Implementation must reference #1055 in PR and issue evidence.
- **Spec Kit fit**: Required because the issue is multi-file player-facing console/browser parity work with durable handoff needs.
- **Player-facing integrity**: `/новости_мира` default output must use Russian/in-world terms and avoid raw JSON/debug/API language for ordinary detail output.
- **Contract/state authority**: The feature reads existing canonical world-news files; no new GM-authored schema or validation contract is planned. If Codex discovers a schema/GM prompt gap, it must stop and create/link a follow-up rather than silently changing contracts.
- **Test-first path**: Add failing tests/source guards for world-event, non-event subsection, and progression detail output before production changes.
- **Verification evidence**: Focused C# tests, builds, Spec Kit prerequisite check, `git diff --check`, and added-line scan are required before PR.
- **Agent orchestration**: Hermes launches Codex with this spec/plan/tasks, Superpowers TDD/debugging/review requirements, and final acceptance remains with Hermes.

## Project Structure

### Documentation (this feature)

```text
specs/1055-mortal-world-news-drilldowns/
├── spec.md
├── plan.md
├── tasks.md
├── checklists/requirements.md
└── contracts/mortal-world-news-drilldowns.md
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

**Structure Decision**: Prefer focused helper methods near the existing Mortal World command-result builder and console world-news handler rather than a broad frontend redesign or shared framework rewrite. If #1054 introduced reusable drill-down patterns, use those patterns for world-news sections; otherwise implement the smallest command-specific structure that can later inform #1056/#1057 without blocking this issue.

## Complexity Tracking

No constitution violations are planned. If implementation requires changing GM-authored state contracts, validation, afterlife behavior, or broad browser navigation UX, that is out of scope for #1055 and must be escalated or split into a tracked follow-up.
