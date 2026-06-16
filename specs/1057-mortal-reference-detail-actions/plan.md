# Implementation Plan: Browser Detail Actions for Mortal Reference Commands

**Branch**: `1057-mortal-reference-detail-actions` | **Date**: 2026-06-16 | **Spec**: `specs/1057-mortal-reference-detail-actions/spec.md`

**Input**: Feature specification from `specs/1057-mortal-reference-detail-actions/spec.md`

## Summary

Implement #1057 as a bounded Mortal World browser/console parity slice for reference-style read-only commands. Preserve existing overview outputs while adding player-facing browser detail actions, detail command metadata, or equivalent shared command-result affordances so selected rich entities can be inspected without raw JSON being the only path.

## Technical Context

**Language/Version**: C# / .NET 8 for client and tests; React/Vite frontend only if existing command-result renderer cannot consume the shared C# DTO affordances.

**Primary Dependencies**: `ExplorerMortalWorldCommandResultBuilder`, `ExplorerWebCommandService`, `ExplorerCommandCatalog`, console `ExplorerMode` command handlers/registries, command-result DTO/action metadata, Spectre.Console/browser escaping patterns, and existing file-backed JSON state readers.

**Storage**: Existing canonical Mortal World JSON files already used by `/quests`, `/skills`, `/factions`, `/locations`, `/rival_threads`, `/guardian_corrections`, `/storage_access`, and `/transport`. No new runtime files, write paths, pending files, schema migration, validation rule, or normalizer change is planned.

**Testing**: xUnit via `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true` with focused filters; builds via `dotnet build`; frontend `npm run verify` only if frontend code changes.

**Target Platform**: Local console client and local browser client on the existing Windows/local .NET stack.

**Project Type**: Existing game client repository with console and local web frontends.

**Performance Goals**: Keep command rendering bounded to the selected command/state file; avoid broad state scanning or shared command-result framework rewrites unless a small helper clearly reduces duplication.

**Constraints**: Player-facing Russian/in-world copy; default output must avoid raw JSON/debug/API/DTO language; read-only behavior; no afterlife or social pending contracts; dynamic GM-authored text must remain safe for Spectre/browser rendering.

**Scale/Scope**: One audit follow-up issue across reference-style Mortal World read-only commands. The implementation must either cover every affected command or document exact follow-up issue(s) for deferred commands.

**Source Issue(s)**: #1057 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1057

**Contract Scope**: player-facing / browser command results / console parity / docs-audit / tests. No GM-facing prompt, runtime-state schema, validation, afterlife, Chaos Sea, Shining Abode, Guardian, NPC, books, or resident social-request contract changes are intended.

## Verification Commands

```bash
# Baseline and broader relevant slice.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

# Focused tests after Codex adds them; Codex should update tasks.md with exact #1057 test names.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserDetailActions|ReferenceDetail|MortalReadOnlyDrilldownAudit|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"

# Build gates when C# source changes.
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

# Frontend gate only if frontend files change.
npm run verify --prefix BookOfEternityClient.WebFrontend

# Spec Kit discoverability.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

# Diff hygiene/security.
git diff --check origin/main...HEAD
# Added-line scan over changed C# / TypeScript / non-plan code for secrets, shell execution, eval/unsafe deserialization, and SQL string formatting; report NO_MATCHES or exact findings.
```

## Constitution Check

- **GitHub traceability**: #1057 is linked in spec, plan, tasks, checklist, and contract. Implementation must reference #1057 in PR and issue evidence.
- **Spec Kit fit**: Required because this issue is multi-command player-facing browser/console parity work with durable handoff needs.
- **Player-facing integrity**: Default browser command results must use Russian/in-world terms and avoid raw JSON/debug/API/DTO language for ordinary detail output.
- **Contract/state authority**: The feature reads existing canonical Mortal World state; no new GM-authored schema, validation, normalizer, pending/control, or afterlife contract is planned. If Codex discovers a schema/GM prompt gap, it must stop and create/link a follow-up rather than silently changing contracts.
- **Test-first path**: Add failing tests/source guards for representative browser detail actions and selected detail output before production changes.
- **Verification evidence**: Focused C# tests, broader slice, builds, Spec Kit prerequisite check, `git diff --check`, and added-line scan are required before PR. Frontend verification is required if frontend files change.
- **Agent orchestration**: Hermes launches Codex with this spec/plan/tasks, Superpowers TDD/debugging/review requirements, and final acceptance remains with Hermes.

## Project Structure

### Documentation (this feature)

```text
specs/1057-mortal-reference-detail-actions/
├── spec.md
├── plan.md
├── tasks.md
├── checklists/requirements.md
└── contracts/mortal-reference-detail-actions.md
```

### Source Code (likely touched paths)

```text
BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs
BookOfEternityClient/WebUi/ or BookOfEternityClient/WebUi/* only if command-result DTO serving needs adjustment
BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs
BookOfEternityClient.Tests/ExplorerModeCommandTests*.cs
BookOfEternityClient.Tests/MortalReadOnlyDrilldownAuditTests.cs
docs/audits/mortal-readonly-drilldown-audit.md
BookOfEternityClient.WebFrontend/src/* only if existing React command-result rendering cannot display safe detail actions
```

**Structure Decision**: Prefer shared C# command-result DTO/action metadata and focused builder helpers over React-side gameplay logic. Reuse nearby #1054/#1055/#1056 drill-down patterns for detail command/action metadata. If repeated action construction across reference commands becomes noisy, extract a small local helper in the command-result builder rather than a broad framework rewrite.

## Complexity Tracking

No constitution violations are planned. If implementation requires changing GM-authored state contracts, validation, afterlife behavior, social pending files, document-reading authority, NPC detail sections, or broad browser navigation UX, that is out of scope for #1057 and must be escalated or split into a tracked follow-up.
