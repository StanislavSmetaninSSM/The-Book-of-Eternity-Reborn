# Implementation Plan: Mortal Read-Only Detail Drill-Down Audit

**Source GitHub issue:** #948 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/948
**Spec:** `specs/948-mortal-readonly-drilldown-audit/spec.md`

## Technical Context

- C#/.NET 8 client logic in `BookOfEternityClient/`.
- Tests in `BookOfEternityClient.Tests/` using xUnit through `dotnet test`.
- Browser command results flow through `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs` and shared command result DTO/builders.
- Console command registration/handlers live under ExplorerMode-related C# files.
- Audit output should be a tracked repository artifact, likely under `docs/audits/` or another existing docs location discovered during implementation.

## Architecture

Treat #948 as a bounded audit plus regression-guard closure. The implementation should not invent a large new drill-down framework. Instead:

1. Enumerate the mortal read-only commands from the shared builder and console registration.
2. Classify current behavior into: already has adequate detail flow, small gap fixable in this PR, larger gap requiring follow-up, or not applicable/no rich entity surface.
3. Add one audit artifact and focused tests/source guards that pin the audit coverage and protect player-facing command-result boundaries.
4. If a small low-risk discoverability improvement is clearly needed to satisfy a confirmed gap, implement it with TDD; otherwise create dedicated follow-up issues for each larger command-specific slice.

## Files to Inspect

- `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`
- `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`
- `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- `BookOfEternityClient.Tests/ExplorerModeCommandTests.cs`
- `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`
- Existing docs/audit directories and prior audit artifacts for style.

## Expected Files to Modify or Create

- Create or update an audit document for #948.
- Add/update C# tests or source guards in `BookOfEternityClient.Tests/` that verify audit coverage and/or the focused behavior fixed under this issue.
- Update this Spec Kit directory if implementation decisions change.
- Do not modify GM-facing docs unless command contract/GM-authored behavior changes.

## Verification Strategy

Minimum local gates before PR:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "ExplorerMortalWorldCommandResultBuilder|ExplorerModeCommandTests|ExplorerWebCommandServiceTests|ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"
git diff --check origin/main...HEAD
```

If tests are not discovered with `--no-restore`, rerun without `--no-restore` and ensure the output includes non-zero test counts. Add broader `dotnet test` slices if production command behavior changes beyond source/audit guards.

## Risk Management

- Avoid broad UX redesigns in this audit issue.
- Do not expose raw API/DTO/debug language in player-facing default browser/console output.
- Keep console/browser parity explicit in the audit and tests.
- Create follow-up issues instead of growing this PR into multiple command rewrites.
