# Implementation Plan: Chaos Sea Guardian/Abode Browser Drill-Downs

**Branch**: `1063-chaos-guardian-abode-drilldowns` | **Date**: 2026-06-16 | **Spec**: `specs/1063-chaos-guardian-abode-drilldowns/spec.md`

## Source Issue

- GitHub issue #1063 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1063
- Origin audit row: `docs/audits/afterlife-drilldown-audit.md` AFD-001 from #949.

## Summary

Add shared C# browser command-result detail actions and selected-detail rendering for Chaos Sea Guardian/Abode overview surfaces. Preserve existing overview output, keep the work read-only, avoid React-side gameplay rules, and cover console/browser parity with focused tests/source guards.

## Technical Context

- **Language/runtime**: C# / .NET 8 command protocol, Explorer command services, tests; React/Vite only if existing command-result rendering needs presentation updates.
- **Primary source areas**: `BookOfEternityClient/CommandProtocol/`, `BookOfEternityClient/WebUi/`, relevant afterlife/ExplorerMode builders under `BookOfEternityClient/UI/ExplorerMode/`, and `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` or nearby afterlife/browser source guards.
- **Existing governance**: `AGENTS.md`, `.specify/memory/constitution.md`, Book project references for Browser Reborn panels/detail surfaces, #1063 issue body, #949 audit artifact/spec.
- **Precedent**: #1057 mortal reference detail actions in `ExplorerWebCommandServiceTests` and shared command-result action patterns using `UiAction`.

## Constitution Check

- **Issue traceability**: All repo edits are tied to #1063; spec/plan/tasks/contract/checklist link #1063 and #949.
- **Player-facing integrity**: Default browser output must be Russian/in-world and must not expose API/DTO/endpoint/debug/raw path copy outside advanced diagnostics.
- **Contract/state authority**: The feature is intended as read-only presentation over existing afterlife state. Runtime schema, pending/control, validation, normalizer, GM prompts/examples, and manifests are not planned to change.
- **Test-first verification**: Add failing focused tests/source guards before production code for new detail actions and selected-detail behavior.
- **Agent orchestration**: Hermes owns final acceptance/PR/merge/closure; Codex may implement and verify in the issue worktree.

## Project Structure and Responsibilities

- `specs/1063-chaos-guardian-abode-drilldowns/spec.md`: product requirements and boundaries.
- `specs/1063-chaos-guardian-abode-drilldowns/plan.md`: technical approach and verification strategy.
- `specs/1063-chaos-guardian-abode-drilldowns/tasks.md`: executable task list and evidence log.
- `specs/1063-chaos-guardian-abode-drilldowns/contracts/browser-chaos-guardian-abode-detail-actions.md`: command/action contract for selected details.
- `specs/1063-chaos-guardian-abode-drilldowns/checklists/requirements.md`: requirements quality checklist.
- `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` or nearby afterlife/browser tests: focused RED/GREEN coverage for overview actions, selected details, and raw-copy guards.
- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`: ensure relevant descriptors accept arguments only if missing.
- `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs` and shared result builders: route browser commands with arguments to shared C# result builders.
- Existing afterlife command/result builders under `BookOfEternityClient/`: add detail action metadata and selected-detail rendering where the current code owns those command outputs.
- `BookOfEternityClient.WebFrontend/`: avoid production changes unless current rendering cannot present existing `UiAction`/detail result surfaces correctly.
- GM-facing docs/examples: update only if runtime contracts or GM-authored state schema/behavior change; otherwise document no-impact rationale.

## Implementation Approach

1. Inspect current command catalog descriptors for `/guardians`, `/abodes`, `/abode_power`, and `/guardian_projects`, including whether they accept arguments and how aliases are parsed.
2. Inspect browser command service dispatch for read-only afterlife commands and compare with #1057 mortal reference detail action patterns.
3. Inspect console ExplorerMode Guardian/Abode selectors/detail panels to identify player-facing selected-detail semantics without copying console presentation wholesale.
4. Add focused RED tests for browser overview actions and selected-detail commands using seeded rich afterlife fixture data.
5. Implement minimal shared C# changes that add detail actions and selected-detail rendering while preserving overviews and read-only behavior.
6. Add or update source guards to prevent raw/API/DTO/debug/path leakage in default browser detail output.
7. Run focused tests, broader afterlife/browser/console slice, C# builds, Spec Kit prerequisite check, diff/static scans, and frontend verify if frontend files changed.
8. Commit with `[skip ci]`. Hermes will launch independent review, create PR, squash merge, comment evidence, close #1063, and clean up.

## Data Model / Action Contract

Each detail action should include:

- `Id`: stable and scoped to the surface and canonical id, for example `guardians-detail-<guardianId>` or equivalent existing project convention.
- `Label`: Russian/in-world label containing the entity name and a detail affordance such as `Подробно`.
- `Command`: the canonical command plus subcommand/argument used by `ExplorerCommandParser`, for example `/guardians хранитель <id>` if that matches the implemented command grammar.
- `Style`: `UiActionStyle.Secondary` unless existing project convention dictates another non-danger style.
- `RequiresConfirmation`: `false` for read-only detail actions.
- `Payload`: omitted or limited to safe presentation metadata; no raw state dumps.

## Risk Controls

- Do not remove overview tables/blocks while adding selected details.
- Do not add React-side Guardian/Abode gameplay filters or mutations.
- Do not create or modify pending/control files.
- Do not change afterlife runtime contracts without same-PR docs/tests.
- Do not expose raw JSON, `game_state/`, API, DTO, endpoint, debug, or local file paths in default player-facing output.
- Split newly discovered broad surfaces to follow-up issues rather than expanding #1063 beyond Guardian/Abode overview details.

## Verification Commands

Baseline and post-change commands should include real non-zero counts:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Chaos|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

git diff --check origin/main...HEAD
```

If afterlife docs/contracts change:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"
```

If frontend files change:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

## Expected PR / Closure Evidence

- PR body uses `Closes #1063` only for the source issue.
- PR references #949 and #1064-#1067 only as non-closing context unless a sibling is fully satisfied in this branch.
- Issue evidence comment includes changed files, local-gated verification commands/counts, independent review verdict, docs/prompts impact, and `GitHub Actions: not used/not required`.
