# Implementation Plan: Console Faction Detail Drill-Down Menu Sections

**Branch**: `work/1086-console-faction-drilldowns` | **Date**: 2026-06-18 | **Spec**: `specs/1086-console-faction-drilldowns/spec.md`

**Input**: Feature specification from `specs/1086-console-faction-drilldowns/spec.md`

## Summary

Implement #1086 as a bounded Console Client faction-detail UX slice: selected faction pages become read-only hubs with player-facing section actions and full section details for existing canonical faction data, while preserving current overviews, #1085 column alignment, and spoiler-safe visibility boundaries.

## Technical Context

**Language/Version**: C# / .NET 8 for client and tests.

**Primary Dependencies**: `ExplorerMode.FactionsAndWorldNews`, `ExplorerMortalWorldCommandResultBuilder`, `ExplorerShiningAbodeCommandResultBuilder` only if the same read-only action/result metadata is reused, `ExplorerCommandResult`/`UiAction`, Spectre.Console escaping helpers, `ConsoleLayout`, and existing faction JSON state readers.

**Storage**: Existing file-backed faction JSON under `game_state/factions/` and any existing Shining faction read-only projections. No new runtime files, pending files, schema migration, validation rule, or normalizer change is planned.

**Testing**: xUnit via `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true` with focused filters; build via `dotnet build`; terminal/plain-text capture evidence for the console menu/action output.

**Target Platform**: Local Console Client on the existing Windows/local .NET stack.

**Project Type**: Existing game client repository with console and local web frontends; this issue is console-first and read-only.

**Performance Goals**: Keep detail rendering bounded to loaded faction sidecar files and selected faction data. Avoid broad state scans or framework rewrites unless a small helper directly reduces repeated safe section rendering.

**Constraints**: Russian/in-world player copy; default output must avoid raw JSON/debug/API/DTO language; read-only behavior; hidden/GM-only data remains hidden; dynamic text must be escaped for Spectre.Console.

**Source Issue(s)**: #1086 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1086

**Contract Scope**: player-facing console navigation/rendering, tests/source guards, visual/terminal evidence, and Spec Kit artifacts. No GM prompt, runtime-state schema, validation, normalizer, browser frontend, or afterlife write-contract change is intended.

## Verification Commands

```bash
# Baseline and broader relevant slice.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~ExplorerWebCommandServiceTests" --logger "console;verbosity=minimal"

# Focused tests after Codex adds/tightens them; Codex should update tasks.md with exact #1086 test names.
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FactionDetail|FactionDrilldown|FactionSection|ExplorerModeSourceGuardTests" --logger "console;verbosity=minimal"

# Build gate when C# source changes.
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore

# Spec Kit discoverability; override is required because .specify/feature.json still points at the previous main feature.
SPECIFY_FEATURE_DIRECTORY=specs/1086-console-faction-drilldowns powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

# Diff hygiene/security.
git diff --check origin/main...HEAD
# Added-line scan over changed C# / TypeScript / non-plan code for secrets, shell execution, eval/unsafe deserialization, SQL string formatting, and raw player-copy/debug leaks; report NO_MATCHES or exact findings.
```

## Constitution Check

- **GitHub traceability**: #1086 is linked in spec, plan, tasks, checklist, and contract. Implementation must reference #1086 in PR and issue evidence.
- **Spec Kit fit**: Required because this issue changes player-facing Console Client UX and may span console rendering, shared command-result metadata, tests/source guards, and visual evidence.
- **Player-facing integrity**: Console output must use Russian/in-world labels and avoid raw JSON/API/DTO/debug/internal wording in default mode.
- **Contract/state authority**: The feature reads existing canonical faction state. No new GM-authored schema, validation, normalizer, pending/control, afterlife write, or browser contract is planned. If Codex discovers a schema/GM prompt gap, it must stop and create/link a follow-up rather than silently changing contracts.
- **Test-first path**: Add failing tests/source guards for faction section actions and representative detail output before production changes.
- **Verification evidence**: Focused C# tests, build, Spec Kit prerequisite check with `SPECIFY_FEATURE_DIRECTORY=specs/1086-console-faction-drilldowns`, `git diff --check`, added-line scan, and terminal/capture evidence are required before PR.
- **Agent orchestration**: Hermes launches Codex with this spec/plan/tasks, Superpowers TDD/debugging/review requirements, and final acceptance remains with Hermes.

## Project Structure

### Documentation (this feature)

```text
specs/1086-console-faction-drilldowns/
├── spec.md
├── plan.md
├── tasks.md
├── checklists/requirements.md
└── contracts/console-faction-detail-drilldowns.md
```

### Source Code (likely touched paths)

```text
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs
BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs        # only if shared read-only faction detail metadata is needed
BookOfEternityClient/CommandProtocol/ExplorerCommandResult.cs              # only if existing actions cannot express console section choices
BookOfEternityClient.Tests/ExplorerModeCommandTests*.cs
BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs
BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs               # only if shared command-result DTO behavior changes
TestResults/console-faction-drilldowns-*/                                  # generated evidence, copy critical artifacts to run dir before cleanup
```

**Structure Decision**: Prefer small local helpers in `ExplorerMode.FactionsAndWorldNews` for building section choices/details from already loaded faction sidecar files. Reuse shared `UiAction`/command-result patterns only when they reduce duplication or allow command-result tests to cover the same read-only detail paths. Do not add React gameplay logic or mutating faction commands.

## Complexity Tracking

No constitution violations are planned. If implementation requires changing GM-authored state contracts, validation, afterlife pending/control files, Shining write services, or browser UX, that is out of scope for #1086 and must be escalated or split into a tracked follow-up.
