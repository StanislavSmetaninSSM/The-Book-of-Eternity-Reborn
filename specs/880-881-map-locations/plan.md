# Implementation Plan: Map and Locations Command Semantics

**Branch**: `fix/880-881-map-locations` | **Date**: 2026-06-07 | **Spec**: `specs/880-881-map-locations/spec.md`

**Input**: Feature specification from `/specs/880-881-map-locations/spec.md`

## Summary

Fix GitHub issues #880 and #881 together because they define two sides of one command contract: `/карта` / `/map` must be the visual map, while `/локации` / `/locations` must be the location list/details flow. Implement with shared C# helpers where practical, strict regression tests, browser visual map rendering, frontend verification, and a visual screenshot/artifact before closure.

## Technical Context

**Language/Version**: C#/.NET 8, Spectre.Console, React/TypeScript/Vite.

**Primary Dependencies**: `ExplorerMode.WorldAndStatus.cs`, `ExplorerMode.MetaLoreAndTravel.cs`, `ExplorerMortalWorldCommandResultBuilder`, `ExplorerCommandResultConsoleRenderer`, `LocalMapViewService`, `LocalMapViewerLauncher`, `UiMapBlock`, browser command-result renderers, frontend contract types/tests.

**Storage**: File-backed JSON state under `game_state/world/current_location.json` and `game_state/world/world_map.json`, supporting both root-level `newLocations`/`locationUpdates` and wrapped `worldMapUpdates.newLocations`/`locationUpdates`.

**Testing**: xUnit via `dotnet test`, frontend `npm run verify`, source guards and/or visual smoke/screenshot artifact for browser map rendering.

**Target Platform**: Local Windows desktop console client and loopback Browser Client.

**Constraints**: Preserve local/offline play, no cloud dependencies, no new GM pending/control contracts, no raw debug/API/DTO language in default player-facing UI, dynamic names/descriptions must be escaped/sanitized.

**Source Issue(s)**:
- #880 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/880
- #881 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/881

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Map|FullyQualifiedName~Location|FullyQualifiedName~ExplorerWebCommandService|FullyQualifiedName~LocalMapViewerService|FullyQualifiedName~CommandResult" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` after restore/build artifacts exist.
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` when tests are touched.
- `npm ci --prefix BookOfEternityClient.WebFrontend` in fresh worktrees when `node_modules/` is absent.
- `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- Browser visual smoke/screenshot command chosen by implementation; save output under `TestResults/` and record the artifact path.
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`.
- `git diff --check origin/main...HEAD`.
- Added-line static risk scan excluding plan docs.

## Constitution Check

- **GitHub traceability**: PASS. Source issues #880 and #881 are linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is a multi-file console/browser parity and player-facing UX bugfix.
- **Player-facing integrity**: PASS. The plan separates map vs locations semantics and requires Russian/player-facing default UI.
- **Contract/state authority**: PASS. No GM-authored pending/control contract should change; docs update is conditional on documented command behavior changes.
- **Test-first path**: PASS. Tasks require RED tests/source guards before implementation and visual evidence before closure.
- **Verification evidence**: PASS. Focused C#, frontend, visual, Spec Kit, build, diff, and static scan commands are listed.
- **Agent orchestration**: PASS. Hermes will delegate implementation to Codex with this spec/plan/tasks, constitution, baseline evidence, Superpowers TDD/debugging/review requirements, and local verification commands.

## Project Structure

### Spec Kit feature

```text
specs/880-881-map-locations/
├── spec.md
├── plan.md
└── tasks.md
```

### Expected source/test touch points

```text
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaLoreAndTravel.cs
BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerCommandResultConsoleRenderer.cs
BookOfEternityClient/Services/LocalMapViewService.cs
BookOfEternityClient/CommandProtocol/UiBlocks.cs
BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs
BookOfEternityClient.Tests/LocalMapViewerServiceTests.cs
BookOfEternityClient.Tests/*Map* or *Location* source-guard tests
BookOfEternityClient.WebFrontend/src/components/CommandResult.tsx
BookOfEternityClient.WebFrontend/src/components/BlockRenderer.tsx
BookOfEternityClient.WebFrontend/src/test or test files for map rendering/player copy
TestResults/browser-smoke/ or equivalent visual artifact path
```

## Implementation Strategy

1. Extract or reuse a shared location-list data builder so console and browser can both project current, adjacent, discovered, and updated locations without duplicating unsafe JSON traversal.
2. Move the Mortal console location selector/list behavior from `/карта` to `/локации`, leaving `/карта` on the visual map path for all realms.
3. Replace browser `UiMapBlock` text-only rendering with a real map component using the existing map DTO. Prefer local React/SVG rendering that mirrors the shared map viewer semantics; do not add remote dependencies.
4. Replace browser `/локации` generic `BuildBundle` behavior with a dedicated player-facing location result that unwraps `worldMapUpdates` and includes `current_location.json` data.
5. Add regression tests and source guards before production changes. If a visual screenshot cannot be automated headlessly, create a deterministic HTML visual smoke artifact under `TestResults/` and record that limitation.
6. Keep docs/prompts unchanged unless implementation changes documented command behavior or local web map renderer docs; if docs are changed, run the relevant documentation coverage tests.

## Risk Notes

- The console `/карта` path is interactive and may require testability seams or source guards rather than brittle key-driven E2E.
- Browser screenshot evidence is mandatory for #880 closure; source guards alone are not sufficient.
- `dotnet test --no-restore` can false-green in fresh worktrees. Use `-p:IsTestProject=true` and confirm non-zero test counts.
- `npm run verify` requires frontend dependencies in fresh worktrees; run `npm ci --prefix BookOfEternityClient.WebFrontend` when needed.
