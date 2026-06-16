# Implementation Plan: Afterlife Soul Relic/Archive Browser Drill-Downs

**Branch**: `work/1064-afterlife-soul-relic-archive-drilldowns` | **Date**: 2026-06-16 | **Spec**: `specs/1064-afterlife-soul-relic-archive-drilldowns/spec.md`

## Source Issue

- GitHub issue #1064 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1064
- Origin audit row: `docs/audits/afterlife-drilldown-audit.md` AFD-003 from #949.

## Summary

Add shared C# browser command-result detail actions and selected-detail rendering for afterlife Soul Relic and Archive surfaces. Preserve existing overview output and local action forms, keep newly added drill-downs read-only, avoid React-side gameplay rules, and cover console/browser parity with focused tests/source guards.

## Technical Context

- **Language/runtime**: C# / .NET 8 command protocol, Explorer command services, afterlife command/result builders, tests; React/Vite only if existing command-result rendering cannot present returned `UiAction`/safe result blocks.
- **Primary source areas**: `BookOfEternityClient/CommandProtocol/`, `BookOfEternityClient/WebUi/`, relevant afterlife/ExplorerMode result builders under `BookOfEternityClient/`, and `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` or nearby afterlife/browser source guards.
- **Existing governance**: `AGENTS.md`, `.specify/memory/constitution.md`, Book project references for afterlife drill-down children, Browser Reborn panels, contextual actions, action result surfaces, #1064 issue body, and #949 audit artifact/spec.
- **Precedent**: #1063 Chaos Sea Guardian/Abode detail actions; #1057 mortal reference detail actions in `ExplorerWebCommandServiceTests` and shared command-result action patterns using `UiAction`.

## Constitution Check

- **Issue traceability**: All repo edits are tied to #1064; spec/plan/tasks/contract/checklist link #1064 and #949.
- **Player-facing integrity**: Default browser output must be Russian/in-world and must not expose API, DTO, endpoint, protocol, debug, raw slash-command, or path copy outside advanced diagnostics.
- **Contract/state authority**: The feature is intended as read-only presentation over existing afterlife state and existing local action forms. Runtime schema, pending/control, validation, normalizer, GM prompts/examples, and manifests are not planned to change.
- **Test-first verification**: Add failing focused tests/source guards before production code for new detail actions and selected-detail behavior.
- **Agent orchestration**: Hermes owns final acceptance/PR/merge/closure; Codex may implement and verify in the issue worktree.

## Project Structure and Responsibilities

- `specs/1064-afterlife-soul-relic-archive-drilldowns/spec.md`: product requirements and boundaries.
- `specs/1064-afterlife-soul-relic-archive-drilldowns/plan.md`: technical approach and verification strategy.
- `specs/1064-afterlife-soul-relic-archive-drilldowns/tasks.md`: executable task list and evidence log.
- `specs/1064-afterlife-soul-relic-archive-drilldowns/contracts/browser-afterlife-relic-archive-detail-actions.md`: command/action contract for selected details.
- `specs/1064-afterlife-soul-relic-archive-drilldowns/checklists/requirements.md`: requirements quality checklist.
- `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` or nearby afterlife/browser tests: focused RED/GREEN coverage for overview/local-action preservation, detail actions, selected details, stale ids, and raw-copy guards.
- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`: ensure relevant descriptors accept arguments only if missing.
- `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs` and shared result builders: route browser commands with arguments to shared C# result builders.
- Existing afterlife command/result builders under `BookOfEternityClient/`: add detail action metadata and selected-detail rendering where current code owns those command outputs.
- `BookOfEternityClient.WebFrontend/`: avoid production changes unless current rendering cannot present existing `UiAction`/safe result surfaces correctly.
- GM-facing docs/examples: update only if runtime contracts or GM-authored state schema/behavior change; otherwise document no-impact rationale.

## Implementation Approach

1. Inspect current command catalog descriptors and parser behavior for `/soul_relics`, `/soul_relic_equip`, `/soul_relic_unequip`, `/afterlife_archive`, `/archive_candidates`, `/archive_consultation`, and `/archive_project_fuel`, including aliases and argument acceptance.
2. Inspect browser command service dispatch for read-only afterlife commands and compare with #1063/#1057 shared detail action patterns.
3. Inspect console ExplorerMode Soul Relic and Archive selectors/detail panels to identify player-facing selected-detail semantics without copying console presentation wholesale.
4. Add focused RED tests for browser overview/local-action results and selected-detail commands using seeded rich afterlife fixture data.
5. Implement minimal shared C# changes that add detail actions and selected-detail rendering while preserving existing overviews and local action forms.
6. Add or update source guards to prevent raw/API/DTO/debug/path leakage in default browser detail output.
7. Run focused tests, broader afterlife/browser/console slice, C# builds, Spec Kit prerequisite check, diff/static scans, and frontend verify if frontend files changed.
8. Commit with `[skip ci]`. Hermes will launch independent review, create PR, squash merge, comment evidence, close #1064, and clean up.

## Data Model / Action Contract

Each detail action should include:

- `Id`: stable and scoped to the surface and canonical id, for example `soul-relic-detail-<relicId>` or `archive-candidate-detail-<candidateId>` using the existing project convention where available.
- `Label`: Russian/in-world label containing the row name and a detail affordance such as `Подробно`.
- `Command`: the canonical command plus subcommand/argument used by `ExplorerCommandParser`, for example `/soul_relics relic <id>` or the implemented grammar that best matches existing parser conventions.
- `Style`: `UiActionStyle.Secondary` unless existing project convention dictates another non-danger style.
- `RequiresConfirmation`: `false` for read-only detail actions.
- `Payload`: omitted or limited to safe presentation metadata; no raw state dumps.

## Risk Controls

- Do not remove overview tables/blocks while adding selected details.
- Do not break existing local action forms for equip/unequip/archive consultation/project fuel.
- Do not add React-side relic/archive gameplay filters or mutations.
- Do not create or modify pending/control files.
- Do not change afterlife runtime contracts without same-PR docs/tests.
- Do not expose raw JSON, `game_state/`, API, DTO, endpoint, protocol, debug, raw slash commands, or local file paths in default player-facing output.
- Split newly discovered broad surfaces to follow-up issues rather than expanding #1064 beyond Soul Relic/Archive selected details.

## Verification Commands

Baseline and post-change commands should include real non-zero counts:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~SoulRelic|FullyQualifiedName~Archive|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

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

- PR body uses `Closes #1064` only for the source issue.
- PR references #949 and #1065-#1067 only as non-closing context unless a sibling is fully satisfied in this branch.
- Issue evidence comment includes changed files, local-gated verification commands/counts, independent review verdict, docs/prompts impact, and `GitHub Actions: not used/not required`.
